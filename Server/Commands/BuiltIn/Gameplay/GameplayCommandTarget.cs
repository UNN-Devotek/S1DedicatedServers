using System;
using System.Globalization;
using System.Linq;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Gameplay
{
    /// <summary>Resolves a unique, authenticated human player for gameplay commands.</summary>
    internal static class GameplayCommandTarget
    {
        internal static ConnectedPlayerInfo Resolve(PlayerManager players, CommandContext context, string identifier)
        {
            ConnectedPlayerInfo target;
            if (identifier == null)
            {
                target = context.Executor;
            }
            else
            {
                var connected = players.GetConnectedPlayers().Where(player => player != null && !player.IsLoopbackConnection).ToList();
                var matches = connected.Where(player =>
                    string.Equals(player.TrustedUniqueId, identifier, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(player.SteamId, identifier, StringComparison.OrdinalIgnoreCase) ||
                    player.ClientId.ToString(CultureInfo.InvariantCulture) == identifier ||
                    string.Equals(player.PlayerName, identifier, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 0 && !string.IsNullOrWhiteSpace(identifier))
                {
                    matches = connected.Where(player => player.PlayerName?.Contains(identifier, StringComparison.OrdinalIgnoreCase) == true).ToList();
                }

                if (matches.Count != 1)
                {
                    context.ReplyError(matches.Count == 0
                        ? $"Player not found: {identifier}"
                        : $"Ambiguous player name: {identifier}. Use an exact name, Steam ID, or client ID.");
                    return null;
                }

                target = matches[0];
            }

            if (target == null || target.IsLoopbackConnection || !target.IsAuthenticated ||
                target.IsDisconnectProcessed || target.Connection?.IsActive != true || target.PlayerInstance == null)
            {
                context.ReplyError("Target must be an authenticated, connected, spawned player.");
                return null;
            }

            return target;
        }
    }
}
