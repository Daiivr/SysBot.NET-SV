using Discord.Commands;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class PingModule : ModuleBase<SocketCommandContext>
{
    [Command("ping")]
    [Summary("Hace que el bot responda, indicando que se está ejecutando.")]
    public async Task PingAsync()
    {
        await ReplyAsync("🏓 Pong!").ConfigureAwait(false);
    }
}
