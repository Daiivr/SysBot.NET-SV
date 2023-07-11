﻿using Discord.Commands;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class LegalizerModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        [Command("legalize"), Alias("alm")]
        [Summary("Intenta legalizar los datos pkm adjuntos.")]
        public async Task LegalizeAsync()
        {
            var attachments = Context.Message.Attachments;
            foreach (var att in attachments)
                await Context.Channel.ReplyWithLegalizedSetAsync(att).ConfigureAwait(false);
        }

        [Command("convert"), Alias("showdown")]
        [Summary("Intenta convertir el Showdown Set en datos pkm.")]
        [Priority(1)]
        public async Task ConvertShowdown([Summary("Generation/Format")] int gen, [Remainder][Summary("Showdown Set")] string content)
        {
            await Context.Channel.ReplyWithLegalizedSetAsync(content, gen).ConfigureAwait(false);
        }

        [Command("convert"), Alias("showdown")]
        [Summary("Intenta convertir el Showdown Set en datos pkm.")]
        [Priority(0)]
        public async Task ConvertShowdown([Remainder][Summary("Showdown Set")] string content)
        {
            await Context.Channel.ReplyWithLegalizedSetAsync<T>(content).ConfigureAwait(false);
        }
    }
}