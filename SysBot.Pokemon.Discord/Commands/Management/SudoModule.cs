using Discord;
using Discord.Commands;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class SudoModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        [Command("blacklist")]
        [Summary("Pone en la lista negra a un usuario de Discord mencionado.")]
        [RequireSudo]
        // ReSharper disable once UnusedParameter.Global
        public async Task BlackListUsers([Remainder] string _)
        {
            var users = Context.Message.MentionedUsers;
            var objects = users.Select(GetReference);
            SysCordSettings.Settings.UserBlacklist.AddIfNew(objects);
            await ReplyAsync("✔ Listo.").ConfigureAwait(false);
        }

        [Command("blacklistComment")]
        [Summary("Añade un comentario para un ID de usuario de Discord en la lista negra.")]
        [RequireSudo]
        // ReSharper disable once UnusedParameter.Global
        public async Task BlackListUsers(ulong id, [Remainder] string comment)
        {
            var obj = SysCordSettings.Settings.UserBlacklist.List.Find(z => z.ID == id);
            if (obj is null)
            {
                await ReplyAsync($"⚠️ No se puede encontrar un usuario con ese ID: ({id}).").ConfigureAwait(false);
                return;
            }

            var oldComment = obj.Comment;
            obj.Comment = comment;
            await ReplyAsync($"✔ Listo. Cambiado el comentario existente **({oldComment})** a **({comment})**.").ConfigureAwait(false);
        }

        [Command("unblacklist")]
        [Summary("Elimina un usuario de Discord mencionado de la lista negra.")]
        [RequireSudo]
        // ReSharper disable once UnusedParameter.Global
        public async Task UnBlackListUsers([Remainder] string _)
        {
            var users = Context.Message.MentionedUsers;
            var objects = users.Select(GetReference);
            SysCordSettings.Settings.UserBlacklist.RemoveAll(z => objects.Any(o => o.ID == z.ID));
            await ReplyAsync("✔ Listo.").ConfigureAwait(false);
        }

        [Command("blacklistId")]
        [Summary("Lista negra de IDs de usuarios de Discord. (Útil si el usuario no está en el servidor).")]
        [RequireSudo]
        public async Task BlackListIDs([Summary("Identificadores de discord separados por comas")][Remainder] string content)
        {
            var IDs = GetIDs(content);
            var objects = IDs.Select(GetReference);
            SysCordSettings.Settings.UserBlacklist.AddIfNew(objects);
            await ReplyAsync("✔ Listo.").ConfigureAwait(false);
        }

        [Command("unBlacklistId")]
        [Summary("Elimina IDs de usuarios de Discord de la lista negra. (Útil si el usuario no está en el servidor).")]
        [RequireSudo]
        public async Task UnBlackListIDs([Summary("Identificadores de discord separados por comas")][Remainder] string content)
        {
            var IDs = GetIDs(content);
            SysCordSettings.Settings.UserBlacklist.RemoveAll(z => IDs.Any(o => o == z.ID));
            await ReplyAsync("✔ Listo.").ConfigureAwait(false);
        }

        [Command("blacklistSummary")]
        [Alias("printBlacklist", "blacklistPrint")]
        [Summary("Muestra la lista de usuarios de Discord en la lista negra.")]
        [RequireSudo]
        public async Task PrintBlacklist()
        {
            var lines = SysCordSettings.Settings.UserBlacklist.Summarize();
            var msg = string.Join("\n", lines);
            await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
        }

        [Command("banID")]
        [Summary("Prohíbe las identificaciones de usuario en línea.")]
        [RequireSudo]
        public async Task BanOnlineIDs([Summary("Identificadores en línea separados por comas")][Remainder] string content)
        {
            var IDs = GetIDs(content);
            var objects = IDs.Select(GetReference);

            var me = SysCord<T>.Runner;
            var hub = me.Hub;
            hub.Config.TradeAbuse.BannedIDs.AddIfNew(objects);
            await ReplyAsync("✔ Listo.").ConfigureAwait(false);
        }

        [Command("bannedIDComment")]
        [Summary("Añade un comentario para un ID de usuario en línea prohibido.")]
        [RequireSudo]
        public async Task BanOnlineIDs(ulong id, [Remainder] string comment)
        {
            var me = SysCord<T>.Runner;
            var hub = me.Hub;
            var obj = hub.Config.TradeAbuse.BannedIDs.List.Find(z => z.ID == id);
            if (obj is null)
            {
                await ReplyAsync($"⚠️ No se puede encontrar un usuario con ese ID en línea: ({id}).").ConfigureAwait(false);
                return;
            }

            var oldComment = obj.Comment;
            obj.Comment = comment;
            await ReplyAsync($"✔ Listo. Cambiado el comentario existente **({oldComment})** a **({comment})**.").ConfigureAwait(false);
        }

        [Command("unbanID")]
        [Summary("Prohíbe las identificaciones de usuario en línea.")]
        [RequireSudo]
        public async Task UnBanOnlineIDs([Summary("Identificadores en línea separados por comas")][Remainder] string content)
        {
            var IDs = GetIDs(content);
            var objects = IDs.Select(GetReference);

            var me = SysCord<T>.Runner;
            var hub = me.Hub;
            hub.Config.TradeAbuse.BannedIDs.RemoveAll(z => IDs.Any(o => o == z.ID));
            await ReplyAsync("✔ Listo.").ConfigureAwait(false);
        }

        [Command("bannedIDSummary")]
        [Alias("printBannedID", "bannedIDPrint")]
        [Summary("Muestra la lista de identificaciones en línea prohibidas.")]
        [RequireSudo]
        public async Task PrintBannedOnlineIDs()
        {
            var me = SysCord<T>.Runner;
            var hub = me.Hub;
            var lines = hub.Config.TradeAbuse.BannedIDs.Summarize();
            var msg = string.Join("\n", lines);
            await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
        }

        [Command("forgetUser")]
        [Alias("forget")]
        [Summary("Olvida los usuarios encontrados anteriormente.")]
        [RequireSudo]
        public async Task ForgetPreviousUser([Summary("Identificadores en línea separados por comas")][Remainder] string content)
        {
            var IDs = GetIDs(content);
            var objects = IDs.Select(GetReference);

            foreach (var ID in IDs)
            {
                PokeRoutineExecutorBase.PreviousUsers.RemoveAllNID(ID);
                PokeRoutineExecutorBase.PreviousUsersDistribution.RemoveAllNID(ID);
            }
            await ReplyAsync("✔ Listo.").ConfigureAwait(false);
        }

        [Command("previousUserSummary")]
        [Alias("prevUsers")]
        [Summary("Muestra una lista de los usuarios encontrados anteriormente.")]
        [RequireSudo]
        public async Task PrintPreviousUsers()
        {
            bool found = false;
            var lines = PokeRoutineExecutorBase.PreviousUsers.Summarize();
            if (lines.Any())
            {
                found = true;
                var msg = "Usuarios encontrados anteriormente:\n" + string.Join("\n", lines);
                await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
            }

            lines = PokeRoutineExecutorBase.PreviousUsersDistribution.Summarize();
            if (lines.Any())
            {
                found = true;
                var msg = "Usuarios de la distribución anterior:\n" + string.Join("\n", lines);
                await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
            }
            if (!found)
                await ReplyAsync("⚠️ No se han encontrado usuarios anteriores.").ConfigureAwait(false);
        }

        private RemoteControlAccess GetReference(IUser channel) => new()
        {
            ID = channel.Id,
            Name = channel.Username,
            Comment = $"Agregado por {Context.User.Username} el {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
        };

        private RemoteControlAccess GetReference(ulong id) => new()
        {
            ID = id,
            Name = "Manual",
            Comment = $"Agregado por {Context.User.Username} el {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
        };

        protected static IEnumerable<ulong> GetIDs(string content)
        {
            return content.Split(new[] { ",", ", ", " " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(z => ulong.TryParse(z, out var x) ? x : 0).Where(z => z != 0);
        }
    }
}