using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using PKHeX.Core;
using System;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public static class QueueHelper<T> where T : PKM, new()
    {
        private const uint MaxTradeCode = 9999_9999;

        public static async Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, SocketUser trader, bool showInServer = true, int catchID = 0)
        {
            if ((uint)code > MaxTradeCode)
            {
                await context.Channel.SendMessageAsync("El codigo de tradeo debe ser un numero entre: 00000000-99999999!").ConfigureAwait(false);
                return;
            }

            IUserMessage test;
            try
            {
                const string helper = "✓ Te he añadido a la __lista__! Te enviaré un __mensaje__ aquí cuando comience tu operación...";
                test = await trader.SendMessageAsync(helper).ConfigureAwait(false);
            }
            catch (HttpException ex)
            {
                await context.Channel.SendMessageAsync($"{ex.HttpCode}: {ex.Reason}!").ConfigureAwait(false);
                var noAccessMsg = context.User == trader ? "✘ Debes __habilitar__ los mensajes privados para poder __intercambiar__ con el bot!" : "El usuario mencionado debe __habilitar__ los mensajes privados para poder tradear!";
                await context.Channel.SendMessageAsync(noAccessMsg).ConfigureAwait(false);
                return;
            }

            // Try adding
            var result = AddToTradeQueue(context, trade, code, trainer, sig, routine, type, trader, out var msg, catchID);

            // Notify in PM
            await trader.SendMessageAsync($"{msg}\nTu codigo de tradeo sera: **{code:0000 0000}**.").ConfigureAwait(false);

            try
            {
                // Clean Up
                if (result)
                {
                    // Delete the user's join message for privacy
                    if (!context.IsPrivate)
                        await context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
                }
                else
                {
                    // Delete our "I'm adding you!", and send the same message that we sent to the general channel.
                    await test.DeleteAsync().ConfigureAwait(false);

                    // Display the error message as an embed in the server channel
                    if (showInServer)
                    {
                        await SendErrorEmbedAsync(context.Channel, msg).ConfigureAwait(false);
                    }
                    else
                    {
                        // Send the regular text error message to the server channel if showInServer is set to false
                        await context.Channel.SendMessageAsync(msg).ConfigureAwait(false);
                    }
                }
            }
            catch (HttpException ex)
            {
                string message;
                // Check if the exception was raised due to missing "Manage Messages" permissions. Ping the bot host if so.
                var permissions = context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel);
                if (!permissions.ManageMessages)
                {
                    var app = await context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
                    var owner = app.Owner.Id;
                    message = $"<@{owner}> necesitas darme los permisos: \"Manage Messages\"!";
                }
                else
                {
                    // Send a generic error message if we're not missing "Manage Messages" permissions.
                    message = $"{ex.HttpCode}: {ex.Reason}!";
                }
                await context.Channel.SendMessageAsync(message).ConfigureAwait(false);
            }
        }

        private static async Task SendErrorEmbedAsync(ISocketMessageChannel channel, string errorMessage)
        {
            var currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); // Get the current time in the desired format

            var builder = new EmbedBuilder
            {
                Color = Color.Red, // Customize the color of the error embed
                Description = errorMessage,
                Footer = new EmbedFooterBuilder
                {
                    Text = $"Hora del Error: {currentTime}" // Add the current time to the footer
                }
            };

            // Add an icon for the embed title
            var iconUrl = "https://img.freepik.com/free-icon/warning_318-478601.jpg"; // Replace with the URL of the icon you want to use for error embeds
            builder.WithAuthor("Error", iconUrl);

            // Add a regular image to the embed
            var imageUrl = "https://c.tenor.com/rDzirQgBPwcAAAAd/tenor.gif"; // Replace with the URL of the image you want to use for the error embed
            builder.WithImageUrl(imageUrl);

            var embed = builder.Build();
            await channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
        }

        public static async Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, int catchID = 0)
        {
            await AddToQueueAsync(context, code, trainer, sig, trade, routine, type, context.User, catchID: catchID).ConfigureAwait(false);
        }

        private static async Task SendEmbedMessageAsync(SocketUser user, string type, int detailId, int position, string pokeName, T pk, double eta, ISocketMessageChannel channel)
        {
            var builder = new EmbedBuilder
            {
                Color = Color.Green, // Customize the color of the embed
                Description = $"{user.Mention} ➜ Agregado al {type}.",
                Footer = new EmbedFooterBuilder
                {
                    Text = $"ID: {detailId} | Posicion actual: {position}" + (eta > 0 ? $"\nTiempo estimado: {eta:F1} minutos" : ""),
                    IconUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl() // Set the user's icon as the footer icon
                }
            };

            if (!string.IsNullOrWhiteSpace(pokeName))
            {
                builder.AddField("Informacion Extra:", $"{pokeName}\n**Nivel**: {pk.CurrentLevel}");
            }

            // Check if the type is "clone" or "dump"
            if (type == "Clone" || type == "Dump")
            {
                // Display the Pokémon image as the thumbnail and remove it from the embed image
                var pokeImgUrl = TradeExtensions<T>.PokeImg(pk, false, false);
                builder.WithThumbnailUrl(pokeImgUrl);
            }
            else
            {
                // Display the Poké Ball image as the thumbnail if available
                var pokeImgUrlForEmbed = TradeExtensions<T>.PokeImg(pk, false, false);
                var ballImg = $"https://raw.githubusercontent.com/BakaKaito/HomeImages/main/Ballimg/50x50/" + $"{(Ball)pk.Ball}ball".ToLower() + ".png";
                if (!string.IsNullOrWhiteSpace(ballImg))
                {
                    builder.WithThumbnailUrl(ballImg);
                    builder.WithImageUrl(pokeImgUrlForEmbed);
                }
                else
                {
                    // Display the Pokémon image as the thumbnail
                    var pokeImgUrl = TradeExtensions<T>.PokeImg(pk, false, false);
                    builder.WithThumbnailUrl(pokeImgUrl);
                }
            }

            // Add an icon for the embed title
            var iconUrl = "https://b.thumbs.redditmedia.com/lnvqYS6qJ76fqr9bM2p2JryeEHfyji6dLegH6wnyoeM.png"; // Replace with the URL of the icon you want to use
            builder.WithAuthor("Solicitud de Intercambio", iconUrl);

            var embed = builder.Build();
            await channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
        }


        private static bool AddToTradeQueue(SocketCommandContext context, T pk, int code, string trainerName, RequestSignificance sig, PokeRoutineType type, PokeTradeType t, SocketUser trader, out string msg, int catchID = 0)
        {
            var user = trader;
            var userID = user.Id;
            var name = user.Username;

            var trainer = new PokeTradeTrainerInfo(trainerName, userID);
            var notifier = new DiscordTradeNotifier<T>(pk, trainer, code, user, context);
            var detail = new PokeTradeDetail<T>(pk, trainer, notifier, t, code, sig == RequestSignificance.Favored);
            var trade = new TradeEntry<T>(detail, userID, type, name);

            var hub = SysCord<T>.Runner.Hub;
            var Info = hub.Queues.Info;
            var added = Info.AddToTradeQueue(trade, userID, sig == RequestSignificance.Owner);

            if (added == QueueResultAdd.AlreadyInQueue)
            {
                msg = $"✘ Lo siento {user.Mention}, aun estás siendo procesado, Por favor espera unos segundos antes de volverlo a intentar.";
                return false;
            }

            var position = Info.CheckPosition(userID, type);

            var ticketID = "";
            if (TradeStartModule<T>.IsStartChannel(context.Channel.Id))
                ticketID = $", ID: **{detail.ID}**";

            var pokeName = "";
            if ((t == PokeTradeType.Specific || t == PokeTradeType.SupportTrade || t == PokeTradeType.Giveaway) && pk.Species != 0)
                pokeName = $" **Recibiendo**: {(t == PokeTradeType.SupportTrade && pk.Species != (int)Species.Ditto && pk.HeldItem != 0 ? $"{(Species)pk.Species} ({ShowdownParsing.GetShowdownText(pk).Split('@', '\n')[1].Trim()})" : $"{(Species)pk.Species}")}.";
            msg = $"{user.Mention} ➜ Agregado al **{type}**. ID: **{detail.ID}**. Posicion actual: **{position.Position}**.{pokeName}";

            // Retrieve the bot count from the Info object
            var botct = Info.Hub.Bots.Count;

            double eta = 0;
            if (position.Position > botct)
            {
                eta = Info.Hub.Config.Queues.EstimateDelay(position.Position, botct);
                msg += $" Tiempo estimado: **{eta:F1}** minutos";
            }

            // Send the message as an embed with the requested Pokémon image as the thumbnail
            SendEmbedMessageAsync(user, type.ToString(), detail.ID, position.Position, pokeName, pk, eta, context.Channel).Wait();

            return true;
        }
    }
}