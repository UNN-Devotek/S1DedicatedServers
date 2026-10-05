#if IL2CPP
using PlayerType = Il2CppScheduleOne.PlayerScripts.Player;
#else
using PlayerType = ScheduleOne.PlayerScripts.Player;
#endif

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>Uses the selected branch's native identity, data-ready and local visibility APIs.</summary>
    internal static class ClientPlayerCompatibility
    {
        internal static bool HasReceivedData(PlayerType player)
        {
#if GAME_BETA
            return player != null && player.PlayerLoaded;
#else
            return player != null && player.playerDataRetrieveReturned;
#endif
        }

        internal static void RequestData(PlayerType player, string steamId)
        {
#if GAME_BETA
            player.RequestPlayerData_Server(steamId, isHost: false);
#else
            player.RequestPlayerData(steamId);
#endif
        }

        internal static void SetLocalVisibility(PlayerType player, bool visible)
        {
#if GAME_BETA
            player.SetVisible(visible, network: false);
#else
            player.SetVisibleToLocalPlayer(visible);
#endif
        }
    }
}
