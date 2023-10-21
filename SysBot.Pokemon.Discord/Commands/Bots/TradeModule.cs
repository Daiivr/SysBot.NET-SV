using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    [Summary("Pone en cola las nuevas operaciones de código de enlace")]
    public class TradeModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

        [Command("tradeList")]
        [Alias("tl")]
        [Summary("Muestra los usuarios en las colas comerciales.")]
        [RequireSudo]
        public async Task GetTradeListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.LinkTrade);
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Operaciones Pendientes.";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("Estos son los usuarios que están esperando actualmente:", embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("trade")]
        [Alias("t")]
        [Summary("Hace que el bot te intercambie el archivo Pokémon proporcionado.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task TradeAsyncAttach([Summary("Trade Code")] int code)
        {
            var sig = Context.User.GetFavor();
            await TradeAsyncAttach(code, sig, Context.User).ConfigureAwait(false);
        }

        [Command("trade")]
        [Alias("t")]
        [Summary("Hace que el bot te intercambie el archivo Pokémon proporcionado.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task TradeAsync([Summary("Trade Code")] int code, [Summary("Showdown Set")][Remainder] string content)
        {
            content = ReusableActions.StripCodeBlock(content);
            var set = new ShowdownSet(content);
            var template = AutoLegalityWrapper.GetTemplate(set);
            if (set.InvalidLines.Count != 0)
            {
                var msg = $"✘ No se puede analizar el conjunto Showdown:\n{string.Join("\n", set.InvalidLines)}";
                await ReplyAsync(msg).ConfigureAwait(false);
                return;
            }

            try
            {
                var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
                var pkm = sav.GetLegal(template, out var result);
                bool pla = typeof(T) == typeof(PA8);

                if (!pla && pkm.Nickname.ToLower() == "egg" && Breeding.CanHatchAsEgg(pkm.Species))
                    TradeExtensions<T>.EggTrade(pkm, template);

                var la = new LegalityAnalysis(pkm);
                var spec = GameInfo.Strings.Species[template.Species];
                pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;
                bool memes = Info.Hub.Config.Trade.Memes && await TradeAdditionsModule<T>.TrollAsync(Context, pkm is not T || !la.Valid, pkm).ConfigureAwait(false);
                if (memes)
                    return;

                if (pkm is not T pk || !la.Valid)
                {
                    var reason = result == "Timeout" ? $"Este **{spec}** tomó demasiado tiempo en generarse." : result == "VersionMismatch" ? "Solicitud denegada: Las versiones de **PKHeX** y **Auto-Legality Mod** no coinciden." : $"{Context.User.Mention} No se puede crear un **{spec}** con los datos proporcionados.";
                    var imsg = $"Oops! {reason}";
                    if (result == "Failed")
                        imsg += $"\n\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";

                    var errorEmbed = new EmbedBuilder
                    {
                        Description = imsg,
                        Color = Color.Red,
                        Footer = new EmbedFooterBuilder
                        {
                            Text = $"{Context.User.Username} • {DateTime.UtcNow:dd-MM-yyyy HH:mm:ss}",
                            IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl()
                        }
                    };

                    // Set the author with an icon
                    errorEmbed.WithAuthor("Error en la Legalidad del Conjunto", "https://img.freepik.com/free-icon/warning_318-478601.jpg");
                    errorEmbed.ImageUrl = "https://i.imgur.com/Y64hLzW.gif"; // Set embed image URL
                    errorEmbed.ThumbnailUrl = "https://i.imgur.com/DWLEXyu.png";

                    // Set any additional properties for the embed if needed
                    // errorEmbed.ThumbnailUrl = "URL_TO_THUMBNAIL_IMAGE"; // Set thumbnail URL
                    // errorEmbed.ImageUrl = "URL_TO_EMBED_IMAGE"; // Set embed image URL
                    // ...

                    await ReplyAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                    return;
                }
                pk.ResetPartyStats();

                var sig = Context.User.GetFavor();
                await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(TradeModule<T>));
                var setLines = string.Join("\n", set.GetSetLines());
                var errorEmbed = new EmbedBuilder
                {
                    Description = $"Oops! Ocurrió un problema inesperado con este Showdown Set:\n```\n{setLines}\n```",
                    Color = Color.Red,
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"{Context.User.Username} • {DateTime.UtcNow:dd-MM-yyyy HH:mm:ss}",
                        IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl()
                    }
                };

                errorEmbed.WithAuthor("Error", "https://img.freepik.com/free-icon/warning_318-478601.jpg");
                errorEmbed.ThumbnailUrl = "https://i.imgur.com/uwx3RYC.png"; // Set thumbnail URL

                await ReplyAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
            }
        }

        [Command("trade")]
        [Alias("t")]
        [Summary("Hace que el robot te cambie un Pokémon convertido del Conjunto de Enfrentamiento proporcionado.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task TradeAsync([Summary("Showdown Set")][Remainder] string content)
        {
            var code = Info.GetRandomTradeCode();
            await TradeAsync(code, content).ConfigureAwait(false);
        }

        [Command("trade")]
        [Alias("t")]
        [Summary("Hace que el bot te intercambie el archivo adjunto.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task TradeAsyncAttach()
        {
            var code = Info.GetRandomTradeCode();
            await TradeAsyncAttach(code).ConfigureAwait(false);
        }

        [Command("banTrade")]
        [Alias("bt")]
        [RequireSudo]
        public async Task BanTradeAsync([Summary("Online ID")] ulong nnid, string comment)
        {
            SysCordSettings.HubConfig.TradeAbuse.BannedIDs.AddIfNew(new[] { GetReference(nnid, comment) });
            await ReplyAsync("Listo.").ConfigureAwait(false);
        }

        private RemoteControlAccess GetReference(ulong id, string comment) => new()
        {
            ID = id,
            Name = id.ToString(),
            Comment = $"Agregado por {Context.User.Username} el {DateTime.Now:yyyy.MM.dd-hh:mm:ss} ({comment})",
        };

        [Command("tradeUser")]
        [Alias("tu", "tradeOther")]
        [Summary("Hace que el bot comercie con el usuario mencionado el archivo adjunto.")]
        [RequireSudo]
        public async Task TradeAsyncAttachUser([Summary("Trade Code")] int code, [Remainder] string _)
        {
            if (Context.Message.MentionedUsers.Count > 1)
            {
                await ReplyAsync("⚠️ Demasiadas menciones. Solo puedes agregar a la lista un usario a la vez.").ConfigureAwait(false);
                return;
            }

            if (Context.Message.MentionedUsers.Count == 0)
            {
                await ReplyAsync("⚠️ Un usuario debe ser mencionado para hacer esto.").ConfigureAwait(false);
                return;
            }

            var usr = Context.Message.MentionedUsers.ElementAt(0);
            var sig = usr.GetFavor();
            await TradeAsyncAttach(code, sig, usr).ConfigureAwait(false);
        }

        [Command("tradeUser")]
        [Alias("tu", "tradeOther")]
        [Summary("Hace que el bot comercie con el usuario mencionado el archivo adjunto.")]
        [RequireSudo]
        public async Task TradeAsyncAttachUser([Remainder] string _)
        {
            var code = Info.GetRandomTradeCode();
            await TradeAsyncAttachUser(code, _).ConfigureAwait(false);
        }

        private async Task TradeAsyncAttach(int code, RequestSignificance sig, SocketUser usr)
        {
            var attachment = Context.Message.Attachments.FirstOrDefault();
            if (attachment == default)
            {
                await ReplyAsync("⚠️ No se proporcionó ningún archivo adjunto!").ConfigureAwait(false);
                return;
            }

            var settings = SysCord<T>.Runner.Hub.Config.Legality;
            var defTrainer = new SimpleTrainerInfo()
            {
                OT = settings.GenerateOT,
                TID16 = settings.GenerateTID16,
                SID16 = settings.GenerateSID16,
                Language = (int)settings.GenerateLanguage,
            };

            var att = await NetUtil.DownloadPKMAsync(attachment, defTrainer).ConfigureAwait(false);
            var pk = GetRequest(att);
            if (pk == null)
            {
                await ReplyAsync("⚠️ El archivo adjunto proporcionado no es compatible con este módulo!").ConfigureAwait(false);
                return;
            }

            await AddTradeToQueueAsync(code, usr.Username, pk, sig, usr).ConfigureAwait(false);
        }

        private static T? GetRequest(Download<PKM> dl)
        {
            if (!dl.Success)
                return null;
            return dl.Data switch
            {
                null => null,
                T pk => pk,
                _ => EntityConverter.ConvertToType(dl.Data, typeof(T), out _) as T,
            };
        }

        private async Task AddTradeToQueueAsync(int code, string trainerName, T pk, RequestSignificance sig, SocketUser usr)
        {
            if (!pk.CanBeTraded())
            {
                // Set the custom icon URL for the embed title
                var customIconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg";
                var customImageUrl = "https://media.tenor.com/vjgjHDFwyOgAAAAM/pysduck-confused.gif"; // Custom image URL for the embed
                var customthumbnail = "https://i.imgur.com/DWLEXyu.png";

                var errorEmbed = new EmbedBuilder
                {
                    Description = $"✘ {usr.Mention} revisa el conjunto enviado, algun dato esta bloqueando el intercambio.\n\n```📝Soluciones:\n• Revisa detenidamente cada detalle del conjunto y vuelve a intentarlo!```",
                    Color = Color.Red,
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"{usr.Username} • {DateTime.UtcNow:dd-MM-yyyy HH:mm:ss} UTC",
                        IconUrl = usr.GetAvatarUrl() ?? usr.GetDefaultAvatarUrl()
                    }
                };

                // Set the custom icon URL for the embed title
                if (!string.IsNullOrWhiteSpace(customIconUrl))
                {
                    errorEmbed.WithAuthor("Error al crear conjunto!", customIconUrl);
                }

                if (!string.IsNullOrWhiteSpace(customImageUrl))
                {
                    errorEmbed.WithImageUrl(customImageUrl); // Set the custom image URL
                }

                if (!string.IsNullOrWhiteSpace(customthumbnail))
                {
                    errorEmbed.WithThumbnailUrl("https://i.imgur.com/DWLEXyu.png"); // Set the custom image URL
                }

                await ReplyAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            var la = new LegalityAnalysis(pk);

            if (!la.Valid && la.Results.Any(m => m.Identifier is CheckIdentifier.Memory))
            {
                var clone = (T)pk.Clone();

                clone.HT_Name = pk.OT_Name;
                clone.HT_Gender = pk.OT_Gender;

                if (clone is PK8 or PA8 or PB8 or PK9)
                    ((dynamic)clone).HT_Language = (byte)pk.Language;

                clone.CurrentHandler = 1;

                la = new LegalityAnalysis(clone);

                if (la.Valid) pk = clone;
            }

            if (!la.Valid)
            {
                var customIconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg"; // Custom icon URL for the embed title
                var customImageUrl = "https://usagif.com/wp-content/uploads/gify/37-pikachu-usagif.gif"; // Custom image URL for the embed
                var customthumbnail = "https://i.imgur.com/DWLEXyu.png";

                string legalityReport = la.Report(verbose: false);

                var errorEmbed = new EmbedBuilder
                {
                    Description = $"✘ {usr.Mention} el archivo **{typeof(T).Name}** no es __legal__ y no puede ser tradeado.\n### He aquí la razón:\n```{legalityReport}```\n```🔊Consejo:\n• Por favor verifica detenidamente la informacion en PKHeX e intentalo de nuevo!\n• Puedes utilizar el plugin de ALM para legalizar tus pokemons y ahorrarte estos problemas.```",
                    Color = Color.Red,
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"{usr.Username} • {DateTime.UtcNow:dd-MM-yyyy HH:mm:ss}",
                        IconUrl = usr.GetAvatarUrl() ?? usr.GetDefaultAvatarUrl()
                    }
                };

                if (!string.IsNullOrWhiteSpace(customIconUrl))
                {
                    errorEmbed.WithAuthor("Error de Legalidad!", customIconUrl);
                }

                if (!string.IsNullOrWhiteSpace(customImageUrl))
                {
                    errorEmbed.WithImageUrl(customImageUrl); // Set the custom image URL
                }

                if (!string.IsNullOrWhiteSpace(customthumbnail))
                {
                    errorEmbed.WithThumbnailUrl(customthumbnail); // Set the custom thumbnail URL
                }

                await ReplyAsync(embed: errorEmbed.Build()).ConfigureAwait(false);
                return;
            }

            await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific, usr).ConfigureAwait(false);
        }
    }
}
