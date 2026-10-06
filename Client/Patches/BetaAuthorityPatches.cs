#if GAME_BETA
using DedicatedServerMod.Utils;
using HarmonyLib;
#if IL2CPP
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.NPCs.Behaviour;
#else
using ScheduleOne.EntityFramework;
using ScheduleOne.NPCs.Behaviour;
#endif

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Keeps buildable destruction on the server. The beta's RunLocally RPC body
    /// attempts a predicted despawn of a child NetworkObject on remote clients.
    /// The RPC writer and inventory pickup are deliberately left intact.
    /// </summary>
    [HarmonyPatch(typeof(BuildableItem), "RpcLogic___Destroy_Server_2166136261")]
    internal static class BetaBuildableDestroyPatches
    {
        private static bool Prefix(BuildableItem __instance)
        {
            return !DedicatedRuntimeContext.IsActive || __instance.IsServerInitialized;
        }
    }

    /// <summary>
    /// Runs flee destination selection only on the authoritative server while retaining
    /// StartFlee's client animation, speed control and voice timing.
    /// </summary>
    [HarmonyPatch(typeof(FleeBehaviour), "Flee")]
    internal static class BetaFleeDestinationPatches
    {
        private static bool Prefix(FleeBehaviour __instance)
        {
            return !DedicatedRuntimeContext.IsActive || __instance.IsServerInitialized;
        }
    }
}
#endif
