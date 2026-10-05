using DedicatedServerMod.Server.Player;
namespace DedicatedServerMod.Server.Player
{
    public sealed class TestConnection { public bool IsActive { get; set; } = true; public bool Reject { get; set; } }
    public sealed class ConnectedPlayerInfo
    {
        public TestConnection Connection { get; set; } = new();
        public bool IsLoopbackConnection { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public string DisplayName => "Tester";
        public string TrustedUniqueId => "tester";
        public int ClientId { get; set; }
    }
    public sealed class PlayerManager
    {
        public List<ConnectedPlayerInfo> Players { get; } = new();
        public IReadOnlyList<ConnectedPlayerInfo> GetConnectedPlayers() => Players;
        public ConnectedPlayerInfo GetPlayerBySteamId(string id) => null;
        public ConnectedPlayerInfo GetPlayerByName(string name) => null;
    }
}
namespace DedicatedServerMod.Server.Permissions { public sealed class ServerPermissionService { } }
namespace DedicatedServerMod.Shared.Networking.Messaging
{
    public static class MessagingService
    {
        public static bool IsEndpointReady { get; set; } = true;
        public static List<(TestConnection Connection, string Command, string Payload)> Sent { get; } = new();
        public static bool SendToClient(TestConnection connection, string command, string data)
        {
            if (connection.Reject) return false;
            Sent.Add((connection, command, data)); return true;
        }
    }
}
namespace DedicatedServerMod.Utils
{
    public static class DebugLog
    {
        public static void Info(string message) { }
        public static void Warning(string message) { }
        public static void Error(string message, Exception error = null) { }
    }
}
namespace DedicatedServerMod.Client.Managers
{
    public sealed class ClientConnectionManager { public bool IsConnectedToDedicatedServer { get; set; } = true; }
}
namespace DedicatedServerMod.Client.Core
{
    public sealed class ClientBootstrap
    {
        public static ClientBootstrap Instance { get; set; } = new();
        public DedicatedServerMod.Client.Managers.ClientConnectionManager ConnectionManager { get; } = new();
    }
}
namespace ScheduleOne.DevUtilities { public static class Singleton<T> { public static T Instance { get; set; } } }
namespace ScheduleOne.UI
{
    public sealed class NotificationsManager
    {
        public List<(string Title,string Message,float Duration,bool Sound)> Shown { get; } = new();
        public bool Throw { get; set; }
        public void SendNotification(string title, string subtitle, object icon, float duration, bool sound)
        {
            if (Throw) throw new InvalidOperationException("UI is unloading");
            Shown.Add((title,subtitle,duration,sound));
        }
    }
}
