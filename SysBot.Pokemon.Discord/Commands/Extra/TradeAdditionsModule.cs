using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    [Summary("Genera y pone en cola varias adiciones comerciales tontas")]
    public class TradeAdditionsModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;
        private readonly PokeTradeHub<T> Hub = SysCord<T>.Runner.Hub;
        private readonly ExtraCommandUtil<T> Util = new();

        [Command("fixOT")]
        [Alias("fix", "f")]
        [Summary("Corrige OT y Apodo de un Pokémon que se muestra a través de Link Trade si se detecta un anuncio.")]
        [RequireQueueRole(nameof(DiscordManager.RolesFixOT))]
        public async Task FixAdOT()
        {
            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.FixOT, PokeTradeType.FixOT).ConfigureAwait(false);
        }

        [Command("fixOT")]
        [Alias("fix", "f")]
        [Summary("Corrige OT y Apodo de un Pokémon que se muestra a través de Link Trade si se detecta un anuncio.")]
        [RequireQueueRole(nameof(DiscordManager.RolesFixOT))]
        public async Task FixAdOT([Summary("Trade Code")] int code)
        {
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.FixOT, PokeTradeType.FixOT).ConfigureAwait(false);
        }

        [Command("fixOTList")]
        [Alias("fl", "fq")]
        [Summary("Muestra los usuarios de la cola FixOT.")]
        [RequireSudo]
        public async Task GetFixListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.FixOT);
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Operaciones Pendientes";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("Estos son los usuarios que están esperando actualmente:", embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("itemTrade")]
        [Alias("it", "item")]
        [Summary("Hace que el bot te intercambie un Pokémon con el objeto solicitado, o un Ditto si se proporciona la palabra clave de propagación de estadísticas..")]
        [RequireQueueRole(nameof(DiscordManager.RolesSupportTrade))]
        public async Task ItemTrade([Remainder] string item)
        {
            var code = Info.GetRandomTradeCode();
            await ItemTrade(code, item).ConfigureAwait(false);
        }

        [Command("itemTrade")]
        [Alias("it", "item")]
        [Summary("Hace que el robot te intercambie un Pokémon con el objeto solicitado.")]
        [RequireQueueRole(nameof(DiscordManager.RolesSupportTrade))]
        public async Task ItemTrade([Summary("Trade Code")] int code, [Remainder] string item)
        {
            Species species = Info.Hub.Config.Trade.ItemTradeSpecies == Species.None ? Species.Diglett : Info.Hub.Config.Trade.ItemTradeSpecies;
            var set = new ShowdownSet($"{SpeciesName.GetSpeciesNameGeneration((ushort)species, 2, 8)} @ {item.Trim()}");
            var template = AutoLegalityWrapper.GetTemplate(set);
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var pkm = sav.GetLegal(template, out var result);
            pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;
            if (pkm.HeldItem == 0)
            {
                await ReplyAsync($"⚠️ {Context.User.Username}, el item que has solicitado no ha sido reconocido.").ConfigureAwait(false);
                return;
            }

            var la = new LegalityAnalysis(pkm);

            if (pkm is not T pk || !la.Valid)
            {
                var reason = result == "Timeout" ? "El conjunto solicitado tardó demasiado en generarse." : "No fui capaz de crear algo a partir de los datos proporcionados.";
                var imsg = $"⚠️ Oops! {reason} Aquí está mi mejor intento para: **{species}**!";
                await Context.Channel.SendPKMAsync(pkm, imsg).ConfigureAwait(false);
                return;
            }
            pk.ResetPartyStats();

            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.SupportTrade).ConfigureAwait(false);
        }

        [Command("dittoTrade")]
        [Alias("dt", "ditto")]
        [Summary("Hace que el bot te intercambie un Ditto con la extensión de estadísticas y el idioma solicitados.")]
        [RequireQueueRole(nameof(DiscordManager.RolesSupportTrade))]
        public async Task DittoTrade([Summary("Una combinación de \"ATK/SPA/SPE\" o \"6IV\"")] string keyword, [Summary("Language")] string language, [Summary("Nature")] string nature)
        {
            var code = Info.GetRandomTradeCode();
            await DittoTrade(code, keyword, language, nature).ConfigureAwait(false);
        }

        [Command("dittoTrade")]
        [Alias("dt", "ditto")]
        [Summary("Hace que el bot te intercambie un Ditto con la extensión de estadísticas y el idioma solicitados.")]
        [RequireQueueRole(nameof(DiscordManager.RolesSupportTrade))]
        public async Task DittoTrade([Summary("Trade Code")] int code, [Summary("Una combinación de \"ATK/SPA/SPE\" o \"6IV\"")] string keyword, [Summary("Language")] string language, [Summary("Nature")] string nature)
        {
            keyword = keyword.ToLower().Trim();
            if (Enum.TryParse(language, true, out LanguageID lang))
            {
                language = lang.ToString();
            }
            else
            {
                await Context.Message.ReplyAsync($"⚠️ No pude reconocer el idioma solicitado: {language}.").ConfigureAwait(false);
                return;
            }
            nature = nature.Trim()[..1].ToUpper() + nature.Trim()[1..].ToLower();
            var set = new ShowdownSet($"{keyword}(Ditto)\nLanguage: {language}\nNature: {nature}");
            var template = AutoLegalityWrapper.GetTemplate(set);
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var pkm = sav.GetLegal(template, out var result);
            TradeExtensions<T>.DittoTrade((T)pkm);

            var la = new LegalityAnalysis(pkm);

            if (pkm is not T pk || !la.Valid)
            {
                var reason = result == "Timeout" ? "El conjunto solicitado tardó demasiado en generarse." : "No fui capaz de crear algo a partir de los datos proporcionados.";
                var imsg = $"⚠️ Oops! {reason} Aquí está mi mejor intento para ese **Ditto**!";
                await Context.Channel.SendPKMAsync(pkm, imsg).ConfigureAwait(false);
                return;
            }

            pk.ResetPartyStats();
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.SupportTrade).ConfigureAwait(false);
        }
    }
}
