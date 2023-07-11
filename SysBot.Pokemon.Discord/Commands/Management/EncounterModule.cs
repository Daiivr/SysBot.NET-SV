using Discord.Commands;
using PKHeX.Core;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class EchoModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        [Command("toss")]
        [Alias("yeet")]
        [Summary("Hace que todos los bots que están esperando un visto bueno sigan funcionando.")]
        [RequireSudo]
        public async Task TossAsync(string name = "")
        {
            var bots = SysCord<T>.Runner.Bots.Select(z => z.Bot);
            foreach (var b in bots)
            {
                if (b is not IEncounterBot x)
                    continue;
                if (!b.Connection.Name.Contains(name) && !b.Connection.Label.Contains(name))
                    continue;
                x.Acknowledge();
            }

            await ReplyAsync("Listo.").ConfigureAwait(false);
        }
        [Command("continue")]
        [Summary("Hace que todos los Bots que están esperando un visto bueno sigan funcionando.")]
        [RequireSudo]
        public async Task ContinueAsync(string name = "")
        {
            var bots = SysCord<T>.Runner.Bots.Select(z => z.Bot);
            foreach (var b in bots)
            {
                if (b is not IArceusBot x)
                    continue;
                if (!b.Connection.Name.Contains(name) && !b.Connection.Label.Contains(name))
                    continue;
                x.AcknowledgeConfirmation();
            }

            await ReplyAsync("Continuando.").ConfigureAwait(false);
        }
    }
}