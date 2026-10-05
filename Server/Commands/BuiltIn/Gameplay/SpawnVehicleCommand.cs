using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Permissions;
using UnityEngine;
#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Vehicles;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.Vehicles;
#endif

namespace DedicatedServerMod.Server.Commands.BuiltIn.Gameplay
{
    /// <summary>Spawns a player-owned vehicle near an explicitly targeted player or in-game executor.</summary>
    internal sealed class SpawnVehicleCommand : BaseServerCommand
    {
        internal SpawnVehicleCommand(PlayerManager players) : base(players) { }

        /// <inheritdoc />
        public override string CommandWord => "spawnvehicle";
        /// <inheritdoc />
        public override string Description => "Spawn a player-owned vehicle near a connected player.";
        /// <inheritdoc />
        public override string Usage => "spawnvehicle <player_name_or_id> <vehicle_code> (in-game self: spawnvehicle <vehicle_code>)";
        /// <inheritdoc />
        public override string RequiredPermissionNode => PermissionNode.CreateConsoleCommandNode(CommandWord);

        /// <inheritdoc />
        public override void Execute(CommandContext context)
        {
            int count = context.Arguments.Count;
            if (count != 2 && (context.IsConsoleExecution || count != 1))
            {
                context.ReplyError($"Usage: {Usage}");
                return;
            }

            ConnectedPlayerInfo target = GameplayCommandTarget.Resolve(PlayerManager, context, count == 2 ? context.Arguments[0] : null);
            if (target == null) return;

            string code = context.Arguments[count - 1].ToLowerInvariant();
            VehicleManager vehicles = NetworkSingleton<VehicleManager>.Instance;
            if (vehicles == null)
            {
                context.ReplyError("Vehicle manager is not ready.");
                return;
            }
            if (vehicles.GetVehiclePrefab(code) == null)
            {
                context.ReplyError($"Unrecognized vehicle code '{code}'.");
                return;
            }

            var transform = target.PlayerInstance.transform;
            Vector3 position = transform.position + transform.forward * 4f + transform.up;
            Quaternion rotation = transform.rotation;
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z) ||
                !IsFinite(rotation.x) || !IsFinite(rotation.y) || !IsFinite(rotation.z) || !IsFinite(rotation.w) ||
                rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w < 0.0001f)
            {
                context.ReplyError($"Cannot spawn a vehicle: {target.DisplayName} has an invalid position or rotation.");
                return;
            }

            var spawned = vehicles.SpawnAndReturnVehicle(code, position, rotation, playerOwned: true);
            if (spawned == null)
            {
                context.ReplyError($"Failed to spawn vehicle '{code}'.");
                return;
            }
            context.Reply($"Spawned player-owned vehicle '{code}' near {target.DisplayName}.");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
