using Discord;
using Discord.Commands;
using PKHeX.Core;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class BotModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        [Command("botStatus")]
        [Summary("Obtiene el estado de los bots.")]
        [RequireSudo]
        public async Task GetStatusAsync()
        {
            var me = SysCord<T>.Runner;
            var bots = me.Bots.Select(z => z.Bot).OfType<PokeRoutineExecutorBase>().ToArray();
            if (bots.Length == 0)
            {
                await ReplyAsync("⚠️ No hay bots configurados.").ConfigureAwait(false);
                return;
            }

            var summaries = bots.Select(GetDetailedSummary);
            var lines = string.Join(Environment.NewLine, summaries);
            await ReplyAsync(Format.Code(lines)).ConfigureAwait(false);
        }

        private static string GetDetailedSummary(PokeRoutineExecutorBase z)
        {
            return $"- {z.Connection.Name} | {z.Connection.Label} - {z.Config.CurrentRoutineType} ~ {z.LastTime:hh:mm:ss} | {z.LastLogged}";
        }

        [Command("botStart")]
        [Summary("Inicia un bot por dirección IP/puerto.")]
        [RequireSudo]
        public async Task StartBotAsync(string ip)
        {
            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"⚠️ Ningún bot tiene esa dirección IP: ({ip}).").ConfigureAwait(false);
                return;
            }

            bot.Start();
            if (SysCordSettings.Settings.EchoOnBotStart)
                await Context.Channel.EchoAndReply($"✔ El bot en **{ip} ({bot.Bot.Connection.Label}** ha recibido la orden de Iniciar.").ConfigureAwait(false);
        }

        [Command("botStop")]
        [Summary("Detiene un bot por dirección IP/puerto.")]
        [RequireSudo]
        public async Task StopBotAsync(string ip)
        {
            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"⚠️ Ningún bot tiene esa dirección IP: ({ip}).").ConfigureAwait(false);
                return;
            }

            bot.Stop();
            if (SysCordSettings.Settings.EchoOnBotStart)
                await Context.Channel.EchoAndReply($"✔ El bot en **{ip} ({bot.Bot.Connection.Label})** ha recibido la orden de Parar.").ConfigureAwait(false);
        }

        [Command("botIdle")]
        [Alias("botPause")]
        [Summary("Ordena a un bot que entre en reposo por dirección IP/puerto.")]
        [RequireSudo]
        public async Task IdleBotAsync(string ip)
        {
            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"⚠️ Ningún bot tiene esa dirección IP: ({ip}).").ConfigureAwait(false);
                return;
            }

            bot.Pause();
            if (SysCordSettings.Settings.EchoOnBotStart)
                await Context.Channel.EchoAndReply($"✔ El bot en {ip} ({bot.Bot.Connection.Label}) ha sido comandado a Idle.").ConfigureAwait(false);
        }

        [Command("botChange")]
        [Summary("Cambia la rutina de un bot (operaciones).")]
        [RequireSudo]
        public async Task ChangeTaskAsync(string ip, [Summary("Routine enum name")] PokeRoutineType task)
        {
            var bot = SysCord<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyAsync($"⚠️ Ningún bot tiene esa dirección IP: ({ip}).").ConfigureAwait(false);
                return;
            }

            bot.Bot.Config.Initialize(task);
            if (SysCordSettings.Settings.EchoOnBotStart)
                await Context.Channel.EchoAndReply($"✔ El bot en **{ip} ({bot.Bot.Connection.Label})** ha recibido la orden de realizar **{task}** como su próxima tarea.").ConfigureAwait(false);
        }

        [Command("botRestart")]
        [Summary("Reinicia el bot(s) por dirección(es) IP, separadas por comas.")]
        [RequireSudo]
        public async Task RestartBotAsync(string ipAddressesCommaSeparated)
        {
            var ips = ipAddressesCommaSeparated.Split(',');
            foreach (var ip in ips)
            {
                var bot = SysCord<T>.Runner.GetBot(ip);
                if (bot == null)
                {
                    await ReplyAsync($"⚠️ Ningún bot tiene esa dirección IP: ({ip}).").ConfigureAwait(false);
                    return;
                }

                var c = bot.Bot.Connection;
                c.Reset();
                bot.Start();
                if (SysCordSettings.Settings.EchoOnBotStart)
                    await Context.Channel.EchoAndReply($"✔ El bot en **{ip} ({c.Label})** ha recibido la orden de Reiniciarse.").ConfigureAwait(false);
            }
        }
    }
}
