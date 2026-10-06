#if GAME_BETA
using HarmonyLib;
using UnityEngine;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.UI;
#else
using FishNet;
using ScheduleOne.UI;
#endif

namespace DedicatedServerMod.Server.Game.Patches.UI
{
    /// <summary>
    /// Completes the beta's server-local presentation events without opening panels
    /// that require user input. Native sleep sequencing and client panels remain intact.
    /// </summary>
    [HarmonyPatch(typeof(DailySummary), nameof(DailySummary.StartEvent))]
    internal static class HeadlessDailySummaryEventPatches
    {
        private static bool Prefix(DailySummary __instance)
        {
            if (!InstanceFinder.IsServer || !Application.isBatchMode) return true;
            __instance.IsInProgress = false;
            return false;
        }
    }

    /// <summary>Completes the headless beta host's rank panel without consuming client XP/stat displays.</summary>
    [HarmonyPatch(typeof(RankUpCanvas), nameof(RankUpCanvas.StartEvent))]
    internal static class HeadlessRankUpEventPatches
    {
        private static bool Prefix(RankUpCanvas __instance)
        {
            if (!InstanceFinder.IsServer || !Application.isBatchMode) return true;
            __instance.IsInProgress = false;
            return false;
        }
    }
}
#endif
