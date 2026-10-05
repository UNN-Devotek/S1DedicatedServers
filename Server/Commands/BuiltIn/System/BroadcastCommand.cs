using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Shared.Networking.Messaging;
using DedicatedServerMod.Shared.Permissions;
using DedicatedServerMod.Utils;
using Newtonsoft.Json;

namespace DedicatedServerMod.Server.Commands.BuiltIn.System
{
    /// <summary>
    /// Sends an announcement through the active messaging backend to authenticated players.
    /// Host console transports share this command with the in-game admin console.
    /// </summary>
    internal sealed class BroadcastCommand : BaseServerCommand
    {
        internal BroadcastCommand(PlayerManager playerManager) : base(playerManager)
        {
        }

        /// <inheritdoc />
        public override string CommandWord => "broadcast";

        /// <inheritdoc />
        public override string Description => "Shows a server announcement to connected players";

        /// <inheritdoc />
        public override string Usage => "broadcast <message>";

        /// <inheritdoc />
        public override string RequiredPermissionNode => PermissionBuiltIns.Nodes.ServerBroadcast;

        /// <inheritdoc />
        public override void Execute(CommandContext context)
        {
            string message = string.Join(" ", context.Arguments).Trim();
            if (!ServerAnnouncementMessage.IsValid(message))
            {
                context.ReplyError($"Usage: {Usage}. Supply 1–{ServerAnnouncementMessage.MaxMessageLength} characters on one line.");
                return;
            }

            if (!MessagingService.IsEndpointReady)
            {
                context.ReplyError("Broadcast unavailable: server messaging is not ready.");
                return;
            }

            string payload = JsonConvert.SerializeObject(new ServerAnnouncementMessage { Message = message });
            int sent = 0;
            int failed = 0;
            foreach (ConnectedPlayerInfo player in PlayerManager.GetConnectedPlayers())
            {
                if (player == null || player.IsLoopbackConnection || !player.IsAuthenticated ||
                    player.Connection == null || !player.Connection.IsActive)
                {
                    continue;
                }

                if (MessagingService.SendToClient(player.Connection, Constants.Messages.ServerAnnouncement, payload))
                {
                    sent++;
                }
                else
                {
                    failed++;
                }
            }

            context.Reply($"Broadcast sent to {sent} player(s): {message}");
            if (failed > 0)
            {
                context.ReplyWarning($"Broadcast could not be sent to {failed} player(s).");
            }
        }
    }
}
