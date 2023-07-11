using Discord.Commands;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class PingModule : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        [Summary("Hace que el bot responda, indicando que está funcionando.")]
        public async Task PingAsync()
        {
            await ReplyAsync("🏓 Pong!").ConfigureAwait(false);
        }

        [Command("speak")]
        [Alias("talk", "say")]
        [Summary("Indica al bot que hable cuando haya gente en la isla.")]
        [RequireSudo]
        public async Task SpeakAsync([Remainder] string request)
        {
            await ReplyAsync(request).ConfigureAwait(false);
        }
    }
}