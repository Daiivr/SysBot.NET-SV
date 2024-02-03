using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("Pone en cola nuevos intercambios de clones")]
public class CloneModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    [Command("clone")]
    [Alias("c")]
    [Summary("Clona los Pokémon que muestras a través de Link Trade.")]
    [RequireQueueRole(nameof(DiscordManager.RolesClone))]
    public Task CloneAsync(int code)
    {
        var sig = Context.User.GetFavor();
        return QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.Clone, PokeTradeType.Clone);
    }

    [Command("clone")]
    [Alias("c")]
    [Summary("Clona los Pokémon que muestras a través de Link Trade.")]
    [RequireQueueRole(nameof(DiscordManager.RolesClone))]
    public Task CloneAsync([Summary("Trade Code")][Remainder] string code)
    {
        int tradeCode = Util.ToInt32(code);
        var sig = Context.User.GetFavor();
        return QueueHelper<T>.AddToQueueAsync(Context, tradeCode == 0 ? Info.GetRandomTradeCode() : tradeCode, Context.User.Username, sig, new T(), PokeRoutineType.Clone, PokeTradeType.Clone);
    }

    [Command("clone")]
    [Alias("c")]
    [Summary("Clona los Pokémon que muestras a través de Link Trade.")]
    [RequireQueueRole(nameof(DiscordManager.RolesClone))]
    public Task CloneAsync()
    {
        var code = Info.GetRandomTradeCode();
        return CloneAsync(code);
    }

    [Command("cloneList")]
    [Alias("cl", "cq")]
    [Summary("Muestra los usuarios en la cola de clonación.")]
    [RequireSudo]
    public async Task GetListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.Clone);
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
