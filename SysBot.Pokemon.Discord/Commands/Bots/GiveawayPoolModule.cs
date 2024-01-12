using Discord;
using Discord.Commands;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord.Commands.Extra
{
    [Summary("Distribution Pool Module")]
    public class GiveawayPoolModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;
        private readonly PokeTradeHub<T> Hub = SysCord<T>.Runner.Hub;
        private string? _lastInitialLetter; // Keep this class-level field

        private string GetPokemonInitialLetter(T pokemon)
        {
            return pokemon.FileName[0].ToString().ToUpper(); // Assuming the Pokémon's name is accessible via a Name property
        }

        [Command("giveaway")]
        [Alias("ga", "giveme", "gimme", "sr")]
        [Summary("Makes the bot trade you the specified giveaway Pokémon.")]
        [RequireQueueRole(nameof(DiscordManager.RolesGiveaway))]
        public async Task GiveawayAsync([Remainder] string content)
        {
            var code = Info.GetRandomTradeCode();
            content = ReusableActions.StripCodeBlock(content);
            string normalizedContent = content.ToLowerInvariant().Replace(" ", "").Replace("-", "");

            T pk;
            var giveawaypool = Info.Hub.LedyPlus.GiveawayPool;
            if (giveawaypool.Count == 0)
            {
                var giveawayMsg = ("La lista de treadeos especiales esta vacia.");
                var embedGiveawayMsg = new EmbedBuilder()
                {
                    Author = new EmbedAuthorBuilder()
                    {
                        Name = Context.User.Username,
                        IconUrl = Context.User.GetAvatarUrl()
                    },
                    Color = Color.Blue
                }
                .WithDescription(giveawayMsg)
                .WithThumbnailUrl("https://i.imgur.com/3D36Tyc.png")
                .WithCurrentTimestamp()
                .Build();
                await ReplyAsync(null, false, embedGiveawayMsg).ConfigureAwait(false);
                return;
            }
            else if (normalizedContent == "random")
            {
                pk = Info.Hub.LedyPlus.GiveawayPool.GetRandomSurprise();
            }
            else if (Info.Hub.LedyPlus.Giveaway.TryGetValue(normalizedContent, out LedyRequest<T>? val) && val is not null)
            {
                pk = val.RequestInfo;
            }
            else
            {
                var notAvailableMsg = string.Format(SysCord<T>.Runner.Config.Discord.CustomGiveawayNotAvailableMessage, Info.Hub.Config.Discord.CommandPrefix);

                var embedNotAvailableMsg = new EmbedBuilder()
                {
                    Author = new EmbedAuthorBuilder()
                    {
                        Name = Context.User.Username,
                        IconUrl = Context.User.GetAvatarUrl()
                    },
                    Color = Color.Blue
                }
                .WithDescription(notAvailableMsg)
                .WithThumbnailUrl("https://i.imgur.com/3D36Tyc.png")
                .WithCurrentTimestamp()
                .Build();
                await ReplyAsync(null, false, embedNotAvailableMsg).ConfigureAwait(false);

                return;
            }

            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific).ConfigureAwait(false);
        }

        [Command("giveawaypool")]
        [Alias("gap", "srl")]
        [Summary("Show a list of Pokémon available for giveaway.")]
        [RequireQueueRole(nameof(DiscordManager.RolesGiveaway))]
        public async Task DisplayGiveawayPoolCountAsync()
        {
            await Context.Message.DeleteAsync().ConfigureAwait(false);
            var giveawaypool = Info.Hub.LedyPlus.GiveawayPool;
            if (giveawaypool.Count > 0)
            {
                var lines = giveawaypool.Files.Select((z, i) => $"{i + 1}: **{z.Key.ToTitleCase().Replace(" ", "").Replace("-", "")}**");
                var msg = string.Join("\n", lines);

                // Split msg into a List<string> based on max page length
                int maxPageLength = 350;
                List<string> pageContent = SplitIntoPages(msg, maxPageLength);

                await ExtraCommandUtil<T>.ListUtil(Context, "Lista de tradeos especiales.", pageContent).ConfigureAwait(false);
            }
            else
            {
                var giveawayPoolMsg = ("La lista de treadeos especiales esta vacia.");
                var embedGiveawayPoolMsg = new EmbedBuilder()
                {
                    Author = new EmbedAuthorBuilder()
                    {
                        Name = Context.User.Username,
                        IconUrl = Context.User.GetAvatarUrl()
                    },
                    Color = Color.Blue
                }
                .WithDescription(giveawayPoolMsg)
                .WithThumbnailUrl("https://i.imgur.com/3D36Tyc.png")
                .WithCurrentTimestamp()
                .Build();
                await ReplyAsync(null, false, embedGiveawayPoolMsg).ConfigureAwait(false);

            }
        }

        [Command("giveawaypoolReload")]
        [Alias("gapr")]
        [Summary("Reloads the bot pool from the setting's folder.")]
        [RequireQueueRole(nameof(DiscordManager.RolesGiveaway))]
        public async Task ReloadGiveawayPoolAsync()
        {
            await Context.Message.DeleteAsync().ConfigureAwait(false);
            var me = SysCord<T>.Runner;
            var hub = me.Hub;
            var failedtoReload = $"Error al recargar la carpeta Giveaway.";
            var embedFailedtoReload = new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = Context.User.Username,
                    IconUrl = Context.User.GetAvatarUrl()
                },
                Color = Color.Blue
            }
            .WithDescription(failedtoReload)
            .WithThumbnailUrl("https://i.imgur.com/3D36Tyc.png")
                .WithCurrentTimestamp()
                .Build();
            var reloadedMsg = $"Carpeta de regalos recargada. Recuento: **{hub.LedyPlus.GiveawayPool.Count}**";
            var embedReloadedMsg = new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = Context.User.Username,
                    IconUrl = Context.User.GetAvatarUrl()
                },
                Color = Color.Blue
            }
            .WithDescription(reloadedMsg)
            .WithThumbnailUrl("https://i.imgur.com/MOXSDFV.png")
                .WithCurrentTimestamp()
                .Build();
            var pool = hub.LedyPlus.GiveawayPool.Reload(hub.Config.Folder.GiveawayFolder);
            if (!pool)
                await ReplyAsync(null, false, embedFailedtoReload).ConfigureAwait(false);
            else
                await ReplyAsync(null, false, embedReloadedMsg).ConfigureAwait(false);
        }

        [Command("giveawaypoolstats")]
        [Alias("gapstats")]
        [Summary("Displays the details of Pokémon files in the random pool.")]
        public async Task DisplayTotalGiveawayFilesCount()
        {
            await Context.Message.DeleteAsync().ConfigureAwait(false);
            var me = SysCord<T>.Runner;
            var hub = me.Hub;
            var giveawaypool = hub.LedyPlus.GiveawayPool;
            var count = giveawaypool.Count;
            if (count is > 0 and < 20)
            {
                var lines = giveawaypool.Files.Select((z, i) => $"{i + 1:00}: {z.Key} = {(Species)z.Value.RequestInfo.Species}");
                var msg = string.Join("\n", lines);

                var embed = new EmbedBuilder();
                embed.AddField(x =>
                {
                    x.Name = $"Recuento: **{count}**";
                    x.Value = msg;
                    x.IsInline = false;
                });
                await ReplyAsync("Detalles", embed: embed.Build()).ConfigureAwait(false);
            }
            else
            {
                var poolCountDetails = $"Detalles";
                var embedPoolCountDetails = new EmbedBuilder()
                {
                    Author = new EmbedAuthorBuilder()
                    {
                        Name = Context.User.Username,
                        IconUrl = Context.User.GetAvatarUrl()
                    },
                    Fields = new List<EmbedFieldBuilder>()
                    {
                        new EmbedFieldBuilder()
                        {
                            Name = poolCountDetails,
                            Value = $"Recuento: **{count}**",
                            IsInline = false
                        }
                    },
                    Color = Color.Blue
                }
                .WithThumbnailUrl("https://i.imgur.com/MOXSDFV.png")
                .WithCurrentTimestamp()
                .Build();
                await ReplyAsync(null, false, embedPoolCountDetails).ConfigureAwait(false);
            }
        }

        [Command("giveawayqueue")]
        [Alias("gaq")]
        [Summary("Prints the users in the giveway queues.")]
        [RequireSudo]
        public async Task GetGiveawayListAsync()
        {
            await Context.Message.DeleteAsync().ConfigureAwait(false);
            string msg = Info.GetTradeList(PokeRoutineType.LinkTrade);
            var giveawayQueue = new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = Context.User.Username,
                    IconUrl = Context.User.GetAvatarUrl()
                },
                Fields = new List<EmbedFieldBuilder>()
                {
                    new EmbedFieldBuilder()
                    {
                        Name = "**Cola de tradeos especiales**",
                        Value = msg,
                        IsInline = false
                    }
                },
                Color = Color.Blue
            }
            .WithThumbnailUrl("https://i.imgur.com/MOXSDFV.png")
            .WithCurrentTimestamp()
            .Build();
            await ReplyAsync(null, false, giveawayQueue).ConfigureAwait(false);


        }

        [Command("random")]
        [Alias("rand", "surprise", "surpriseme")]
        [Summary("Gives a random Pokémon from the giveaway pool.")]
        [RequireQueueRole(nameof(DiscordManager.RolesGiveaway))]
        public async Task RandomPokemonAsync()
        {

            var giveawaypool = Info.Hub.LedyPlus.GiveawayPool;
            if (giveawaypool.Count == 0)
            {
                var emptyGiveawayPool = ("La lista de treadeos especiales esta vacia.");
                var embedEmptyGiveawayPool = new EmbedBuilder()
                {
                    Author = new EmbedAuthorBuilder()
                    {
                        Name = Context.User.Username,
                        IconUrl = Context.User.GetAvatarUrl()
                    },
                    Color = Color.Blue
                }
                .WithDescription(emptyGiveawayPool)
                    .WithThumbnailUrl("https://i.imgur.com/3D36Tyc.png")
                    .WithCurrentTimestamp()
                    .Build();
                await ReplyAsync(null, false, embedEmptyGiveawayPool).ConfigureAwait(false);

                return;
            }

            T pk;
            List<T> filteredPool = giveawaypool.Where(p => GetPokemonInitialLetter(p) != _lastInitialLetter).ToList();

            if (filteredPool.Count == 0) // fallback to the complete pool if the filtered list is empty
            {
                filteredPool = giveawaypool;
            }

            var randomIndex = new Random().Next(filteredPool.Count);
            pk = filteredPool[randomIndex];

            _lastInitialLetter = GetPokemonInitialLetter(pk); // Update the last initial letter

            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific).ConfigureAwait(false);
        }

        [Command("page")]
        [Summary("Displays a specific page of Pokémon available for giveaway.")]
        [RequireQueueRole(nameof(DiscordManager.RolesGiveaway))]
        public async Task DisplayGiveawayPoolPageAsync(int pageNumber)
        {
            await Context.Message.DeleteAsync().ConfigureAwait(false);
            var giveawaypool = Info.Hub.LedyPlus.GiveawayPool;
            const int itemsPerPage = 16; // Number of items per page

            int skipItems = (pageNumber - 1) * itemsPerPage;
            int totalNumberOfPages = (int)Math.Ceiling((double)giveawaypool.Count / itemsPerPage);

            if (giveawaypool.Count > skipItems)
            {
                var pageItems = giveawaypool.Files
                    .Skip(skipItems)
                    .Take(itemsPerPage)
                    .Select((z, i) => $"{skipItems + i + 1}: **{z.Key.ToTitleCase().Replace(" ", "").Replace("-", "")}**");

                var msg = string.Join("\n", pageItems);

                // Split msg into a List<string> based on max page length
                int maxPageLength = 500;
                List<string> pageContent = SplitIntoPages(msg, maxPageLength);

                await ExtraCommandUtil<T>.ListUtil(Context, $"Detalles de tradeos especiales - Pagina {pageNumber} de {totalNumberOfPages}", pageContent).ConfigureAwait(false);
            }
            else
            {
                Embed embed = new EmbedBuilder()
                { Author = new EmbedAuthorBuilder() { Name = Context.User.Username, IconUrl = Context.User.GetAvatarUrl() } }

                   .WithColor(Color.Blue)
                   .WithDescription($"La pagina {pageNumber} no existe.")
                   .WithThumbnailUrl("https://i.imgur.com/3D36Tyc.png")
                   .WithCurrentTimestamp()
                   .Build();

                await ReplyAsync(null, false, embed).ConfigureAwait(false);
            }
        }

        private List<string> SplitIntoPages(string text, int maxPageLength)
        {
            List<string> pages = new List<string>();
            while (text.Length > 0)
            {
                int length = text.Length > maxPageLength ? maxPageLength : text.Length;
                string page = text.Substring(0, length);
                int lastNewLine = page.LastIndexOf('\n');
                if (lastNewLine > 0 && length == maxPageLength) // Ensure we don't cut off in the middle of a line
                {
                    page = page.Substring(0, lastNewLine);
                }

                pages.Add(page);
                text = text.Substring(page.Length);
            }
            return pages;
        }

    }
}