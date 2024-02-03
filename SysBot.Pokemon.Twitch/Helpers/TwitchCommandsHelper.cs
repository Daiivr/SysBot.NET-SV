using System;
using System.Linq;
using PKHeX.Core;
using SysBot.Base;

namespace SysBot.Pokemon.Twitch;

public static class TwitchCommandsHelper<T> where T : PKM, new()
{
    // Helper functions for commands
    public static bool AddToWaitingList(string setstring, string display, string username, ulong mUserId, bool sub, out string msg)
    {
        if (!TwitchBot<T>.Info.GetCanQueue())
        {
            msg = "⚠️ Lo siento, actualmente no estoy aceptando solicitudes.";
            return false;
        }

        var set = ShowdownUtil.ConvertToShowdown(setstring);
        if (set == null)
        {
            msg = $"⚠️ Omitiendo el tradeo, @{username}: El Apodo proporcionado para la especie esta vacío.";
            return false;
        }
        var template = AutoLegalityWrapper.GetTemplate(set);
        if (template.Species < 1)
        {
            msg = $"⚠️ Omitiendo tradeo, @{username}: Por favor, lea lo que se supone que debe escribir como argumento del comando.";
            return false;
        }

        if (set.InvalidLines.Count != 0)
        {
            msg = $"⚠️ Omitiendo tradeo, @{username}: No se puede analizar el conjunto Showdown:\n{string.Join("\n", set.InvalidLines)}";
            return false;
        }

        try
        {
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            PKM pkm = sav.GetLegal(template, out var result);

                var nickname = pkm.Nickname.ToLower();
                if (nickname == "egg" && Breeding.CanHatchAsEgg(pkm.Species))
                    TradeExtensions<T>.EggTrade(pkm, template);

                if (!pkm.CanBeTraded())
                {
                    msg = $"⚠️ Omitiendo el tradeo, @{username}: ¡El contenido Pokémon proporcionado está bloqueado para el comercio!";
                    return false;
                }

            if (pkm is T pk)
            {
                var valid = new LegalityAnalysis(pkm).Valid;
                if (valid)
                {
                    var tq = new TwitchQueue<T>(pk, new PokeTradeTrainerInfo(display, mUserId), username, sub);
                    TwitchBot<T>.QueuePool.RemoveAll(z => z.UserName == username); // remove old requests if any
                    TwitchBot<T>.QueuePool.Add(tq);
                    msg = $"@{username} ➜ añadido a la lista de espera. Por favor, susúrrame tu código de intercambio! ¡Su solicitud de la lista de espera será eliminado si usted es demasiado lento! (Susurre a este bot, no el streamer)";
                    return true;
                }
            }

            var reason = result == "Timeout" ? "El conjunto tardó demasiado en generarse." : "Incapaz de legalizar el Pokémon.";
            msg = $"Skipping trade, @{username}: {reason}";
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TwitchCommandsHelper<T>));
            msg = $"⚠️ Omitiendo trad, @{username}: Ha ocurrido un problema inesperado.";
        }
        return false;
    }

    public static string ClearTrade(string user)
    {
        var result = TwitchBot<T>.Info.ClearTrade(user);
        return GetClearTradeMessage(result);
    }

    public static string ClearTrade(ulong userID)
    {
        var result = TwitchBot<T>.Info.ClearTrade(userID);
        return GetClearTradeMessage(result);
    }

    private static string GetClearTradeMessage(QueueResultRemove result)
    {
        return result switch
        {
            QueueResultRemove.CurrentlyProcessing => "⚠️ Parece que está siendo procesado. No se te ha eliminado de la cola.",
            QueueResultRemove.CurrentlyProcessingRemoved => "⚠️ Parece que estas siendo procesado. Eliminado de la cola.",
            QueueResultRemove.Removed => "✔️ Eliminado de la cola.",
            _ => "⚠️ Lo sentimos, actualmente no está en la cola.",
        };
    }

    public static string GetCode(ulong parse)
    {
        var detail = TwitchBot<T>.Info.GetDetail(parse);
        return detail == null
            ? "⚠️ Lo sentimos, actualmente no estas en la cola."
            : $"Su código de tradeo es: {detail.Trade.Code:0000 0000}";
    }
}
