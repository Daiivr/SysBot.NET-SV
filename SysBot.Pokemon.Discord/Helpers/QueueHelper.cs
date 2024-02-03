using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using PKHeX.Core;
using System;
using System.Text;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public static class QueueHelper<T> where T : PKM, new()
{
    private const uint MaxTradeCode = 9999_9999;

    public static async Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, SocketUser trader, bool showInServer = true, int catchID = 0)
    {
        if ((uint)code > MaxTradeCode)
        {
            await context.Channel.SendMessageAsync("⚠️ El codigo de tradeo debe ser un numero entre: 00000000-99999999!").ConfigureAwait(false);
            return;
        }

        try
        {
            const string helper = "✓ Te he añadido a la __lista__! Te enviaré un __mensaje__ aquí cuando comience tu operación...";
            IUserMessage test = await trader.SendMessageAsync(helper).ConfigureAwait(false);

            // Try adding
            var result = AddToTradeQueue(context, trade, code, trainer, sig, routine, type, trader, out var msg, catchID);

            // Notify in PM to mirror what is said in the channel.
            await trader.SendMessageAsync($"{msg}\nTu codigo de tradeo sera: **{code:0000 0000}**.").ConfigureAwait(false);

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
            await HandleDiscordExceptionAsync(context, trader, ex).ConfigureAwait(false);
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

        var imgUrl = "https://i.imgur.com/DWLEXyu.png"; // Replace with the URL of the image you want to use for the error embed
        builder.WithThumbnailUrl(imgUrl);

        var embed = builder.Build();
        await channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
    }

    public static Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type)
    {
        return AddToQueueAsync(context, code, trainer, sig, trade, routine, type, context.User);
    }

    private static string AddSpacesBeforeUpperCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new StringBuilder();
        foreach (char c in input)
        {
            if (char.IsUpper(c))
                result.Append(' ');

            result.Append(c);
        }

        return result.ToString().Trim(); // Remove any leading/trailing spaces
    }

    private static async Task SendEmbedMessageAsync(SocketUser user, string type, int detailId, int position, string pokeName, T pk, double eta, ISocketMessageChannel channel)
    {
        var builder = new EmbedBuilder
        {
            Color = pk.IsShiny && pk.ShinyXor == 0 ? Color.Gold : pk.IsShiny ? Color.LighterGrey : Color.Teal,
            Description = $"{user.Mention} ➜ Agregado al **{type}**.",
            Footer = new EmbedFooterBuilder
            {
                Text = $"ID: {detailId} | Posicion actual: {position}" + (eta > 0 ? $"\nTiempo estimado: {eta:F1} minutos" : ""),
                IconUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl()
            }
        };

        // Añade la imagen del tipo de Poké Ball como icono del título del embed
        if (type != "Clone" && type != "Dump")
        {
            var ballImg = $"https://raw.githubusercontent.com/BakaKaito/HomeImages/main/Ballimg/50x50/" + $"{(Ball)pk.Ball}ball".ToLower() + ".png";
            if (!string.IsNullOrWhiteSpace(ballImg))
            {
                builder.WithAuthor("Solicitud de Intercambio", ballImg);
            }
        }

        // Verifica si el Pokémon es un huevo
        if (pk.IsEgg)
        {
            var eggImageUrl = "https://i.imgur.com/vXktZIJ.gif";
            builder.WithThumbnailUrl(eggImageUrl);
            pokeName = "**Recibiendo**: Huevo."; // Asigna "Huevo" como nombre
        }
        else
        {
            if (pk.HeldItem != 0)
            {
                var itemimg = $"https://raw.githubusercontent.com/Daiivr/Pokemon-Scarlet-and-Violet/main/Item%20Icons/50x50/item_{pk.HeldItem}.png";
                builder.WithThumbnailUrl(itemimg);
                var pokeImgUrl = TradeExtensions<T>.PokeImg(pk, false, false);
                builder.WithImageUrl(pokeImgUrl);
            }
            else
            {
                if (type == "Clone" || type == "Dump" || type == "FixOT")
                {
                    var titleicon = $"https://b.thumbs.redditmedia.com/lnvqYS6qJ76fqr9bM2p2JryeEHfyji6dLegH6wnyoeM.png";
                    var pokeImgUrl = TradeExtensions<T>.PokeImg(pk, false, false);
                    builder.WithAuthor("Solicitud de Intercambio", titleicon);
                    // Asigna una imagen de embed diferente según el tipo de comando
                    string embedImgUrl;
                    if (type == "Dump")
                    {
                        embedImgUrl = "https://i.imgur.com/FmSvdvy.png"; // URL para imagen de Dump
                    }
                    else if (type == "Clone")
                    {
                        embedImgUrl = "https://i.imgur.com/acxv2pn.png"; // URL para imagen de Clone
                    }
                    else // FixOT
                    {
                        embedImgUrl = "https://i.imgur.com/FpotPyx.png"; // URL para imagen de FixOT
                    }
                    builder.WithThumbnailUrl(embedImgUrl);
                }
                else
                {
                    var pokeImgUrl = TradeExtensions<T>.PokeImg(pk, false, false);
                    builder.WithThumbnailUrl(pokeImgUrl);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(pokeName))
        {
            pokeName = AddSpacesBeforeUpperCase(pokeName);
            builder.AddField("Informacion Extra:", $"{pokeName}\n**Nivel**: {pk.CurrentLevel}.");
        }

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
            msg = $"✘ Lo siento {user.Mention}, aun estás siendo procesado, por favor espera unos segundos antes de volverlo a intentar.";
            return false;
        }

        var position = Info.CheckPosition(userID, type);

        var ticketID = "";
        if (TradeStartModule<T>.IsStartChannel(context.Channel.Id))
            ticketID = $", ID: **{detail.ID}**";

        var shiny = pk.ShinyXor == 0 ? "<:square:1134580807529398392>" : pk.ShinyXor <= 16 ? "<:shiny:1134580552926777385>" : "";
        var pokeName = "";
        if ((t == PokeTradeType.Specific || t == PokeTradeType.SupportTrade || t == PokeTradeType.Giveaway) && pk.Species != 0)
            pokeName = $"**Recibiendo**: {shiny}{(t == PokeTradeType.SupportTrade && pk.Species != (int)Species.Ditto && pk.HeldItem != 0 ? $"{(Species)pk.Species} ({ShowdownParsing.GetShowdownText(pk).Split('@', '\n')[1].Trim()})" : $"{(Species)pk.Species}")}.";
        msg = $"{user.Mention} ➜ Agregado al **{type}**. ID: **{detail.ID}**. Posicion actual: **{position.Position}**.{AddSpacesBeforeUpperCase(pokeName)}";

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

    private static async Task HandleDiscordExceptionAsync(SocketCommandContext context, SocketUser trader, HttpException ex)
    {
        string message = string.Empty;
        switch (ex.DiscordCode)
        {
            case DiscordErrorCode.InsufficientPermissions or DiscordErrorCode.MissingPermissions:
                {
                    // Check if the exception was raised due to missing "Send Messages" or "Manage Messages" permissions. Nag the bot owner if so.
                    var permissions = context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel);
                    if (!permissions.SendMessages)
                    {
                        // Nag the owner in logs.
                        message = "You must grant me \"Send Messages\" permissions!";
                        Base.LogUtil.LogError(message, "QueueHelper");
                        return;
                    }
                    if (!permissions.ManageMessages)
                    {
                        var app = await context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
                        var owner = app.Owner.Id;
                        message = $"<@{owner}> You must grant me \"Manage Messages\" permissions!";
                    }
                }
                break;
            case DiscordErrorCode.CannotSendMessageToUser:
                {
                    // The user either has DMs turned off, or Discord thinks they do.
                    message = context.User == trader ? "⚠️ Debes habilitar los mensajes privados para poder agregarte en la cola.!" : "⚠️ El usuario mencionado debe habilitar los mensajes privados para que estén en cola!";
                }
                break;
            default:
                {
                    // Send a generic error message.
                    message = ex.DiscordCode != null ? $"Discord error {(int)ex.DiscordCode}: {ex.Reason}" : $"Http error {(int)ex.HttpCode}: {ex.Message}";
                }
                break;
        }
        await context.Channel.SendMessageAsync(message).ConfigureAwait(false);
    }
}
