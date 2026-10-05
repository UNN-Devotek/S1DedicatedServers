namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x * b, a.y * b, a.z * b);
    }
    public struct Quaternion { public float x, y, z, w; }
    public sealed class Transform
    {
        public Vector3 position = new(10, 20, 30), forward = new(0, 0, 1), up = new(0, 1, 0);
        public Quaternion rotation = new() { w = 1 };
    }
}
#if IL2CPP
namespace Il2CppScheduleOne
#else
namespace ScheduleOne
#endif
{
    public static class Registry
    {
        public static object GetItem(string id) => id == "baggie" ? new object() : null;
    }
}
#if IL2CPP
namespace Il2CppScheduleOne.DevUtilities
#else
namespace ScheduleOne.DevUtilities
#endif
{
    public static class NetworkSingleton<T> { public static T Instance { get; set; } }
}
#if IL2CPP
namespace Il2CppScheduleOne.Vehicles
#else
namespace ScheduleOne.Vehicles
#endif
{
    public sealed class VehicleManager
    {
        public List<(string Code, UnityEngine.Vector3 Position, UnityEngine.Quaternion Rotation, bool Owned)> Spawned = new();
        public bool Fail { get; set; }
        public object GetVehiclePrefab(string code) => code == "shitbox" ? new object() : null;
        public object SpawnAndReturnVehicle(string code, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation, bool playerOwned)
        {
            if (Fail) return null;
            Spawned.Add((code, position, rotation, playerOwned));
            return new object();
        }
    }
}
namespace DedicatedServerMod.Server.Player
{
    public sealed class TestConnection { public bool IsActive = true, Reject; }
    public sealed class TestPlayer { public UnityEngine.Transform transform = new(); }
    public sealed class ConnectedPlayerInfo
    {
        public string PlayerName, SteamId, AuthenticatedSteamId;
        public string TrustedUniqueId => AuthenticatedSteamId ?? SteamId;
        public string DisplayName => PlayerName;
        public int ClientId;
        public TestConnection Connection = new();
        public TestPlayer PlayerInstance = new();
        public bool IsAuthenticated = true, IsLoopbackConnection, IsDisconnectProcessed;
    }
    public sealed class PlayerManager
    {
        public List<ConnectedPlayerInfo> Players = new();
        public IReadOnlyList<ConnectedPlayerInfo> GetConnectedPlayers() => Players;
        public ConnectedPlayerInfo GetPlayerBySteamId(string id) => Players.FirstOrDefault(p => p.SteamId == id);
        public ConnectedPlayerInfo GetPlayerByName(string name) => Players.FirstOrDefault(p => p.PlayerName == name);
    }
}
namespace DedicatedServerMod.Server.Permissions { public sealed class ServerPermissionService { } }
namespace DedicatedServerMod.Shared.Networking.Messaging
{
    public static class MessagingService
    {
        public static bool IsEndpointReady = true;
        public static List<(DedicatedServerMod.Server.Player.TestConnection Connection, string Command, string Payload)> Sent = new();
        public static bool SendToClient(DedicatedServerMod.Server.Player.TestConnection connection, string command, string payload)
        {
            if (connection.Reject) return false;
            Sent.Add((connection, command, payload));
            return true;
        }
    }
}
namespace DedicatedServerMod.Utils
{
    public static class DebugLog
    {
        public static void Info(string message) { }
        public static void Warning(string message) { }
        public static void Error(string message) { }
    }
}
