using HarmonyLib;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI;
#else
using ScheduleOne.Persistence;
using ScheduleOne.UI;
#endif

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Holds the loading screen open until dedicated-server authentication and mod verification complete.
    /// </summary>
    [HarmonyPatch]
    internal static class LoadingScreenPatches
    {
        internal static void Initialize()
        {
            DebugLog.StartupDebug("LoadingScreen patches initialized (using attribute-based patching)");
        }

        [HarmonyPatch(typeof(LoadManager), nameof(LoadManager.GetLoadStatusText))]
        [HarmonyPostfix]
        private static void GetLoadStatusTextPostfix(ref string __result)
        {
            if (ShouldHoldLoadingScreen())
            {
                __result = "Verifying server requirements...";
            }
        }

        [HarmonyPatch(typeof(LoadingScreen), nameof(LoadingScreen.Close))]
        [HarmonyPrefix]
        private static bool ClosePrefix(LoadingScreen __instance)
        {
            bool shouldHold = ShouldHoldLoadingScreen();
            if (shouldHold)
            {
                DebugLog.Debug("Holding loading screen open until dedicated join verification completes");
            }

            // Native Close pops its UI state even when it has already closed. Both the
            // game's load completion and our verification completion can call it.
            return !shouldHold && (!DedicatedRuntimeContext.IsActive || __instance.IsOpen);
        }

        private static bool ShouldHoldLoadingScreen()
        {
            return Core.ClientBootstrap.Instance?.ConnectionManager?.ShouldBlockLoadingScreenClose ?? false;
        }
    }
}
