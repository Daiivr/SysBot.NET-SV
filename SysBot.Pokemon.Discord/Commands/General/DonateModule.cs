﻿using Discord.Commands;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class DonateModule : ModuleBase<SocketCommandContext>
    {
        [Command("donate")]
        [Alias("donation")]
        [Summary("Muestra el enlace de donación del anfitrión.")]
        public async Task PingAsync()
        {
            var str = $"¡Aquí está el enlace de donación! Gracias por tu apoyo :3 {SysCordSettings.Settings.DonationLink}";
            await ReplyAsync(str).ConfigureAwait(false);
        }
    }
}