using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("Pone en cola nuevas operaciones de volcado")]
public class DumpModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    [Command("dump")]
    [Alias("d")]
    [Summary("Envia los Pokémon que muestras a través de Link Trade.")]
    [RequireQueueRole(nameof(DiscordManager.RolesDump))]
    public Task DumpAsync(int code)
    {
        var sig = Context.User.GetFavor();
        return QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.Dump, PokeTradeType.Dump);
    }

    [Command("dump")]
    [Alias("d")]
    [Summary("Envia los Pokémon que muestras a través de Link Trade.")]
    [RequireQueueRole(nameof(DiscordManager.RolesDump))]
    public Task DumpAsync([Summary("Trade Code")][Remainder] string code)
    {
        int tradeCode = Util.ToInt32(code);
        var sig = Context.User.GetFavor();
        return QueueHelper<T>.AddToQueueAsync(Context, tradeCode == 0 ? Info.GetRandomTradeCode() : tradeCode, Context.User.Username, sig, new T(), PokeRoutineType.Dump, PokeTradeType.Dump);
    }

    [Command("dump")]
    [Alias("d")]
    [Summary("Envia los Pokémon que muestras a través de Link Trade.")]
    [RequireQueueRole(nameof(DiscordManager.RolesDump))]
    public Task DumpAsync()
    {
        var code = Info.GetRandomTradeCode();
        return DumpAsync(code);
    }

    [Command("dumpList")]
    [Alias("dl", "dq")]
    [Summary("Muestra los usuarios en la cola de volcado.")]
    [RequireSudo]
    public async Task GetListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.Dump);
        var embed = new EmbedBuilder();
        embed.AddField(x =>
        {
            x.Name = "Operaciones pendientes";
            x.Value = msg;
            x.IsInline = false;
        });
        await ReplyAsync("⌛ Estos son los usuarios que están esperando actualmente:", embed: embed.Build()).ConfigureAwait(false);
    }
}
