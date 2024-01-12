﻿using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    [Summary("Queues new Seed Check trades")]
    public class SeedCheckModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

        [Command("seedCheck")]
        [Alias("checkMySeed", "checkSeed", "seed", "s", "sc")]
        [Summary("Comprueba la semilla de un Pokémon.")]
        public async Task SeedCheckAsync(int code)
        {
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.SeedCheck, PokeTradeType.Seed).ConfigureAwait(false);
        }

        [Command("seedCheck")]
        [Alias("checkMySeed", "checkSeed", "seed", "s", "sc")]
        [Summary("Comprueba la semilla de un Pokémon.")]
        public async Task SeedCheckAsync([Summary("Trade Code")][Remainder] string code)
        {
            int tradeCode = Util.ToInt32(code);
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, tradeCode == 0 ? Info.GetRandomTradeCode() : tradeCode, Context.User.Username, sig, new T(), PokeRoutineType.SeedCheck, PokeTradeType.Seed).ConfigureAwait(false);
        }

        [Command("seedCheck")]
        [Alias("checkMySeed", "checkSeed", "seed", "s", "sc")]
        [Summary("Comprueba la semilla de un Pokémon.")]
        public async Task SeedCheckAsync()
        {
            var code = Info.GetRandomTradeCode();
            await SeedCheckAsync(code).ConfigureAwait(false);
        }

        [Command("seedList")]
        [Alias("sl", "scq", "seedCheckQueue", "seedQueue", "seedList")]
        [Summary("Muestra los usuarios de la cola Seed Check.")]
        [RequireSudo]
        public async Task GetSeedListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.SeedCheck);
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Operaciones pendientes.";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("Estos son los usuarios que están esperando actualmente:", embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("findFrame")]
        [Alias("ff", "getFrameData")]
        [Summary("Muestra el siguiente fotograma brillante a partir de la semilla proporcionada.")]
        public async Task FindFrameAsync([Remainder] string seedString)
        {
            var me = SysCord<T>.Runner;
            var hub = me.Hub;

            seedString = seedString.ToLower();
            if (seedString.StartsWith("0x"))
                seedString = seedString[2..];

            var seed = Util.GetHexValue64(seedString);

            var r = new SeedSearchResult(Z3SearchResult.Success, seed, -1, hub.Config.SeedCheckSWSH.ResultDisplayMode);
            var msg = r.ToString();

            var embed = new EmbedBuilder { Color = Color.LighterGrey };

            embed.AddField(x =>
            {
                x.Name = $"Semilla: {seed:X16}";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync($"Aquí están los detalles para: `{r.Seed:X16}`:", embed: embed.Build()).ConfigureAwait(false);
        }
    }
}
