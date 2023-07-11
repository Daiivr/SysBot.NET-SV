using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    [Summary("Genera y pone en cola varias adiciones comerciales tontas")]
    public class TradeAdditionsModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;
        private readonly PokeTradeHub<T> Hub = SysCord<T>.Runner.Hub;
        private readonly ExtraCommandUtil<T> Util = new();

        [Command("giveawayqueue")]
        [Alias("gaq")]
        [Summary("Imprime los usuarios en las colas de reparto.")]
        [RequireSudo]
        public async Task GetGiveawayListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.LinkTrade);
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Operaciones Pendientes";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("Estos son los usuarios que están esperando actualmente:", embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("giveawaypool")]
        [Alias("gap", "srl")]
        [Summary("Muestra una lista de Pokémon disponibles para regalar.")]
        [RequireQueueRole(nameof(DiscordManager.RolesGiveaway))]
        public async Task DisplayGiveawayPoolCountAsync()
        {
            var pool = Info.Hub.Ledy.Pool;
            if (pool.Count > 0)
            {
                var test = pool.Files;
                var lines = pool.Files.Select((z, i) => $"{i + 1}: {z.Key} = {(Species)z.Value.RequestInfo.Species}");
                var msg = string.Join("\n", lines);
                await Util.ListUtil(Context, "Tradeos especiales!", msg).ConfigureAwait(false);
            }
            else await ReplyAsync("⚠️ Aun no hay ningun pokemon en la lista para tradeos especiales.").ConfigureAwait(false);
        }

        [Command("giveaway")]
        [Alias("ga", "giveme", "gimme", "sr")]
        [Summary("Hace que el bot te intercambie el Pokémon de regalo especificado.")]
        [RequireQueueRole(nameof(DiscordManager.RolesGiveaway))]
        public async Task GiveawayAsync([Remainder] string content)
        {
            var code = Info.GetRandomTradeCode();
            await GiveawayAsync(code, content).ConfigureAwait(false);
        }

        [Command("giveaway")]
        [Alias("ga", "giveme", "gimme", "sr")]
        [Summary("Hace que el bot te intercambie el Pokémon de regalo especificado.")]
        [RequireQueueRole(nameof(DiscordManager.RolesGiveaway))]
        public async Task GiveawayAsync([Summary("Código del Tradeo")] int code, [Remainder] string content)
        {
            T pk;
            content = ReusableActions.StripCodeBlock(content);
            var pool = Info.Hub.Ledy.Pool;
            if (pool.Count == 0)
            {
                await ReplyAsync("⚠️ Aun no hay ningun pokemon en la lista para tradeos especiales.").ConfigureAwait(false);
                return;
            }
            else if (content.ToLower() == "random") // Request a random giveaway prize.
                pk = Info.Hub.Ledy.Pool.GetRandomSurprise();
            else if (Info.Hub.Ledy.Distribution.TryGetValue(content, out LedyRequest<T>? val) && val is not null)
                pk = val.RequestInfo;
            else
            {
                await ReplyAsync($"⚠️ El pokémon solicitado no esta disponible, usa **\"{Info.Hub.Config.Discord.CommandPrefix}srl\"** para consultar la lista completa de pokemons disponibles.").ConfigureAwait(false);
                return;
            }

            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific).ConfigureAwait(false);
        }

        [Command("fixOT")]
        [Alias("fix", "f")]
        [Summary("Corrige OT y Apodo de un Pokémon que se muestra a través de Link Trade si se detecta un anuncio.")]
        [RequireQueueRole(nameof(DiscordManager.RolesFixOT))]
        public async Task FixAdOT()
        {
            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.FixOT, PokeTradeType.FixOT).ConfigureAwait(false);
        }

        [Command("fixOT")]
        [Alias("fix", "f")]
        [Summary("Corrige OT y Apodo de un Pokémon que se muestra a través de Link Trade si se detecta un anuncio.")]
        [RequireQueueRole(nameof(DiscordManager.RolesFixOT))]
        public async Task FixAdOT([Summary("Trade Code")] int code)
        {
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.FixOT, PokeTradeType.FixOT).ConfigureAwait(false);
        }

        [Command("fixOTList")]
        [Alias("fl", "fq")]
        [Summary("Muestra los usuarios de la cola FixOT.")]
        [RequireSudo]
        public async Task GetFixListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.FixOT);
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Operaciones Pendientes";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("Estos son los usuarios que están esperando actualmente:", embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("itemTrade")]
        [Alias("it", "item")]
        [Summary("Hace que el bot te intercambie un Pokémon con el objeto solicitado, o un Ditto si se proporciona la palabra clave de propagación de estadísticas..")]
        [RequireQueueRole(nameof(DiscordManager.RolesSupportTrade))]
        public async Task ItemTrade([Remainder] string item)
        {
            var code = Info.GetRandomTradeCode();
            await ItemTrade(code, item).ConfigureAwait(false);
        }

        [Command("itemTrade")]
        [Alias("it", "item")]
        [Summary("Hace que el robot te intercambie un Pokémon con el objeto solicitado.")]
        [RequireQueueRole(nameof(DiscordManager.RolesSupportTrade))]
        public async Task ItemTrade([Summary("Trade Code")] int code, [Remainder] string item)
        {
            Species species = Info.Hub.Config.Trade.ItemTradeSpecies == Species.None ? Species.Diglett : Info.Hub.Config.Trade.ItemTradeSpecies;
            var set = new ShowdownSet($"{SpeciesName.GetSpeciesNameGeneration((ushort)species, 2, 8)} @ {item.Trim()}");
            var template = AutoLegalityWrapper.GetTemplate(set);
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var pkm = sav.GetLegal(template, out var result);
            pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;
            if (pkm.HeldItem == 0 && !Info.Hub.Config.Trade.Memes)
            {
                await ReplyAsync($"⚠️ {Context.User.Username}, el item que has solicitado no ha sido reconocido.").ConfigureAwait(false);
                return;
            }

            var la = new LegalityAnalysis(pkm);
            if (Info.Hub.Config.Trade.Memes && await TrollAsync(Context, pkm is not T || !la.Valid, pkm, true).ConfigureAwait(false))
                return;

            if (pkm is not T pk || !la.Valid)
            {
                var reason = result == "Timeout" ? "El conjunto solicitado tardó demasiado en generarse." : "No fui capaz de crear algo a partir de los datos proporcionados.";
                var imsg = $"⚠️ Oops! {reason} Aquí está mi mejor intento para: **{species}**!";
                await Context.Channel.SendPKMAsync(pkm, imsg).ConfigureAwait(false);
                return;
            }
            pk.ResetPartyStats();

            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.SupportTrade).ConfigureAwait(false);
        }

        [Command("dittoTrade")]
        [Alias("dt", "ditto")]
        [Summary("Hace que el bot te intercambie un Ditto con la extensión de estadísticas y el idioma solicitados.")]
        [RequireQueueRole(nameof(DiscordManager.RolesSupportTrade))]
        public async Task DittoTrade([Summary("Una combinación de \"ATK/SPA/SPE\" o \"6IV\"")] string keyword, [Summary("Language")] string language, [Summary("Nature")] string nature)
        {
            var code = Info.GetRandomTradeCode();
            await DittoTrade(code, keyword, language, nature).ConfigureAwait(false);
        }

        [Command("dittoTrade")]
        [Alias("dt", "ditto")]
        [Summary("Hace que el bot te intercambie un Ditto con la extensión de estadísticas y el idioma solicitados.")]
        [RequireQueueRole(nameof(DiscordManager.RolesSupportTrade))]
        public async Task DittoTrade([Summary("Trade Code")] int code, [Summary("Una combinación de \"ATK/SPA/SPE\" o \"6IV\"")] string keyword, [Summary("Language")] string language, [Summary("Nature")] string nature)
        {
            keyword = keyword.ToLower().Trim();
            if (Enum.TryParse(language, true, out LanguageID lang))
                language = lang.ToString();
            else
            {
                await Context.Message.ReplyAsync($"⚠️ No pude reconocer el idioma solicitado: {language}.").ConfigureAwait(false);
                return;
            }
            nature = nature.Trim()[..1].ToUpper() + nature.Trim()[1..].ToLower();
            var set = new ShowdownSet($"{keyword}(Ditto)\nLanguage: {language}\nNature: {nature}");
            var template = AutoLegalityWrapper.GetTemplate(set);
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var pkm = sav.GetLegal(template, out var result);
            TradeExtensions<T>.DittoTrade((T)pkm);

            var la = new LegalityAnalysis(pkm);
            if (Info.Hub.Config.Trade.Memes && await TrollAsync(Context, pkm is not T || !la.Valid, pkm).ConfigureAwait(false))
                return;

            if (pkm is not T pk || !la.Valid)
            {
                var reason = result == "Timeout" ? "El conjunto solicitado tardó demasiado en generarse." : "No fui capaz de crear algo a partir de los datos proporcionados.";
                var imsg = $"⚠️ Oops! {reason} Aquí está mi mejor intento para ese **Ditto**!";
                await Context.Channel.SendPKMAsync(pkm, imsg).ConfigureAwait(false);
                return;
            }

            pk.ResetPartyStats();
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.SupportTrade).ConfigureAwait(false);
        }

        [Command("peek")]
        [Summary("Realiza y envía una captura de pantalla desde el Switch especificado.")]
        [RequireOwner]
        public async Task Peek(string address)
        {
            var source = new CancellationTokenSource();
            var token = source.Token;

            var bot = SysCord<T>.Runner.GetBot(address);
            if (bot == null)
            {
                await ReplyAsync($"⚠️ No se ha encontrado ningún bot con la dirección especificada: ({address}).").ConfigureAwait(false);
                return;
            }

            var c = bot.Bot.Connection;
            var bytes = await c.PixelPeek(token).ConfigureAwait(false) ?? Array.Empty<byte>();
            if (bytes.Length == 1)
            {
                await ReplyAsync($"⚠️ No se pudo tomar una captura de pantalla para el bot en {address}. ¿Está conectado el bot?").ConfigureAwait(false);
                return;
            }
            MemoryStream ms = new(bytes);

            var img = "cap.jpg";
            var embed = new EmbedBuilder { ImageUrl = $"attachment://{img}", Color = Color.Purple }.WithFooter(new EmbedFooterBuilder { Text = $"Imagen capturada del bot en la dirección: {address}." });
            await Context.Channel.SendFileAsync(ms, img, "", false, embed: embed.Build());
        }

        public static async Task<bool> TrollAsync(SocketCommandContext context, bool invalid, PKM pkm, bool itemTrade = false)
        {
            var rng = new Random();
            bool noItem = pkm.HeldItem == 0 && itemTrade;
            var path = Info.Hub.Config.Trade.MemeFileNames.Split(',');
            if (Info.Hub.Config.Trade.MemeFileNames == "" || path.Length == 0)
                path = new string[] { "https://i.imgur.com/qaCwr09.png" }; //If memes enabled but none provided, use a default one.

            if (invalid || !ItemRestrictions.IsHeldItemAllowed(pkm) || noItem || (pkm.Nickname.ToLower() == "egg" && !Breeding.CanHatchAsEgg(pkm.Species)))
            {
                var msg = $"{(noItem ? $"{context.User.Username}, the item you entered wasn't recognized." : $"Oops! I wasn't able to create that {GameInfo.Strings.Species[pkm.Species]}.")} Here's a meme instead!\n";
                await context.Channel.SendMessageAsync($"{(invalid || noItem ? msg : "")}{path[rng.Next(path.Length)]}").ConfigureAwait(false);
                return true;
            }
            return false;
        }

        [Command("repeek")]
        [Summary("Realiza y envía una captura de pantalla desde el Switch especificado.")]
        [RequireOwner]
        public async Task RePeek(string address)
        {
            var source = new CancellationTokenSource();
            var token = source.Token;

            var bot = SysCord<T>.Runner.GetBot(address);
            if (bot == null)
            {
                await ReplyAsync($"⚠️ No se ha encontrado ningún bot con la dirección especificada: ({address}).").ConfigureAwait(false);
                return;
            }

            var c = bot.Bot.Connection;
            c.Reset();
            var bytes = Task.Run(async () => await c.PixelPeek(token).ConfigureAwait(false)).Result ?? Array.Empty<byte>();
            MemoryStream ms = new(bytes);
            var img = "cap.jpg";
            var embed = new EmbedBuilder { ImageUrl = $"attachment://{img}", Color = Color.Purple }.WithFooter(new EmbedFooterBuilder { Text = $"Imagen capturada del bot en la dirección: {address}." });
            await Context.Channel.SendFileAsync(ms, img, "", false, embed: embed.Build());
        }

        [Command("setCatchLimit")]
        [Alias("scl")]
        [Summary("Establece el límite de capturas para las incursiones en SV.")]
        [RequireSudo]
        public async Task SetOffsetIncrement([Summary("Establece el límite de capturas para las incursiones en SV.")] int limit)
        {
            int parse = SysCord<T>.Runner.Hub.Config.RaidSV.CatchLimit = limit;

            var msg = $"✔ {Context.User.Mention} El límite de capturas para las incursiones se ha fijado en **{parse}**.";
            await ReplyAsync(msg).ConfigureAwait(false);
        }

        [Command("clearRaidSVBans")]
        [Alias("crb")]
        [Summary("Borra la lista de baneos de RaidSV.")]
        [RequireSudo]
        public async Task ClearRaidBansSV()
        {
            SysCord<T>.Runner.Hub.Config.RaidSV.RaiderBanList.Clear();
            var msg = "✔ La lista de baneos de RaidSV ha sido borrada.";
            await ReplyAsync(msg).ConfigureAwait(false);
        }

        [Command("addRaidParams")]
        [Alias("arp")]
        [Summary("Añade un nuevo parámetro de incursión.")]
        [RequireSudo]
        public async Task AddNewRaidParam([Summary("Seed")] string seed, [Summary("Species Type")] string species, [Summary("Content Type")] string content)
        {
            int type = int.Parse(content);

            var description = string.Empty;
            var prevpath = "bodyparam.txt";            
            var filepath = "RaidFilesSV\\bodyparam.txt";
            if (File.Exists(prevpath))            
                Directory.Move(filepath, prevpath + Path.GetFileName(filepath));
            
            if (File.Exists(filepath))
                description = File.ReadAllText(filepath);

            var data = string.Empty;
            var prevpk = "pkparam.txt";
            var pkpath = "RaidFilesSV\\pkparam.txt";
            if (File.Exists(prevpk))            
                Directory.Move(pkpath, prevpk + Path.GetFileName(pkpath));
            
            if (File.Exists(pkpath))
                data = File.ReadAllText(pkpath);

            var parse = TradeExtensions<T>.EnumParse<Species>(species);
            if (parse == default)
            {
                await ReplyAsync($"⚠️ {species} no es una Especie válida.").ConfigureAwait(false);
                return;
            }

            RotatingRaidSettingsSV.RotatingRaidParameters newparam = new()
            {
                CrystalType = (TeraCrystalType)type,                
                Description = new[] { description },
                PartyPK = new[] { data },
                Species = parse,
                SpeciesForm = 0,
                Seed = seed,
                IsCoded = true,
                Title = $"{parse} ☆ - {(TeraCrystalType)type}",
            };

            SysCord<T>.Runner.Hub.Config.RotatingRaidSV.RaidEmbedParameters.Add(newparam);
            var msg = $"✔ ¡Se ha añadido una nueva incursión de **{newparam.Species}**!";
            await ReplyAsync(msg).ConfigureAwait(false);
        }

        [Command("removeRaidParams")]
        [Alias("rrp")]
        [Summary("Remueve un parámetro de incursión.")]
        [RequireSudo]
        public async Task RemoveRaidParam([Summary("Seed")] string seed)
        {

            var remove = uint.Parse(seed, NumberStyles.AllowHexSpecifier);
            var list = SysCord<T>.Runner.Hub.Config.RotatingRaidSV.RaidEmbedParameters;
            foreach (var s in list)
            {
                var def = uint.Parse(s.Seed, NumberStyles.AllowHexSpecifier);
                if (def == remove)
                {
                    list.Remove(s);
                    var msg = $"✔ La Incursión de {s.Species} | {s.Seed:X8} ha sido eliminada!";
                    await ReplyAsync(msg).ConfigureAwait(false);
                    return;
                }
            }
        }

        [Command("toggleRaidParams")]
        [Alias("trp")]
        [Summary("Activa/Desactiva el parámetro de incursión.")]
        [RequireSudo]
        public async Task DeactivateRaidParam([Summary("Seed")] string seed)
        {

            var deactivate = uint.Parse(seed, NumberStyles.AllowHexSpecifier);
            var list = SysCord<T>.Runner.Hub.Config.RotatingRaidSV.RaidEmbedParameters;
            foreach (var s in list)
            {
                var def = uint.Parse(s.Seed, NumberStyles.AllowHexSpecifier);
                if (def == deactivate)
                {
                    if (s.ActiveInRotation == true)
                        s.ActiveInRotation = false;
                    else
                        s.ActiveInRotation = true;
                    var m = s.ActiveInRotation == true ? "habilitada" : "desactivada";
                    var msg = $"✔ La Incursión de {s.Species} | {s.Seed:X8} ha sido **{m}**!";
                    await ReplyAsync(msg).ConfigureAwait(false);
                    return;
                }
            }
        }

        [Command("togglecodeRaidParams")]
        [Alias("tcrp")]
        [Summary("Conmuta el parámetro de incursión de código.")]
        [RequireSudo]
        public async Task ToggleCodeRaidParam([Summary("Seed")] string seed)
        {

            var deactivate = uint.Parse(seed, NumberStyles.AllowHexSpecifier);
            var list = SysCord<T>.Runner.Hub.Config.RotatingRaidSV.RaidEmbedParameters;
            foreach (var s in list)
            {
                var def = uint.Parse(s.Seed, NumberStyles.AllowHexSpecifier);
                if (def == deactivate)
                {
                    if (s.IsCoded == true)
                        s.IsCoded = false;
                    else
                        s.IsCoded = true;
                    var m = s.IsCoded == true ? "privada" : "publica";
                    var msg = $"✔ La Incursión de {s.Species} | {s.Seed:X8} es ahora {m}!";
                    await ReplyAsync(msg).ConfigureAwait(false);
                    return;
                }
            }
        }

        [Command("changeRaidParamTitle")]
        [Alias("crpt")]
        [Summary("Añade un nuevo parámetro de incursión.")]
        [RequireSudo]
        public async Task ChangeRaidParamTite([Summary("Seed")] string seed, [Summary("Content Type")] string title)
        {

            var deactivate = uint.Parse(seed, NumberStyles.AllowHexSpecifier);
            var list = SysCord<T>.Runner.Hub.Config.RotatingRaidSV.RaidEmbedParameters;
            foreach (var s in list)
            {
                var def = uint.Parse(s.Seed, NumberStyles.AllowHexSpecifier);
                if (def == deactivate)
                {
                    s.Title = title;
                    var msg = $"✔ El título de incursión para {s.Species} | {s.Seed:X8} ha sido cambiado!";
                    await ReplyAsync(msg).ConfigureAwait(false);
                    return;
                }
            }
        }

        [Command("viewraidList")]
        [Alias("vrl", "rotatinglist")]
        [Summary("Muestra las 20 primeras incursiones de la colección actual.")]
        public async Task GetRaidListAsync()
        {
            var list = SysCord<T>.Runner.Hub.Config.RotatingRaidSV.RaidEmbedParameters.Take(19);
            string msg = string.Empty;
            foreach (var s in list)
            {
                if (s.ActiveInRotation)
                    msg += s.Title + " - " + s.Seed + " - Estado: Activa" + Environment.NewLine;
                else
                    msg += s.Title + " - " + s.Seed + " - Estado: Inactiva" + Environment.NewLine;
            }
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Raid List";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("Estas son las 20 primeras incursiones que figuran actualmente en la lista:", embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("toggleRaidPK")]
        [Alias("trpk")]
        [Summary("Conmuta el parámetro de incursion.")]
        [RequireSudo]
        public async Task ToggleRaidParamPK([Summary("Seed")] string seed, [Summary("Showdown Set")][Remainder] string content)
        {

            var deactivate = uint.Parse(seed, NumberStyles.AllowHexSpecifier);
            var list = SysCord<T>.Runner.Hub.Config.RotatingRaidSV.RaidEmbedParameters;
            foreach (var s in list)
            {
                var def = uint.Parse(s.Seed, NumberStyles.AllowHexSpecifier);
                if (def == deactivate)
                {
                    s.PartyPK = new[] { content };
                    var m = string.Join("\n", s.PartyPK);
                    var msg = $"✔ El pokemon del bot para la raid de {s.Species} | {s.Seed:X8} se ha cqambiado a \n{m}!";
                    await ReplyAsync(msg).ConfigureAwait(false);
                    return;
                }
            }
        }

        [Command("raidhelp")]
        [Alias("rh")]
        [Summary("Muestra la lista de comandos de ayuda de raid.")]
        public async Task GetRaidHelpListAsync()
        {
            var embed = new EmbedBuilder();
            List<string> cmds = new()
            {
                "$crb - Borrar todo en la lista de prohibiciones.\n",
                "$vrl - Muestra todas las incursiones de la lista.\n",
                "$arp - Añadir parámetro a la colección.\nEj: [Command] [Seed] [Species] [Difficulty]\n",
                "$rrp - Eliminar parámetro de la colección.\nEj: [Command] [Seed]\n",
                "$trp - Conmutar el parámetro como Activo/Inactivo en la colección.\nEj: [Command] [Seed]\n",
                "$tcrp - Conmutar el parámetro como Codificado/No codificado en la colección.\nEj: [Command] [Seed]\n",
                "$trpk - Establece un PartyPK para el parámetro a través de un set de showdown.\nEj: [Command] [Seed] [ShowdownSet]\n",
                "$crpt - Establecer el título del parámetro.\nEj: [Command] [Seed]"
            };
            string msg = string.Join("", cmds.ToList());
            embed.AddField(x =>
            {
                x.Name = "Comandos de ayuda para Raid";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("Aquí tienes tu ayuda para la incursión!", embed: embed.Build()).ConfigureAwait(false);
        }
    }
}
