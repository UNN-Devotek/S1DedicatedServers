using System;
using System.Reflection;
using HarmonyLib;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using NPCActionType = Il2CppScheduleOne.NPCs.Schedules.NPCAction;
using NPCEventStayInBuildingType = Il2CppScheduleOne.NPCs.Schedules.NPCEvent_StayInBuilding;
using NPCType = Il2CppScheduleOne.NPCs.NPC;
#else
using ScheduleOne.DevUtilities;
using NPCActionType = ScheduleOne.NPCs.Schedules.NPCAction;
using NPCEventStayInBuildingType = ScheduleOne.NPCs.Schedules.NPCEvent_StayInBuilding;
using NPCType = ScheduleOne.NPCs.NPC;
#endif

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Skips stale NPC building-entry animation RPCs when reconnect scene cleanup has left
    /// the client-side NPC action without the object graph required by its coroutine.
    /// </summary>
    [HarmonyPatch(typeof(NPCEventStayInBuildingType), "RpcLogic___PlayEnterAnimation_2166136261")]
    internal static class NPCEventStayInBuildingEnterAnimationClientPatches
    {
        private const BindingFlags NpcMemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        // Mono exposes the native field directly; generated IL2CPP wrappers expose it as a property.
        private static readonly FieldInfo NpcField = typeof(NPCActionType).GetField("npc", NpcMemberFlags);
        private static readonly PropertyInfo NpcProperty = typeof(NPCActionType).GetProperty("npc", NpcMemberFlags);

        [HarmonyPrepare]
        private static bool Prepare()
        {
            return NpcField != null || NpcProperty?.GetMethod != null;
        }

        private static bool Prefix(NPCEventStayInBuildingType __instance)
        {
            if (!DedicatedRuntimeContext.IsActive)
            {
                return true;
            }

            try
            {
                if (__instance == null || !CoroutineService.InstanceExists)
                {
                    return false;
                }

                var npc = (NpcField != null
                    ? NpcField.GetValue(__instance)
                    : NpcProperty.GetValue(__instance)) as NPCType;
                if (npc == null ||
                    npc.Movement == null ||
                    npc.Avatar == null ||
                    npc.Avatar.Animation == null)
                {
                    DebugLog.Warning("Suppressed stale NPCEvent_StayInBuilding enter animation during client reconnect cleanup.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Suppressed NPCEvent_StayInBuilding enter animation after validation failed: {ex.Message}");
                return false;
            }
        }
    }

}
