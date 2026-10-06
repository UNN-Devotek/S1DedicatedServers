using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using TimeManagerType = Il2CppScheduleOne.GameTime.TimeManager;
#else
using FishNet;
using TimeManagerType = ScheduleOne.GameTime.TimeManager;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    /// <summary>
    /// Replaces only the native clock iterators' render-end yields with a normal
    /// frame yield on batch servers. The native timing, pause checks, minute/tick
    /// callbacks and synchronization still execute; no second clock is created.
    /// </summary>
    [HarmonyPatch]
    internal static class HeadlessTimeLoopPatches
    {
        private static readonly Dictionary<Type, MemberInfo> CurrentMembers = new();
        private static readonly HashSet<Type> ReportedIterators = new();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (Type type in typeof(TimeManagerType).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!type.Name.Contains("TimeLoop") && !type.Name.Contains("TickLoop")) continue;
                MethodInfo moveNext = AccessTools.Method(type, "MoveNext");
                MemberInfo current = (MemberInfo)AccessTools.Property(type, "__2__current")
                    ?? AccessTools.Field(type, "<>2__current");
                if (moveNext == null || current == null) continue;
                CurrentMembers[type] = current;
                yield return moveNext;
            }
        }

        private static bool Prepare()
        {
            if (TargetMethods().Any()) return true;
            DebugLog.Warning("Native clock iterator fields were not found; headless clock yield repair was not applied.");
            return false;
        }

        private static void Postfix(object __instance, bool __result)
        {
            if (!__result || !InstanceFinder.IsServer || !Application.isBatchMode
                || !CurrentMembers.TryGetValue(__instance.GetType(), out MemberInfo member)) return;
            object current = member is PropertyInfo property
                ? property.GetValue(__instance) : ((FieldInfo)member).GetValue(__instance);
#if IL2CPP
            bool renderEnd = current is Il2CppSystem.Object native && native.TryCast<WaitForEndOfFrame>() != null;
#else
            bool renderEnd = current is WaitForEndOfFrame;
#endif
            if (!renderEnd) return;
            if (member is PropertyInfo target) target.SetValue(__instance, null);
            else ((FieldInfo)member).SetValue(__instance, null);
            if (ReportedIterators.Add(__instance.GetType()))
                DebugLog.Info($"Headless native clock yield repaired: {__instance.GetType().Name} WaitForEndOfFrame -> next frame.");
        }
    }
}
