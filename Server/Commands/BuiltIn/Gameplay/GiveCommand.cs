using System.Globalization;
using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.ConsoleSupport;
using DedicatedServerMod.Shared.Networking.Messaging;
using DedicatedServerMod.Shared.Permissions;
using DedicatedServerMod.Utils;
#if IL2CPP
using Registry = Il2CppScheduleOne.Registry;
#else
using Registry = ScheduleOne.Registry;
#endif

namespace DedicatedServerMod.Server.Commands.BuiltIn.Gameplay
{
    /// <summary>Validates an item grant and relays the game's inventory command to its recipient.</summary>
    internal sealed class GiveCommand : BaseServerCommand
    {
        internal const int MaxQuantity = 1000;

        internal GiveCommand(PlayerManager players) : base(players) { }

        /// <inheritdoc />
        public override string CommandWord => "give";
        /// <inheritdoc />
        public override string Description => "Request an item grant for a connected player's inventory.";
        /// <inheritdoc />
        public override string Usage => "give <player_name_or_id> <item_id> [quantity] (in-game self: give <item_id> [quantity])";
        /// <inheritdoc />
        public override string RequiredPermissionNode => PermissionNode.CreateConsoleCommandNode(CommandWord);

        /// <inheritdoc />
        public override void Execute(CommandContext context)
        {
            int count = context.Arguments.Count;
            if (count < (context.IsConsoleExecution ? 2 : 1) || count > 3)
            {
                context.ReplyError($"Usage: {Usage}");
                return;
            }

            // Preserve the vanilla in-game give <item> [quantity] syntax. A targeted
            // in-game grant must supply all three arguments to disambiguate it.
            bool targeted = context.IsConsoleExecution || count == 3;
            int itemIndex = targeted ? 1 : 0;
            int quantity = 1;
            if (count > itemIndex + 1 &&
                (!int.TryParse(context.Arguments[itemIndex + 1], NumberStyles.None, CultureInfo.InvariantCulture, out quantity) ||
                 quantity < 1 || quantity > MaxQuantity))
            {
                context.ReplyError($"Quantity must be a whole number between 1 and {MaxQuantity}.");
                return;
            }

            ConnectedPlayerInfo target = GameplayCommandTarget.Resolve(PlayerManager, context, targeted ? context.Arguments[0] : null);
            if (target == null) return;

            string itemId = context.Arguments[itemIndex].ToLowerInvariant();
            if (Registry.GetItem(itemId) == null)
            {
                context.ReplyError($"Unrecognized item ID '{itemId}'.");
                return;
            }
            if (!MessagingService.IsEndpointReady)
            {
                context.ReplyError("Item grant unavailable: server messaging is not ready.");
                return;
            }

            string payload = CommandLineParser.BuildLine(new ParsedCommandLine(CommandWord,
                new[] { itemId, quantity.ToString(CultureInfo.InvariantCulture) }));
            if (!MessagingService.SendToClient(target.Connection, Constants.Messages.ExecConsole, payload))
            {
                context.ReplyError($"Could not send item grant to {target.DisplayName}.");
                return;
            }
            context.Reply($"Item grant queued for {target.DisplayName}: {itemId} x{quantity}. Inventory delivery is not confirmed.");
        }
    }
}
