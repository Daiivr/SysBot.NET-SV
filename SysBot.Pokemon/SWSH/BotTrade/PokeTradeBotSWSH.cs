﻿using Newtonsoft.Json.Linq;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using PKHeX.Core.Searching;
using SysBot.Base;
using System;
using System.Buffers.Binary;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

namespace SysBot.Pokemon
{
    public class PokeTradeBotSWSH : PokeRoutineExecutor8SWSH, ICountBot
    {
        public static ISeedSearchHandler<PK8> SeedChecker = new NoSeedSearchHandler<PK8>();
        private readonly PokeTradeHub<PK8> Hub;
        private readonly TradeSettings TradeSettings;
        private readonly TradeAbuseSettings AbuseSettings;
        public ICountSettings Counts => TradeSettings;

        /// <summary>
        /// Folder to dump received trade data to.
        /// </summary>
        /// <remarks>If null, will skip dumping.</remarks>
        private readonly IDumper DumpSetting;

        /// <summary>
        /// Synchronized start for multiple bots.
        /// </summary>
        public bool ShouldWaitAtBarrier { get; private set; }

        /// <summary>
        /// Tracks failed synchronized starts to attempt to re-sync.
        /// </summary>
        public int FailedBarrier { get; private set; }

        public PokeTradeBotSWSH(PokeTradeHub<PK8> hub, PokeBotState cfg) : base(cfg)
        {
            Hub = hub;
            TradeSettings = hub.Config.Trade;
            AbuseSettings = hub.Config.TradeAbuse;
            DumpSetting = hub.Config.Folder;
        }

        // Cached offsets that stay the same per session.
        private ulong OverworldOffset;

        public override async Task MainLoop(CancellationToken token)
        {
            try
            {
                await InitializeHardware(Hub.Config.Trade, token).ConfigureAwait(false);

                Log("Identifying trainer data of the host console.");
                var sav = await IdentifyTrainer(token).ConfigureAwait(false);
                RecentTrainerCache.SetRecentTrainer(sav);
                await InitializeSessionOffsets(token).ConfigureAwait(false);

                Log($"Starting main {nameof(PokeTradeBotSWSH)} loop.");
                await InnerLoop(sav, token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log(e.Message);
            }

            Log($"Ending {nameof(PokeTradeBotSWSH)} loop.");
            await HardStop().ConfigureAwait(false);
        }

        public override async Task HardStop()
        {
            UpdateBarrier(false);
            await CleanExit(CancellationToken.None).ConfigureAwait(false);
        }

        public override async Task RebootAndStop(CancellationToken t)
        {
            await ReOpenGame(Hub.Config, t).ConfigureAwait(false);
            await HardStop().ConfigureAwait(false);
        }

        private async Task InnerLoop(SAV8SWSH sav, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Config.IterateNextRoutine();
                var task = Config.CurrentRoutineType switch
                {
                    PokeRoutineType.Idle => DoNothing(token),
                    PokeRoutineType.SurpriseTrade => DoSurpriseTrades(sav, token),
                    _ => DoTrades(sav, token),
                };
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (SocketException e)
                {
                    if (e.StackTrace != null)
                        Connection.LogError(e.StackTrace);
                    var attempts = Hub.Config.Timings.ReconnectAttempts;
                    var delay = Hub.Config.Timings.ExtraReconnectDelay;
                    var protocol = Config.Connection.Protocol;
                    if (!await TryReconnect(attempts, delay, protocol, token).ConfigureAwait(false))
                        return;
                }
            }
        }

        private async Task DoNothing(CancellationToken token)
        {
            int waitCounter = 0;
            while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.Idle)
            {
                if (waitCounter == 0)
                    Log("No task assigned. Waiting for new task assignment.");
                waitCounter++;
                if (waitCounter % 10 == 0 && Hub.Config.AntiIdle)
                    await Click(B, 1_000, token).ConfigureAwait(false);
                else
                    await Task.Delay(1_000, token).ConfigureAwait(false);
            }
        }

        private async Task DoTrades(SAV8SWSH sav, CancellationToken token)
        {
            var type = Config.CurrentRoutineType;
            int waitCounter = 0;
            await SetCurrentBox(0, token).ConfigureAwait(false);
            while (!token.IsCancellationRequested && Config.NextRoutineType == type)
            {
                var (detail, priority) = GetTradeData(type);
                if (detail is null)
                {
                    await WaitForQueueStep(waitCounter++, token).ConfigureAwait(false);
                    continue;
                }
                waitCounter = 0;

                detail.IsProcessing = true;
                string tradetype = $" ({detail.Type})";
                Log($"Starting next {type}{tradetype} Bot Trade. Getting data...");
                Hub.Config.Stream.StartTrade(this, detail, Hub);
                Hub.Queues.StartTrade(this, detail);

                await PerformTrade(sav, detail, type, priority, token).ConfigureAwait(false);
            }
        }

        private async Task WaitForQueueStep(int waitCounter, CancellationToken token)
        {
            if (waitCounter == 0)
            {
                // Updates the assets.
                Hub.Config.Stream.IdleAssets(this);
                Log("Nothing to check, waiting for new users...");
            }

            const int interval = 10;
            if (waitCounter % interval == interval-1 && Hub.Config.AntiIdle)
                await Click(B, 1_000, token).ConfigureAwait(false);
            else
                await Task.Delay(1_000, token).ConfigureAwait(false);
        }

        protected virtual (PokeTradeDetail<PK8>? detail, uint priority) GetTradeData(PokeRoutineType type)
        {
            if (Hub.Queues.TryDequeue(type, out var detail, out var priority))
                return (detail, priority);
            if (Hub.Queues.TryDequeueLedy(out detail))
                return (detail, PokeTradePriorities.TierFree);
            return (null, PokeTradePriorities.TierFree);
        }

        private async Task PerformTrade(SAV8SWSH sav, PokeTradeDetail<PK8> detail, PokeRoutineType type, uint priority, CancellationToken token)
        {
            PokeTradeResult result;
            try
            {
                result = await PerformLinkCodeTrade(sav, detail, token).ConfigureAwait(false);
                if (result == PokeTradeResult.Success)
                    return;
            }
            catch (SocketException socket)
            {
                Log(socket.Message);
                result = PokeTradeResult.ExceptionConnection;
                HandleAbortedTrade(detail, type, priority, result);
                throw; // let this interrupt the trade loop. re-entering the trade loop will recheck the connection.
            }
            catch (Exception e)
            {
                Log(e.Message);
                result = PokeTradeResult.ExceptionInternal;
            }

            HandleAbortedTrade(detail, type, priority, result);
        }

        private void HandleAbortedTrade(PokeTradeDetail<PK8> detail, PokeRoutineType type, uint priority, PokeTradeResult result)
        {
            detail.IsProcessing = false;
            if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
            {
                detail.IsRetry = true;
                Hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
                detail.SendNotification(this, "⚠️ Oops! Algo ocurrio. Intentemoslo una ves mas.");
            }
            else
            {
                detail.SendNotification(this, $"⚠️ Oops! Algo ocurrio. Cancelando el trade: **{result}**.");
                detail.TradeCanceled(this, result);
            }
        }

        private async Task DoSurpriseTrades(SAV8SWSH sav, CancellationToken token)
        {
            await SetCurrentBox(0, token).ConfigureAwait(false);
            while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.SurpriseTrade)
            {
                var pkm = Hub.Ledy.Pool.GetRandomSurprise();
                await EnsureConnectedToYComm(OverworldOffset, Hub.Config, token).ConfigureAwait(false);
                var _ = await PerformSurpriseTrade(sav, pkm, token).ConfigureAwait(false);
            }
        }

        private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV8SWSH sav, PokeTradeDetail<PK8> poke, CancellationToken token)
        {
            // Update Barrier Settings
            UpdateBarrier(poke.IsSynchronized);
            poke.TradeInitialize(this);
            await EnsureConnectedToYComm(OverworldOffset, Hub.Config, token).ConfigureAwait(false);
            Hub.Config.Stream.EndEnterCode(this);

            if (await CheckIfSoftBanned(token).ConfigureAwait(false))
                await UnSoftBan(token).ConfigureAwait(false);

            var toSend = poke.TradeData;
            if (toSend.Species != 0)
                await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);

            TradeExtensions<PK8>.SWSHTrade = toSend;

            if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                await ExitTrade(true, token).ConfigureAwait(false);
                return PokeTradeResult.RecoverStart;
            }

            while (await CheckIfSearchingForLinkTradePartner(token).ConfigureAwait(false))
            {
                Log("Still searching, resetting bot position.");
                await ResetTradePosition(token).ConfigureAwait(false);
            }

            Log("Opening Y-Comm menu.");
            await Click(Y, 2_000, token).ConfigureAwait(false);

            Log("Selecting Link Trade.");
            await Click(A, 1_500, token).ConfigureAwait(false);

            Log("Selecting Link Trade code.");
            await Click(DDOWN, 500, token).ConfigureAwait(false);

            for (int i = 0; i < 2; i++)
                await Click(A, 1_500, token).ConfigureAwait(false);

            // All other languages require an extra A press at this menu.
            if (GameLang != LanguageID.English && GameLang != LanguageID.Spanish)
                await Click(A, 1_500, token).ConfigureAwait(false);

            // Loading Screen
            if (poke.Type != PokeTradeType.Random)
                Hub.Config.Stream.StartEnterCode(this);
            await Task.Delay(Hub.Config.Timings.ExtraTimeOpenCodeEntry, token).ConfigureAwait(false);

            var code = poke.Code;
            Log($"Entering Link Trade code: {code:0000 0000}...");
            await EnterLinkCode(code, Hub.Config, token).ConfigureAwait(false);

            // Wait for Barrier to trigger all bots simultaneously.
            WaitAtBarrierIfApplicable(token);
            await Click(PLUS, 1_000, token).ConfigureAwait(false);

            Hub.Config.Stream.EndEnterCode(this);

            // Confirming and return to overworld.
            var delay_count = 0;
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                if (delay_count++ >= 5)
                {
                    // Too many attempts, recover out of the trade.
                    await ExitTrade(true, token).ConfigureAwait(false);
                    return PokeTradeResult.RecoverPostLinkCode;
                }

                for (int i = 0; i < 5; i++)
                    await Click(A, 0_800, token).ConfigureAwait(false);
            }

            poke.TradeSearching(this);
            await Task.Delay(0_500, token).ConfigureAwait(false);

            // Wait for a Trainer...
            var partnerFound = await WaitForTradePartnerOffer(token).ConfigureAwait(false);

            if (token.IsCancellationRequested)
                return PokeTradeResult.RoutineCancel;
            if (!partnerFound)
            {
                await ResetTradePosition(token).ConfigureAwait(false);
                return PokeTradeResult.NoTrainerFound;
            }

            // Select Pokemon
            // pkm already injected to b1s1
            await Task.Delay(5_500 + Hub.Config.Timings.ExtraTimeOpenBox, token).ConfigureAwait(false); // necessary delay to get to the box properly

            var trainerName = await GetTradePartnerName(TradeMethod.LinkTrade, token).ConfigureAwait(false);
            var trainerID = await GetTradePartnerID7(TradeMethod.LinkTrade, token).ConfigureAwait(false);
            var trainerNID = await GetTradePartnerNID(token).ConfigureAwait(false);
            RecordUtil<PokeTradeBotSWSH>.Record($"Initiating\t{trainerNID:X16}\t{trainerName}\t{poke.Trainer.TrainerName}\t{poke.Trainer.ID}\t{poke.ID}\t{toSend.EncryptionConstant:X8}");
            Log($"Found Link Trade partner: {trainerName}-{trainerID.TID7:000000} (ID: {trainerNID})");

            var partnerCheck = await CheckPartnerReputation(this, poke, trainerNID, trainerName, AbuseSettings, token);
            if (partnerCheck != PokeTradeResult.Success)
            {
                await ExitSeedCheckTrade(token).ConfigureAwait(false);
                return partnerCheck;
            }

            if (!await IsInBox(token).ConfigureAwait(false))
            {
                await ExitTrade(true, token).ConfigureAwait(false);
                return PokeTradeResult.RecoverOpenBox;
            }
			
			var info = await GetGameInfo(token).ConfigureAwait(false);
            var tradePartner = new TradePartnerSWSH(trainerID, trainerName, info.version, info.language, info.gender);

            if (Hub.Config.Trade.UseTradePartnerDetails && CanUsePartnerDetails(toSend, sav, tradePartner, poke, out var toSendEdited))
            {
                toSend = toSendEdited;
                await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);
            }

            // Confirm Box 1 Slot 1
            if (poke.Type == PokeTradeType.Specific || poke.Type == PokeTradeType.SupportTrade || poke.Type == PokeTradeType.Giveaway)
            {
                for (int i = 0; i < 5; i++)
                    await Click(A, 0_500, token).ConfigureAwait(false);
            }

            poke.SendNotification(this, $"Entrenador encontrado: **{trainerName}**.\n\n▼\n Aqui esta tu Informacion\n **TID**: __{trainerID.TID7}__\n▲\n\n Esperando por un __Pokémon__...");

            if (poke.Type == PokeTradeType.Dump)
                return await ProcessDumpTradeAsync(poke, token).ConfigureAwait(false);

            // Wait for User Input...
            var offered = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 25_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
            var oldEC = await Connection.ReadBytesAsync(LinkTradePartnerPokemonOffset, 4, token).ConfigureAwait(false);
            if (offered is null)
            {
                await ExitSeedCheckTrade(token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            if (poke.Type == PokeTradeType.Seed)
            {
                // Immediately exit, we aren't trading anything.
                return await EndSeedCheckTradeAsync(poke, offered, token).ConfigureAwait(false);
            }

            PokeTradeResult update;
            var trainer = new PartnerDataHolder(trainerNID, trainerName, $"{trainerID.TID7:000000}");
            (toSend, update) = await GetEntityToSend(sav, poke, offered, oldEC, toSend, trainer, token).ConfigureAwait(false);
            if (update != PokeTradeResult.Success)
            {
                await ExitTrade(false, token).ConfigureAwait(false);
                return update;
            }

            var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
            if (tradeResult != PokeTradeResult.Success)
            {
                await ExitTrade(false, token).ConfigureAwait(false);
                return tradeResult;
            }

            if (token.IsCancellationRequested)
            {
                await ExitTrade(false, token).ConfigureAwait(false);
                return PokeTradeResult.RoutineCancel;
            }

            // Trade was Successful!
            var received = await ReadBoxPokemon(0, 0, token).ConfigureAwait(false);
            // Pokémon in b1s1 is same as the one they were supposed to receive (was never sent).
            if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
            {
                Log("User did not complete the trade.");
                RecordUtil<PokeTradeBotSWSH>.Record($"Cancelled\t{trainerNID:X16}\t{trainerName}\t{poke.Trainer.TrainerName}\\t{poke.ID}\t{toSend.EncryptionConstant:X8}\t{offered.EncryptionConstant:X8}");
                await ExitTrade(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            // As long as we got rid of our inject in b1s1, assume the trade went through.
            Log("User completed the trade.");
            poke.TradeFinished(this, received);

            RecordUtil<PokeTradeBotSWSH>.Record($"Finished\t{trainerNID:X16}\t{toSend.EncryptionConstant:X8}\t{received.EncryptionConstant:X8}");

            // Only log if we completed the trade.
            UpdateCountsAndExport(poke, received, toSend);

            // Log for Trade Abuse tracking.
            LogSuccessfulTrades(poke, trainerNID, trainerName);

            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.Success;
        } 

        private async Task<(GameVersion version, LanguageID language, Gender gender)> GetGameInfo(CancellationToken token)
        {
            var info = await SwitchConnection.ReadBytesAsync(LinkTradePartnerInfoOffset, 0x4, token).ConfigureAwait(false);
            return ((GameVersion)info[0], (LanguageID)info[1], (Gender)info[2]);
        }        

        private static RemoteControlAccess GetReference(string name, ulong id, string comment) => new()
        {
            ID = id,
            Name = name,
            Comment = $"Added automatically on {DateTime.Now:yyyy.MM.dd-hh:mm:ss} ({comment})",
        };

        protected virtual async Task<bool> WaitForTradePartnerOffer(CancellationToken token)
        {
            Log("Waiting for trainer...");
            return await WaitForPokemonChanged(LinkTradePartnerPokemonOffset, Hub.Config.Trade.TradeWaitTime * 1_000, 0_200, token).ConfigureAwait(false);
        }

        private void UpdateCountsAndExport(PokeTradeDetail<PK8> poke, PK8 received, PK8 toSend)
        {
            var counts = TradeSettings;
            if (poke.Type == PokeTradeType.Random)
                counts.AddCompletedDistribution();
            else if (poke.Type == PokeTradeType.Clone)
                counts.AddCompletedClones();
            else if (poke.Type == PokeTradeType.FixOT)
                counts.AddCompletedFixOTs();
            else if (poke.Type == PokeTradeType.SupportTrade)
                counts.AddCompletedSupportTrades();
            else
                counts.AddCompletedTrade();

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
            {
                var subfolder = poke.Type.ToString().ToLower();
                DumpPokemon(DumpSetting.DumpFolder, subfolder, received); // received by bot
                if (poke.Type is PokeTradeType.Specific or PokeTradeType.Clone or PokeTradeType.FixOT or PokeTradeType.SupportTrade or PokeTradeType.Giveaway)
                    DumpPokemon(DumpSetting.DumpFolder, "traded", toSend); // sent to partner
            }
        }

        private async Task<PokeTradeResult> ConfirmAndStartTrading(PokeTradeDetail<PK8> detail, CancellationToken token)
        {
            // We'll keep watching B1S1 for a change to indicate a trade started -> should try quitting at that point.
            var oldEC = await Connection.ReadBytesAsync(BoxStartOffset, 8, token).ConfigureAwait(false);

            await Click(A, 3_000, token).ConfigureAwait(false);
            for (int i = 0; i < Hub.Config.Trade.MaxTradeConfirmTime; i++)
            {
                // If we are in a Trade Evolution/PokeDex Entry and the Trade Partner quits, we land on the Overworld
                if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                    return PokeTradeResult.TrainerLeft;
                if (await IsUserBeingShifty(detail, token).ConfigureAwait(false))
                    return PokeTradeResult.SuspiciousActivity;
                await Click(A, 1_000, token).ConfigureAwait(false);

                // EC is detectable at the start of the animation.
                var newEC = await Connection.ReadBytesAsync(BoxStartOffset, 8, token).ConfigureAwait(false);
                if (!newEC.SequenceEqual(oldEC))
                {
                    await Task.Delay(25_000, token).ConfigureAwait(false);
                    return PokeTradeResult.Success;
                }
            }

            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                return PokeTradeResult.TrainerLeft;

            return PokeTradeResult.Success;
        }
		
		private bool CanUsePartnerDetails(PK8 pk, SAV8SWSH sav, TradePartnerSWSH partner, PokeTradeDetail<PK8> trade, out PK8 res)
        {
            res = pk.Clone();

            if ((Species)pk.Species is Species.None or Species.Ditto || trade.Type is not PokeTradeType.Specific)
            {
                Log("Can not apply Partner details: Not a specific trade request.");
                return false;
            }

            //Current handler cannot be past gen OT
            if (!pk.IsNative && !Hub.Config.Legality.ForceTradePartnerInfo)
            {
                Log("Can not apply Partner details: Current handler cannot be different gen OT.");
                return false;
            }

            //Only override trainer details if user didn't specify OT details in the Showdown/PK9 request
            if (HasSetDetails(pk, fallback: sav))
            {
                Log("Can not apply Partner details: Requested Pokémon already has set Trainer details.");
                return false;
            }

            res.OT_Name = partner.TrainerName;
            res.OT_Gender = partner.Gender;
            res.TrainerTID7 = partner.TID7;
            res.TrainerSID7 = partner.SID7;
            res.Language = partner.Language;
            res.Version = partner.Game;

            if (!pk.IsNicknamed)
                res.ClearNickname();

            if (pk.IsShiny)
                res.PID = (uint)(((res.TID16 ^ res.SID16 ^ (res.PID & 0xFFFF) ^ pk.ShinyXor) << 16) | (res.PID & 0xFFFF));

            if (!pk.ChecksumValid)
                res.RefreshChecksum();

            var la = new LegalityAnalysis(res);
            if (!la.Valid)
            {
                Log("Can not apply Partner details:");
                Log(la.Report());

                if (!Hub.Config.Legality.ForceTradePartnerInfo)
                    return false;

                Log("Trying to force Trade Partner Info discarding the game version...");
                res.Version = pk.Version;
                la = new LegalityAnalysis(res);

                if (!la.Valid)
                {
                    Log("Can not apply Partner details:");
                    Log(la.Report());
                    return false;
                }
            }

            Log($"Applying trade partner details: {partner.TrainerName} ({(partner.Gender == 0 ? "M" : "F")}), " +
                $"TID: {partner.TID7:000000}, SID: {partner.SID7:0000}, {(LanguageID)partner.Language} ({(GameVersion)res.Version})");

            return true;
        }

        private bool HasSetDetails(PKM set, ITrainerInfo fallback)
        {
            var set_trainer = new SimpleTrainerInfo((GameVersion)set.Version)
            {
                OT = set.OT_Name,
                TID16 = set.TID16,
                SID16 = set.SID16,
                Gender = set.OT_Gender,
                Language = set.Language,
            };

            var def_trainer = new SimpleTrainerInfo((GameVersion)fallback.Game)
            {
                OT = Hub.Config.Legality.GenerateOT,
                TID16 = Hub.Config.Legality.GenerateTID16,
                SID16 = Hub.Config.Legality.GenerateSID16,
                Gender = Hub.Config.Legality.GenerateGenderOT,
                Language = (int)Hub.Config.Legality.GenerateLanguage,
            };

            var alm_trainer = Hub.Config.Legality.GeneratePathTrainerInfo != string.Empty ?
                TrainerSettings.GetSavedTrainerData(fallback.Generation, (GameVersion)fallback.Game, fallback, (LanguageID)fallback.Language) : null;

            return !IsEqualTInfo(set_trainer, def_trainer) && !IsEqualTInfo(set_trainer, alm_trainer);
        }

        private static bool IsEqualTInfo(ITrainerInfo trainerInfo, ITrainerInfo? compareInfo)
        {
            if (compareInfo is null)
                return false;

            if (!trainerInfo.OT.Equals(compareInfo.OT))
                return false;

            if (trainerInfo.Gender != compareInfo.Gender)
                return false;

            if (trainerInfo.Language != compareInfo.Language)
                return false;

            if (trainerInfo.TID16 != compareInfo.TID16)
                return false;

            if (trainerInfo.SID16 != compareInfo.SID16)
                return false;

            return true;
        }

        protected virtual async Task<(PK8 toSend, PokeTradeResult check)> GetEntityToSend(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, byte[] oldEC, PK8 toSend, PartnerDataHolder partnerID, CancellationToken token)
        {
            return poke.Type switch
            {
                PokeTradeType.Random => await HandleRandomLedy(sav, poke, offered, toSend, partnerID, token).ConfigureAwait(false),
                PokeTradeType.Clone => await HandleClone(sav, poke, offered, oldEC, token).ConfigureAwait(false),
                PokeTradeType.FixOT => await HandleFixOT(sav, poke, offered, partnerID, token).ConfigureAwait(false),
                _ => (toSend, PokeTradeResult.Success),
            };
        }

        private async Task<(PK8 toSend, PokeTradeResult check)> HandleClone(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, byte[] oldEC, CancellationToken token)
        {
            if (Hub.Config.Discord.ReturnPKMs)
                poke.SendNotification(this, offered, "¡Aqui esta lo que me mostraste!");

            var la = new LegalityAnalysis(offered);
            if (!la.Valid)
            {
                Log($"Clone request (from {poke.Trainer.TrainerName}) has detected an invalid Pokémon: {GameInfo.GetStrings(1).Species[offered.Species]}.");
                if (DumpSetting.Dump)
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);

                var report = la.Report();
                Log(report);
                poke.SendNotification(this, "⚠️ Este Pokémon no es __**legal**__ según los controles de legalidad de __PKHeX__. Tengo prohibido clonar esto. Cancelando trade...");
                poke.SendNotification(this, report);

                return (offered, PokeTradeResult.IllegalTrade);
            }

            var clone = offered.Clone();
            TradeExtensions<PK8>.SWSHTrade = clone;

            poke.SendNotification(this, $"✔ He __clonado__ tu **{GameInfo.GetStrings(1).Species[clone.Species]}!**\nAhora __preciosa__ **B** para cancelar y luego seleccione un Pokémon que no quieras para reliazar el tradeo.");
            Log($"Cloned a {(Species)clone.Species}. Waiting for user to change their Pokémon...");

            // Separate this out from WaitForPokemonChanged since we compare to old EC from original read.
            var partnerFound = await ReadUntilChanged(LinkTradePartnerPokemonOffset, oldEC, 15_000, 0_200, false, token).ConfigureAwait(false);

            if (!partnerFound)
            {
                poke.SendNotification(this, "**Porfavor cambia el pokemon ahora o cancelare el tradeo!!!**");
                // They get one more chance.
                partnerFound = await ReadUntilChanged(LinkTradePartnerPokemonOffset, oldEC, 15_000, 0_200, false, token).ConfigureAwait(false);
            }

            var pk2 = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 3_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
            if (!partnerFound || pk2 == null || SearchUtil.HashByDetails(pk2) == SearchUtil.HashByDetails(offered))
            {
                Log("Trade partner did not change their Pokémon.");
                return (offered, PokeTradeResult.TrainerTooSlow);
            }

            await Click(A, 0_800, token).ConfigureAwait(false);
            await SetBoxPokemon(clone, 0, 0, token, sav).ConfigureAwait(false);

            for (int i = 0; i < 5; i++)
                await Click(A, 0_500, token).ConfigureAwait(false);

            return (clone, PokeTradeResult.Success);
        }

        private async Task<(PK8 toSend, PokeTradeResult check)> HandleRandomLedy(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, PK8 toSend, PartnerDataHolder partner, CancellationToken token)
        {
            // Allow the trade partner to do a Ledy swap.
            var config = Hub.Config.Distribution;
            var trade = Hub.Ledy.GetLedyTrade(offered, partner.TrainerOnlineID, config.LedySpecies);
            if (trade != null)
            {
                if (trade.Type == LedyResponseType.AbuseDetected)
                {
                    var msg = $"⚠️ He encontrado a **{partner.TrainerName}** abusando de los __Ledy trades__.";
                    if (AbuseSettings.EchoNintendoOnlineIDLedy)
                        msg += $"\n**ID**: {partner.TrainerOnlineID}";
                    if (!string.IsNullOrWhiteSpace(AbuseSettings.LedyAbuseEchoMention))
                        msg = $"{AbuseSettings.LedyAbuseEchoMention} {msg}";
                    EchoUtil.Echo(msg);

                    return (toSend, PokeTradeResult.SuspiciousActivity);
                }

                toSend = trade.Receive;
                poke.TradeData = toSend;

                poke.SendNotification(this, "✔ Inyectando el pokemon solicitado!.");
                await Click(A, 0_800, token).ConfigureAwait(false);
                await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);
                await Task.Delay(2_500, token).ConfigureAwait(false);
            }
            else if (config.LedyQuitIfNoMatch)
            {
                return (toSend, PokeTradeResult.TrainerRequestBad);
            }

            for (int i = 0; i < 5; i++)
            {
                if (await IsUserBeingShifty(poke, token).ConfigureAwait(false))
                    return (toSend, PokeTradeResult.SuspiciousActivity);
                await Click(A, 0_500, token).ConfigureAwait(false);
            }

            return (toSend, PokeTradeResult.Success);
        }

        // For pointer offsets that don't change per session are accessed frequently, so set these each time we start.
        private async Task InitializeSessionOffsets(CancellationToken token)
        {
            Log("Caching session offsets...");
            OverworldOffset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
        }

        protected virtual async Task<bool> IsUserBeingShifty(PokeTradeDetail<PK8> detail, CancellationToken token)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return false;
        }

        private async Task RestartGameSWSH(CancellationToken token)
        {
            await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            await InitializeSessionOffsets(token).ConfigureAwait(false);
        }

        private async Task<PokeTradeResult> ProcessDumpTradeAsync(PokeTradeDetail<PK8> detail, CancellationToken token)
        {
            int ctr = 0;
            var time = TimeSpan.FromSeconds(Hub.Config.Trade.MaxDumpTradeTime);
            var start = DateTime.Now;
            var pkprev = new PK8();
            var bctr = 0;
            while (ctr < Hub.Config.Trade.MaxDumpsPerTrade && DateTime.Now - start < time)
            {
                if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                    break;
                if (bctr++ % 3 == 0)
                    await Click(B, 0_100, token).ConfigureAwait(false);

                var pk = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 3_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
                if (pk == null || pk.Species < 1 || !pk.ChecksumValid || SearchUtil.HashByDetails(pk) == SearchUtil.HashByDetails(pkprev))
                    continue;

                // Save the new Pokémon for comparison next round.
                pkprev = pk;

                // Send results from separate thread; the bot doesn't need to wait for things to be calculated.
                if (DumpSetting.Dump)
                {
                    var subfolder = detail.Type.ToString().ToLower();
                    DumpPokemon(DumpSetting.DumpFolder, subfolder, pk); // received
                }

                var la = new LegalityAnalysis(pk);
                var verbose = $"```{la.Report(true)}```";
                Log($"Shown Pokémon is: {(la.Valid ? "Valid" : "Invalid")}.");

                ctr++;
                var msg = Hub.Config.Trade.DumpTradeLegalityCheck ? verbose : $"File {ctr}";

                // Extra information about trainer data for people requesting with their own trainer data.
                var ot = pk.OT_Name;
                var ot_gender = pk.OT_Gender == 0 ? "Male" : "Female";
                var tid = pk.GetDisplayTID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringTID());
                var sid = pk.GetDisplaySID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringSID());
                msg += $"\n**Trainer Data**\n```OT: {ot}\nOTGender: {ot_gender}\nTID: {tid}\nSID: {sid}```";

                // Extra information for shiny eggs, because of people dumping to skip hatching.
                var eggstring = pk.IsEgg ? "Egg " : string.Empty;
                msg += pk.IsShiny ? $"\n**This Pokémon {eggstring}is shiny!**" : string.Empty;
                detail.SendNotification(this, pk, msg);
            }

            Log($"Ended Dump loop after processing {ctr} Pokémon.");
            await ExitSeedCheckTrade(token).ConfigureAwait(false);
            if (ctr == 0)
                return PokeTradeResult.TrainerTooSlow;

            TradeSettings.AddCompletedDumps();
            detail.Notifier.SendNotification(this, detail, $"Dumped {ctr} Pokémon.");
            detail.Notifier.TradeFinished(this, detail, detail.TradeData); // blank pk8
            return PokeTradeResult.Success;
        }

        private async Task<PokeTradeResult> PerformSurpriseTrade(SAV8SWSH sav, PK8 pkm, CancellationToken token)
        {
            // General Bot Strategy:
            // 1. Inject to b1s1
            // 2. Send out Trade
            // 3. Clear received PKM to skip the trade animation
            // 4. Repeat

            // Inject to b1s1
            if (await CheckIfSoftBanned(token).ConfigureAwait(false))
                await UnSoftBan(token).ConfigureAwait(false);

            Log("Starting next Surprise Trade. Getting data...");
            await SetBoxPokemon(pkm, 0, 0, token, sav).ConfigureAwait(false);

            if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                await ExitTrade(true, token).ConfigureAwait(false);
                return PokeTradeResult.RecoverStart;
            }

            if (await CheckIfSearchingForSurprisePartner(token).ConfigureAwait(false))
            {
                Log("Still searching, resetting bot position.");
                await ResetTradePosition(token).ConfigureAwait(false);
            }

            Log("Opening Y-Comm menu.");
            await Click(Y, 1_500, token).ConfigureAwait(false);

            if (token.IsCancellationRequested)
                return PokeTradeResult.RoutineCancel;

            Log("Selecting Surprise Trade.");
            await Click(DDOWN, 0_500, token).ConfigureAwait(false);
            await Click(A, 2_000, token).ConfigureAwait(false);

            if (token.IsCancellationRequested)
                return PokeTradeResult.RoutineCancel;

            await Task.Delay(0_750, token).ConfigureAwait(false);

            if (!await IsInBox(token).ConfigureAwait(false))
            {
                await ExitTrade(true, token).ConfigureAwait(false);
                return PokeTradeResult.RecoverPostLinkCode;
            }

            Log($"Selecting Pokémon: {pkm.FileName}");
            // Box 1 Slot 1; no movement required.
            await Click(A, 0_700, token).ConfigureAwait(false);

            if (token.IsCancellationRequested)
                return PokeTradeResult.RoutineCancel;

            Log("Confirming...");
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await Click(A, 0_800, token).ConfigureAwait(false);

            if (token.IsCancellationRequested)
                return PokeTradeResult.RoutineCancel;

            // Let Surprise Trade be sent out before checking if we're back to the Overworld.
            await Task.Delay(3_000, token).ConfigureAwait(false);

            if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                await ExitTrade(true, token).ConfigureAwait(false);
                return PokeTradeResult.RecoverReturnOverworld;
            }

            // Wait 30 Seconds for Trainer...
            Log("Waiting for Surprise Trade partner...");

            // Wait for an offer...
            var oldEC = await Connection.ReadBytesAsync(SurpriseTradeSearchOffset, 4, token).ConfigureAwait(false);
            var partnerFound = await ReadUntilChanged(SurpriseTradeSearchOffset, oldEC, Hub.Config.Trade.TradeWaitTime * 1_000, 0_200, false, token).ConfigureAwait(false);

            if (token.IsCancellationRequested)
                return PokeTradeResult.RoutineCancel;

            if (!partnerFound)
            {
                await ResetTradePosition(token).ConfigureAwait(false);
                return PokeTradeResult.NoTrainerFound;
            }

            // Let the game flush the results and de-register from the online surprise trade queue.
            await Task.Delay(7_000, token).ConfigureAwait(false);

            var TrainerName = await GetTradePartnerName(TradeMethod.SurpriseTrade, token).ConfigureAwait(false);
            var TrainerID = await GetTradePartnerID7(TradeMethod.SurpriseTrade, token).ConfigureAwait(false);
            var SurprisePoke = await ReadSurpriseTradePokemon(token).ConfigureAwait(false);

            Log($"Found Surprise Trade partner: {TrainerName}-{TrainerID.TID7:000000}, Pokémon: {(Species)SurprisePoke.Species}");

            // Clear out the received trade data; we want to skip the trade animation.
            // The box slot locks have been removed prior to searching.

            await Connection.WriteBytesAsync(BitConverter.GetBytes(SurpriseTradeSearch_Empty), SurpriseTradeSearchOffset, token).ConfigureAwait(false);
            await Connection.WriteBytesAsync(PokeTradeBotUtil.EMPTY_SLOT, SurpriseTradePartnerPokemonOffset, token).ConfigureAwait(false);

            // Let the game recognize our modifications before finishing this loop.
            await Task.Delay(5_000, token).ConfigureAwait(false);

            // Clear the Surprise Trade slot locks! We'll skip the trade animation and reuse the slot on later loops.
            // Write 8 bytes of FF to set both Int32's to -1. Regular locks are [Box32][Slot32]

            await Connection.WriteBytesAsync(BitConverter.GetBytes(ulong.MaxValue), SurpriseTradeLockBox, token).ConfigureAwait(false);

            if (token.IsCancellationRequested)
                return PokeTradeResult.RoutineCancel;

            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                Log("Trade complete!");
            else
                await ExitTrade(true, token).ConfigureAwait(false);

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                DumpPokemon(DumpSetting.DumpFolder, "surprise", SurprisePoke);
            TradeSettings.AddCompletedSurprise();

            return PokeTradeResult.Success;
        }

        private async Task<PokeTradeResult> EndSeedCheckTradeAsync(PokeTradeDetail<PK8> detail, PK8 pk, CancellationToken token)
        {
            await ExitSeedCheckTrade(token).ConfigureAwait(false);

            detail.TradeFinished(this, pk);

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                DumpPokemon(DumpSetting.DumpFolder, "seed", pk);

            // Send results from separate thread; the bot doesn't need to wait for things to be calculated.
#pragma warning disable 4014
            Task.Run(() =>
            {
                try
                {
                    ReplyWithSeedCheckResults(detail, pk);
                }
                catch (Exception ex)
                {
                    detail.SendNotification(this, $"No es posible calcular las semillas: {ex.Message}\r\n{ex.StackTrace}");
                }
            }, token);
#pragma warning restore 4014

            TradeSettings.AddCompletedSeedCheck();

            return PokeTradeResult.Success;
        }

        private void ReplyWithSeedCheckResults(PokeTradeDetail<PK8> detail, PK8 result)
        {
            detail.SendNotification(this, "Calculando su(s) semilla(s)...");

            if (result.IsShiny)
            {
                Log("The Pokémon is already shiny!"); // Do not bother checking for next shiny frame
                detail.SendNotification(this, "¡Este Pokémon ya es shiny! No se ha calculado la semilla de incursión.");

                if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                    DumpPokemon(DumpSetting.DumpFolder, "seed", result);

                detail.TradeFinished(this, result);
                return;
            }

            SeedChecker.CalculateAndNotify(result, detail, Hub.Config.SeedCheckSWSH, this);
            Log("Seed calculation completed.");
        }

        private void WaitAtBarrierIfApplicable(CancellationToken token)
        {
            if (!ShouldWaitAtBarrier)
                return;
            var opt = Hub.Config.Distribution.SynchronizeBots;
            if (opt == BotSyncOption.NoSync)
                return;

            var timeoutAfter = Hub.Config.Distribution.SynchronizeTimeout;
            if (FailedBarrier == 1) // failed last iteration
                timeoutAfter *= 2; // try to re-sync in the event things are too slow.

            var result = Hub.BotSync.Barrier.SignalAndWait(TimeSpan.FromSeconds(timeoutAfter), token);

            if (result)
            {
                FailedBarrier = 0;
                return;
            }

            FailedBarrier++;
            Log($"Barrier sync timed out after {timeoutAfter} seconds. Continuing.");
        }

        /// <summary>
        /// Checks if the barrier needs to get updated to consider this bot.
        /// If it should be considered, it adds it to the barrier if it is not already added.
        /// If it should not be considered, it removes it from the barrier if not already removed.
        /// </summary>
        private void UpdateBarrier(bool shouldWait)
        {
            if (ShouldWaitAtBarrier == shouldWait)
                return; // no change required

            ShouldWaitAtBarrier = shouldWait;
            if (shouldWait)
            {
                Hub.BotSync.Barrier.AddParticipant();
                Log($"Joined the Barrier. Count: {Hub.BotSync.Barrier.ParticipantCount}");
            }
            else
            {
                Hub.BotSync.Barrier.RemoveParticipant();
                Log($"Left the Barrier. Count: {Hub.BotSync.Barrier.ParticipantCount}");
            }
        }

        private async Task<bool> WaitForPokemonChanged(uint offset, int waitms, int waitInterval, CancellationToken token)
        {
            // check EC and checksum; some pkm may have same EC if shown sequentially
            var oldEC = await Connection.ReadBytesAsync(offset, 8, token).ConfigureAwait(false);
            return Hub.Config.Trade.SpinTrade ? await SpinTrade(offset, oldEC, waitms, waitInterval, false, token).ConfigureAwait(false) : await ReadUntilChanged(offset, oldEC, waitms, waitInterval, false, token).ConfigureAwait(false);
        }

        private async Task ExitTrade(bool unexpected, CancellationToken token)
        {
            if (unexpected)
                Log("Unexpected behavior, recovering position.");

            int attempts = 0;
            int softBanAttempts = 0;
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                var screenID = await GetCurrentScreen(token).ConfigureAwait(false);
                if (screenID == CurrentScreen_Softban)
                {
                    softBanAttempts++;
                    if (softBanAttempts > 10)
                        await RestartGameSWSH(token).ConfigureAwait(false);
                }

                attempts++;
                if (attempts >= 15)
                    break;

                await Click(B, 1_000, token).ConfigureAwait(false);
                await Click(B, 1_000, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);
            }
        }

        private async Task ExitSeedCheckTrade(CancellationToken token)
        {
            // Seed Check Bot doesn't show anything, so it can skip the first B press.
            int attempts = 0;
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                attempts++;
                if (attempts >= 15)
                    break;

                await Click(B, 1_000, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);
            }

            await Task.Delay(3_000, token).ConfigureAwait(false);
        }

        private async Task ResetTradePosition(CancellationToken token)
        {
            Log("Resetting bot position.");

            // Shouldn't ever be used while not on overworld.
            if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await ExitTrade(true, token).ConfigureAwait(false);

            // Ensure we're searching before we try to reset a search.
            if (!await CheckIfSearchingForLinkTradePartner(token).ConfigureAwait(false))
                return;

            await Click(Y, 2_000, token).ConfigureAwait(false);
            for (int i = 0; i < 5; i++)
                await Click(A, 1_500, token).ConfigureAwait(false);
            // Extra A press for Japanese.
            if (GameLang == LanguageID.Japanese)
                await Click(A, 1_500, token).ConfigureAwait(false);
            await Click(B, 1_500, token).ConfigureAwait(false);
            await Click(B, 1_500, token).ConfigureAwait(false);
        }

        private async Task<bool> CheckIfSearchingForLinkTradePartner(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(LinkTradeSearchingOffset, 1, token).ConfigureAwait(false);
            return data[0] == 1; // changes to 0 when found
        }

        private async Task<bool> CheckIfSearchingForSurprisePartner(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(SurpriseTradeSearchOffset, 8, token).ConfigureAwait(false);
            return BitConverter.ToUInt32(data, 0) == SurpriseTradeSearch_Searching;
        }

        private async Task<string> GetTradePartnerName(TradeMethod tradeMethod, CancellationToken token)
        {
            var ofs = GetTrainerNameOffset(tradeMethod);
            var data = await Connection.ReadBytesAsync(ofs, 26, token).ConfigureAwait(false);
            return StringConverter8.GetString(data);
        }

        private async Task<(uint TID7, uint SID7)> GetTradePartnerID7(TradeMethod tradeMethod, CancellationToken token)
        {
            var ofs = GetTrainerTIDSIDOffset(tradeMethod);
            var data = await Connection.ReadBytesAsync(ofs, 8, token).ConfigureAwait(false);
            var sid = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan()) / 1_000_000;
            var tid = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan()) % 1_000_000;
            return (tid, sid);
        }

        public async Task<ulong> GetTradePartnerNID(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(LinkTradePartnerNIDOffset, 8, token).ConfigureAwait(false);
            return BitConverter.ToUInt64(data, 0);
        }

        private async Task<(PK8 toSend, PokeTradeResult check)> HandleFixOT(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, PartnerDataHolder partner, CancellationToken token)
        {
            if (Hub.Config.Discord.ReturnPKMs)
                poke.SendNotification(this, offered, "¡Aqui esta lo que me mostraste!");

            var adOT = TradeExtensions<PK8>.HasAdName(offered, out _);
            var laInit = new LegalityAnalysis(offered);
            if (!adOT && laInit.Valid)
            {
                poke.SendNotification(this, "⚠️ No se ha detectado ningún __anuncio__ en Nickname u OT, y el Pokémon es **legal**. Saliendo del comercio.");
                return (offered, PokeTradeResult.TrainerRequestBad);
            }

            var clone = offered.Clone();
            string shiny = string.Empty;
            if (!TradeExtensions<PK8>.ShinyLockCheck(offered.Species, TradeExtensions<PK8>.FormOutput(offered.Species, offered.Form, out _), $"{(Ball)offered.Ball}"))
                shiny = $"\nShiny: {(offered.ShinyXor == 0 ? "Square" : offered.IsShiny ? "Star" : "No")}";
            else shiny = "\nShiny: No";

            var name = partner.TrainerName;
            var ball = $"\n{(Ball)offered.Ball}";
            var extraInfo = $"OT: {name}{ball}{shiny}";
            var set = ShowdownParsing.GetShowdownText(offered).Split('\n').ToList();
            var shinyRes = set.Find(x => x.Contains("Shiny"));
            if (shinyRes != null)
                set.Remove(shinyRes);
            set.InsertRange(1, extraInfo.Split('\n'));

            if (!laInit.Valid)
            {
                Log($"FixOT request has detected an illegal Pokémon from {name}: {(Species)offered.Species}");
                var report = laInit.Report();
                Log(laInit.Report());
                poke.SendNotification(this, $"**El pokémon mostrado no es legal. Intentando regenerar...**\n\n```{report}```");
                if (DumpSetting.Dump)
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
            }

            if (clone.FatefulEncounter)
            {
                clone.SetDefaultNickname(laInit);
                var info = new SimpleTrainerInfo { Gender = clone.OT_Gender, Language = clone.Language, OT = name, TID16 = clone.TID16, SID16 = clone.SID16, Generation = 8 };
                var mg = EncounterEvent.GetAllEvents().Where(x => x.Species == clone.Species && x.Form == clone.Form && x.IsShiny == clone.IsShiny && x.OT_Name == clone.OT_Name).ToList();
                if (mg.Count > 0)
                    clone = TradeExtensions<PK8>.CherishHandler(mg.First(), info);
                else clone = (PK8)sav.GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(string.Join("\n", set))), out _);
            }
            else clone = (PK8)sav.GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(string.Join("\n", set))), out _);

            clone = (PK8)TradeExtensions<PK8>.TrashBytes(clone, new LegalityAnalysis(clone));
            clone.ResetPartyStats();
            var la = new LegalityAnalysis(clone);
            if (!la.Valid)
            {
                poke.SendNotification(this, "⚠️ Este Pokémon no es legal según las comprobaciones de legalidad de PKHeX. No he podido solucionarlo. Saliendo del intercambio.");
                return (clone, PokeTradeResult.IllegalTrade);
            }

            TradeExtensions<PK8>.SWSHTrade = clone;

            poke.SendNotification(this, $"{(!laInit.Valid ? "**Legalizado" : "**Arreglado Apodo/OT para")} {(Species)clone.Species}**!");
            Log($"{(!laInit.Valid ? "Legalizado" : "Arreglado Apodo/OT para")} {(Species)clone.Species}!");

            await Click(A, 0_800, token).ConfigureAwait(false);
            await SetBoxPokemon(clone, 0, 0, token, sav).ConfigureAwait(false);
            await Click(A, 0_500, token).ConfigureAwait(false);
            poke.SendNotification(this, "¡Ahora confirma el intercambio!");

            await Task.Delay(6_000, token).ConfigureAwait(false);
            var pk2 = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 1_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
            bool changed = pk2 == null || clone.Species != pk2.Species || offered.OT_Name != pk2.OT_Name;
            if (changed)
            {
                Log($"{name} changed the shown Pokémon ({(Species)clone.Species}){(pk2 != null ? $" to {(Species)pk2.Species}" : "")}");
                poke.SendNotification(this, "**¡Despacha los Pokémon mostrados originalmente, por favor!**");
                var timer = 10_000;
                while (changed)
                {
                    pk2 = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 2_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
                    changed = pk2 == null || clone.Species != pk2.Species || offered.OT_Name != pk2.OT_Name;
                    await Task.Delay(1_000, token).ConfigureAwait(false);
                    timer -= 1_000;
                    if (timer <= 0)
                        break;
                }
            }

            if (changed)
            {
                poke.SendNotification(this, "Pokémon intercambiado y no cambiado de nuevo. Saliendo del intercambio.");
                Log("Trading partner did not wish to send away their ad-mon.");
                return (offered, PokeTradeResult.TrainerTooSlow);
            }

            await Click(A, 0_500, token).ConfigureAwait(false);
            for (int i = 0; i < 5; i++)
                await Click(A, 0_500, token).ConfigureAwait(false);

            return (clone, PokeTradeResult.Success);
        }
    }
}