using DedicatedServerMod.Client.Core;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
using Newtonsoft.Json;
#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
#endif

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Displays transient announcements using the game's notification UI.
    /// </summary>
    internal static class ClientAnnouncementHandler
    {
        internal static void Handle(string data)
        {
            if (ClientBootstrap.Instance?.ConnectionManager?.IsConnectedToDedicatedServer != true)
            {
                return;
            }

            try
            {
                // Bound the serialized input before parsing as well as the displayed text.
                if (string.IsNullOrEmpty(data) || data.Length > 4096)
                {
                    return;
                }

                ServerAnnouncementMessage announcement = JsonConvert.DeserializeObject<ServerAnnouncementMessage>(data);
                if (announcement == null || !ServerAnnouncementMessage.IsValid(announcement.Message))
                {
                    return;
                }

                DebugLog.Info($"[SERVER ANNOUNCEMENT] {announcement.Message}");
                NotificationsManager notifications = Singleton<NotificationsManager>.Instance;
                if (notifications == null)
                {
                    DebugLog.Warning("Server announcement UI is not available in the current scene.");
                    return;
                }

                notifications.SendNotification("Server announcement", announcement.Message, null, 10f, true);
            }
            catch (JsonException ex)
            {
                DebugLog.Warning($"Invalid server announcement: {ex.Message}");
            }
            catch (Exception ex)
            {
                DebugLog.Error("Failed to display server announcement", ex);
            }
        }
    }
}
