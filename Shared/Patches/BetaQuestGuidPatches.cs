#if GAME_BETA
using System;
using DedicatedServerMod.Utils;
using HarmonyLib;
#if IL2CPP
using Il2CppScheduleOne.Quests;
#else
using ScheduleOne.Quests;
#endif

namespace DedicatedServerMod.Shared.Patches
{
    /// <summary>
    /// Preserves a runtime quest's assigned GUID during the beta's delayed Start.
    /// CreateDeaddropCollectionQuest registers its network GUID before Start, but
    /// Start initializes from an empty StaticGUID and otherwise replaces that identity.
    /// </summary>
    [HarmonyPatch(typeof(Quest), "Start")]
    internal static class BetaQuestGuidPatches
    {
        private static void Prefix(Quest __instance)
        {
            if (!DedicatedRuntimeContext.IsActive || __instance == null ||
                (Guid.TryParse(__instance.StaticGUID, out Guid authored) && authored != Guid.Empty))
            {
                return;
            }

            if (Guid.TryParse(__instance.GUID.ToString(), out Guid assigned) && assigned != Guid.Empty)
            {
                // Keep native initialization and event registration; only correct its input.
                __instance.StaticGUID = assigned.ToString();
            }
        }
    }
}
#endif
