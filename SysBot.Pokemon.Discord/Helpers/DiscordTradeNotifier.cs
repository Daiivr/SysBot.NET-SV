using Discord;
using Discord.WebSocket;
using PKHeX.Core;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Discord.Commands;

namespace SysBot.Pokemon.Discord
{
    public class DiscordTradeNotifier<T> : IPokeTradeNotifier<T> where T : PKM, new()
    {
        private T Data { get; }
        private PokeTradeTrainerInfo Info { get; }
        private int Code { get; }
        private SocketUser Trader { get; }
        private SocketCommandContext Context { get; }
        public Action<PokeRoutineExecutor<T>>? OnFinish { private get; set; }
        public readonly PokeTradeHub<T> Hub = SysCord<T>.Runner.Hub;

        public DiscordTradeNotifier(T data, PokeTradeTrainerInfo info, int code, SocketUser trader, SocketCommandContext channel)
        {
            Data = data;
            Info = info;
            Code = code;
            Trader = trader;
            Context = channel;
        }

        public void TradeInitialize(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
        {
            var receive = Data.Species == 0 ? string.Empty : $" ({Data.Nickname})";
            Trader.SendMessageAsync($"Iniciando __trade__ **{receive}**. Porfavor este atento. Tu codigo es: **{Code:0000 0000}**.").ConfigureAwait(false);
        }

        public void TradeSearching(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
        {
            var name = Info.TrainerName;
            var trainer = string.IsNullOrEmpty(name) ? string.Empty : $" {name}";
            Trader.SendMessageAsync($"Estoy esperando por ti, **{trainer}**! __Tienes **40 segundos**__. Tu codigo es: **{Code:0000 0000}**. Mi IGN es **{routine.InGameName}**.").ConfigureAwait(false);
        }

        public void TradeCanceled(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeResult msg)
        {
            OnFinish?.Invoke(routine);
            Trader.SendMessageAsync($"✘ Trade __cancelado__: {msg}").ConfigureAwait(false);
        }

        public void TradeFinished(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result)
        {
            OnFinish?.Invoke(routine);
            var tradedToUser = Data.Species;
            var message = tradedToUser != 0 ? $"✓ Trade finalizado. Disfruta de tu **{(Species)tradedToUser}**!" : "✔ Trade finalizado!";
            Trader.SendMessageAsync(message).ConfigureAwait(false);
            if (result.Species != 0 && Hub.Config.Discord.ReturnPKMs)
                Trader.SendPKMAsync(result, "▼ Aqui esta lo que me enviaste! ▼").ConfigureAwait(false);

            var SVmon = TradeExtensions<PK9>.SVTrade;
            var LAmon = TradeExtensions<PA8>.LATrade;
            var BDSPmon = TradeExtensions<PB8>.BDSPTrade;
            var SWSHmon = TradeExtensions<PK8>.SWSHTrade;
            PKM fin = result;
            switch (result)
            {
                case PK9: fin = SVmon; break;
                case PA8: fin = LAmon; break;
                case PB8: fin = BDSPmon; break;
                case PK8: fin = SWSHmon; break;
            }

            if (fin.Species != 0 && Hub.Config.Trade.TradeDisplay)
            {
		var shiny = fin.ShinyXor == 0 ? "■" : fin.ShinyXor <= 16 ? "★" : "";
                var set = new ShowdownSet($"{fin.Species}");
                var ballImg = $"https://raw.githubusercontent.com/BakaKaito/HomeImages/main/Ballimg/50x50/" + $"{(Ball)fin.Ball}ball".ToLower() + ".png";
                var gender = fin.Gender == 0 ? " - (M)" : fin.Gender == 1 ? " - (F)" : "";
                var pokeImg = TradeExtensions<T>.PokeImg(fin, false, false);
                var trademessage = $"**Nivel**: {fin.CurrentLevel}\n" +
                    $"**Habilidad**: {(Ability)fin.Ability}\n" +
                    $"**Naturaleza**: {(Nature)fin.Nature}\n" +
                    $"**IVs**: {fin.IV_HP}/{fin.IV_ATK}/{fin.IV_DEF}/{fin.IV_SPA}/{fin.IV_SPD}/{fin.IV_SPE}\n" +
                    $"**EVs**: {fin.EV_HP}/{fin.EV_ATK}/{fin.EV_DEF}/{fin.EV_SPA}/{fin.EV_SPD}/{fin.EV_SPE}\n" +
                    $"**Movimientos**: \n- {(Move)fin.Move1}\n- {(Move)fin.Move2}\n- {(Move)fin.Move3}\n- {(Move)fin.Move4}\n" +
                    (PokeTradeBotSV.HasMark((IRibbonIndex)fin, out RibbonIndex mark) ? $"\n**Pokémon Mark**: {mark.ToString().Replace("Mark", "")}{Environment.NewLine}" : "");

                string markEntryText = "";
                var index = (int)mark - (int)RibbonIndex.MarkLunchtime;
                if (index > 0)
                    markEntryText = MarkTitle[index];

                var specitem = fin.HeldItem != 0 ? $"{SpeciesName.GetSpeciesNameGeneration(fin.Species, 2, fin.Generation <= 8 ? 8 : 9)}{TradeExtensions<T>.FormOutput(fin.Species, fin.Form, out _) + " (" + ShowdownParsing.GetShowdownText(fin).Split('@', '\n')[1].Trim() + ")"}" : $"{SpeciesName.GetSpeciesNameGeneration(fin.Species, 2, fin.Generation <= 8 ? 8 : 9) + TradeExtensions<T>.FormOutput(fin.Species, fin.Form, out _)}{markEntryText}";

                var msg = "Mostrando tu ";
                var mode = info.Type;
                switch (mode)
                {
                    case PokeTradeType.Specific: msg += "**Trade**!"; break;
                    case PokeTradeType.Clone: msg += "**Clone**!"; break;
                    case PokeTradeType.SupportTrade or PokeTradeType.Giveaway: msg += $"**Trade especial**!"; break;
                    case PokeTradeType.FixOT: msg += $"**Fixed OT**!"; break;
                }

                string TIDFormatted = fin.Generation >= 7 ? $"{fin.TrainerTID7:000000}" : $"{fin.TID16:00000}";
                var footer = new EmbedFooterBuilder { Text = $"Informacion de entrenador: {fin.OT_Name}/{TIDFormatted}" };
                var author = new EmbedAuthorBuilder { Name = $"{Context.User.Username}'s Pokémon" };
                author.IconUrl = ballImg;
                var embed = new EmbedBuilder { Color = fin.IsShiny && fin.ShinyXor == 0 ? Color.Gold : fin.IsShiny ? Color.LighterGrey : Color.Teal, Author = author, Footer = footer, ThumbnailUrl = pokeImg };
                embed.AddField(x =>
                {
                    x.Name = $"{shiny} {specitem}{gender}";
                    x.Value = trademessage;
                    x.IsInline = false;
                });

                Context.Channel.SendMessageAsync(Trader.Username + " ➔ " + msg, embed: embed.Build()).ConfigureAwait(false);
                switch (fin)
                {
                    case PK9: TradeExtensions<PK9>.SVTrade = new(); break;
                    case PA8: TradeExtensions<PA8>.LATrade = new(); break;
                    case PB8: TradeExtensions<PB8>.BDSPTrade = new(); break;
                    case PK8: TradeExtensions<PK8>.SWSHTrade = new(); break;
                }
            }
        }

        public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, string message)
        {
            Trader.SendMessageAsync(message).ConfigureAwait(false);
        }

        public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeSummary message)
        {
            if (message.ExtraInfo is SeedSearchResult r)
            {
                SendNotificationZ3(r);
                return;
            }

            var msg = message.Summary;
            if (message.Details.Count > 0)
                msg += ", " + string.Join(", ", message.Details.Select(z => $"{z.Heading}: {z.Detail}"));
            Trader.SendMessageAsync(msg).ConfigureAwait(false);
        }

        public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result, string message)
        {
            if (result.Species != 0 && (Hub.Config.Discord.ReturnPKMs || info.Type == PokeTradeType.Dump))
                Trader.SendPKMAsync(result, message).ConfigureAwait(false);
        }

        private void SendNotificationZ3(SeedSearchResult r)
        {
            var lines = r.ToString();
            var embed = new EmbedBuilder { Color = Color.LighterGrey };
            embed.AddField(x =>
            {
                x.Name = $"Semilla: {r.Seed:X16}";
                x.Value = lines;
                x.IsInline = false;
            });
            var msg = $"Aqui estan los detalles para: `{r.Seed:X16}`:";
            Trader.SendMessageAsync(msg, embed: embed.Build()).ConfigureAwait(false);
        }

        public static readonly string[] MarkTitle =
{
            " the Peckish"," the Sleepy"," the Dozy"," the Early Riser"," the Cloud Watcher"," the Sodden"," the Thunderstruck"," the Snow Frolicker"," the Shivering"," the Parched"," the Sandswept"," the Mist Drifter",
            " the Chosen One"," the Catch of the Day"," the Curry Connoisseur"," the Sociable"," the Recluse"," the Rowdy"," the Spacey"," the Anxious"," the Giddy"," the Radiant"," the Serene"," the Feisty"," the Daydreamer",
            " the Joyful"," the Furious"," the Beaming"," the Teary-Eyed"," the Chipper"," the Grumpy"," the Scholar"," the Rampaging"," the Opportunist"," the Stern"," the Kindhearted"," the Easily Flustered"," the Driven",
            " the Apathetic"," the Arrogant"," the Reluctant"," the Humble"," the Pompous"," the Lively"," the Worn-Out",
        };
    }
}
