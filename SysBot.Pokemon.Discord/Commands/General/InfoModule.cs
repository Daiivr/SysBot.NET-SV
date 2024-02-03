using Discord;
using Discord.Commands;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

// src: https://github.com/foxbot/patek/blob/master/src/Patek/Modules/InfoModule.cs
// ISC License (ISC)
// Copyright 2017, Christopher F. <foxbot@protonmail.com>
public class InfoModule : ModuleBase<SocketCommandContext>
{
    private const string detail = "Soy un bot de Discord de código abierto impulsado por PKHe X.Core y otro software de código abierto.";
    private const string repo = "https://github.com/kwsch/SysBot.NET";
    private const string daifork = "https://github.com/Daiivr/SysBot.NET";

    [Command("info")]
    [Alias("about", "whoami", "owner")]
    public async Task InfoAsync()
    {
        var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);

        var builder = new EmbedBuilder
        {
            Color = new Color(114, 137, 218),
            Description = detail,
        };

        builder.AddField("Info",
            $"- [Código fuente]({repo})\n" +
            $"- [Codigo Fuente de este Bot]({daifork})\n" +
            $"- {Format.Bold("Propietario")}: {app.Owner} ({app.Owner.Id})\n" +
            $"- {Format.Bold("Biblioteca")}: Discord.Net ({DiscordConfig.Version})\n" +
            $"- {Format.Bold("Tiempo de actividad")}: {GetUptime()}\n" +
            $"- {Format.Bold("Tiempo de ejecución")}: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture} " +
            $"({RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture})\n" +
            $"- {Format.Bold("Tiempo de compilación")}: {GetVersionInfo("SysBot.Base", false)}\n" +
            $"- {Format.Bold("Versión de PKHeX")}: {GetVersionInfo("PKHeX.Core")}\n" +
            $"- {Format.Bold("Versión de AutoLegality")}: {GetVersionInfo("PKHeX.Core.AutoMod")}\n"
        );

        builder.AddField("Estadísticas",
            $"- {Format.Bold("Tamaño")}: {GetHeapSize()}MiB\n" +
            $"- {Format.Bold("Servers")}: {Context.Client.Guilds.Count}\n" +
            $"- {Format.Bold("Canales")}: {Context.Client.Guilds.Sum(g => g.Channels.Count)}\n" +
            $"- {Format.Bold("Usuarios")}: {Context.Client.Guilds.Sum(g => g.MemberCount)}\n" +
            $"- {Format.Bold("\nGracias, [Project Pokémon](https://projectpokemon.org), por hacer públicos los sprites e imágenes de Pokémon utilizados en este bot!")}\n"
        );

        builder.WithThumbnailUrl("https://i.imgur.com/rzwDEDO.png");

        await ReplyAsync("He aquí un poco de informacion sobre mí!", embed: builder.Build()).ConfigureAwait(false);
    }

    private static string GetUptime() => (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss");
    private static string GetHeapSize() => Math.Round(GC.GetTotalMemory(true) / (1024.0 * 1024.0), 2).ToString(CultureInfo.CurrentCulture);

    private static string GetVersionInfo(string assemblyName, bool inclVersion = true)
    {
        const string _default = "Unknown";
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var assembly = Array.Find(assemblies, x => x.GetName().Name == assemblyName);

        var attribute = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attribute is null)
            return _default;

        var info = attribute.InformationalVersion;
        var split = info.Split('+');
        if (split.Length >= 2)
        {
            var version = split[0];
            var revision = split[1];
            if (DateTime.TryParseExact(revision, "yyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var buildTime))
                return (inclVersion ? $"{version} " : "") + $@"{buildTime:yy-MM-dd\.hh\:mm}";
            return inclVersion ? version : _default;
        }
        return _default;
    }
}
