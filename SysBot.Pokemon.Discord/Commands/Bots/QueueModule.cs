using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("Borra y alterna las funciones de la cola.")]
public class QueueModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    [Command("queueStatus")]
    [Alias("qs", "ts")]
    [Summary("Comprueba la posición del usuario en la cola.")]
    public async Task GetTradePositionAsync()
    {
        var msg = Context.User.Mention + " - " + Info.GetPositionString(Context.User.Id);
        await ReplyAsync(msg).ConfigureAwait(false);
    }

    [Command("queueClear")]
    [Alias("qc", "tc")]
    [Summary("Borra al usuario de las colas de tradeo. No eliminará a un usuario si está siendo procesado.")]
    public async Task ClearTradeAsync()
    {
        string msg = ClearTrade();
        await ReplyAsync(msg).ConfigureAwait(false);
    }

    [Command("queueClearUser")]
    [Alias("qcu", "tcu")]
    [Summary("Borra al usuario de las colas de tradeo. No eliminará a un usuario si está siendo procesado.")]
    [RequireSudo]
    public async Task ClearTradeUserAsync([Summary("ID de usuario de discord")] ulong id)
    {
        string msg = ClearTrade(id);
        await ReplyAsync(msg).ConfigureAwait(false);
    }

    [Command("queueClearUser")]
    [Alias("qcu", "tcu")]
    [Summary("Borra al usuario de las colas de tradeo. No eliminará a un usuario si está siendo procesado.")]
    [RequireSudo]
    public async Task ClearTradeUserAsync([Summary("Nombre de usuario de la persona a borrar")] string _)
    {
        foreach (var user in Context.Message.MentionedUsers)
        {
            string msg = ClearTrade(user.Id);
            await ReplyAsync(msg).ConfigureAwait(false);
        }
    }

    [Command("queueClearUser")]
    [Alias("qcu", "tcu")]
    [Summary("Borra al usuario de las colas de tradeo. No eliminará a un usuario si está siendo procesado.")]
    [RequireSudo]
    public async Task ClearTradeUserAsync()
    {
        var users = Context.Message.MentionedUsers;
        if (users.Count == 0)
        {
            await ReplyAsync("⚠️ Ningún usuario fue mencionado").ConfigureAwait(false);
            return;
        }
        foreach (var u in users)
            await ClearTradeUserAsync(u.Id).ConfigureAwait(false);
    }

    [Command("queueClearAll")]
    [Alias("qca", "tca")]
    [Summary("Borra a todos los usuarios de las colas de tradeo.")]
    [RequireSudo]
    public async Task ClearAllTradesAsync()
    {
        Info.ClearAllQueues();
        await ReplyAsync("✔ Borrado todo en la cola de espera.").ConfigureAwait(false);
    }

    [Command("queueToggle")]
    [Alias("qt", "tt")]
    [Summary("Activa/desactiva la posibilidad de unirse a la cola comercial.")]
    [RequireSudo]
    public Task ToggleQueueTradeAsync()
    {
        var state = Info.ToggleQueue();
        var msg = state
            ? "✓ **Configuración de cola modificada**: Los usuarios ahora __pueden unirse__ a la **cola**."
            : "⚠️ **Configuración de cola modificada**: Los usuarios __**NO PUEDEN**__ unirse a la `cola` hasta que se vuelva a `habilitar`.";

        return Context.Channel.EchoAndReply(msg);
    }

    [Command("queueMode")]
    [Alias("qm")]
    [Summary("Cambia la forma en que se controlan las colas (manual/umbral/intervalo).")]
    [RequireSudo]
    public async Task ChangeQueueModeAsync([Summary("Queue mode")] QueueOpening mode)
    {
        SysCord<T>.Runner.Hub.Config.Queues.QueueToggleMode = mode;
        await ReplyAsync($"✔ Modo de cola cambiado a {mode}.").ConfigureAwait(false);
    }

    [Command("queueList")]
    [Alias("ql")]
    [Summary("Envía la lista de usuarios en la cola por mensaje privado.")]
    [RequireSudo]
    public async Task ListUserQueue()
    {
        var lines = SysCord<T>.Runner.Hub.Queues.Info.GetUserList("(ID {0}) - Code: {1} - {2} - {3}");
        var msg = string.Join("\n", lines);
        if (msg.Length < 3)
            await ReplyAsync("La lista de espera está vacía.").ConfigureAwait(false);
        else
            await Context.User.SendMessageAsync(msg).ConfigureAwait(false);
    }

    private string ClearTrade()
    {
        var userID = Context.User.Id;
        return ClearTrade(userID);
    }

    //private static string ClearTrade(string username)
    //{
    //    var result = Info.ClearTrade(username);
    //    return GetClearTradeMessage(result);
    //}

    private static string ClearTrade(ulong userID)
    {
        var result = Info.ClearTrade(userID);
        return GetClearTradeMessage(result);
    }

    private static string GetClearTradeMessage(QueueResultRemove result)
    {
        return result switch
        {
            QueueResultRemove.CurrentlyProcessing => "⚠️ Parece que estás siendo procesado actualmente! No se te eliminó de la lista.",
            QueueResultRemove.CurrentlyProcessingRemoved => "⚠️ Parece que estás siendo procesado actualmente!",
            QueueResultRemove.Removed => "✓ Te he eliminado de la lista.",
            _ => "⚠️ Lo sentimos, actualmente no estás en la lista.",
        };
    }
}
