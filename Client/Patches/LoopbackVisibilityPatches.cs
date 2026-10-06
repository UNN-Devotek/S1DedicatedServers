using DedicatedServerMod.Utils;
using HarmonyLib;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.PlayerScripts;
#else
using FishNet;
using ScheduleOne.PlayerScripts;
#endif

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Preserves the loopback player's hidden presentation when native spawn/appearance
    /// code reapplies third-person visibility after the player-spawned callback.
    /// </summary>
    [HarmonyPatch(typeof(Player), "ApplyThirdPersonMeshVisibility")]
    internal static class LoopbackVisibilityPatches
    {
        private static void Prefix(Player __instance)
        {
            if (!DedicatedRuntimeContext.IsActive || InstanceFinder.IsServer ||
                __instance == null || !__instance.IsGhostHost())
            {
                return;
            }

            __instance.ThirdPersonMeshesVisibleToLocalPlayer = false;
            __instance.Avatar?.SetVisible(false);
        }
    }
}
