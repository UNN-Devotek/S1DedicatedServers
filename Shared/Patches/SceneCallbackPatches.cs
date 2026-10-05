using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DedicatedServerMod.Utils;
using HarmonyLib;
#if IL2CPP
using LoadManagerType = Il2CppScheduleOne.Persistence.LoadManager;
using TimeManagerType = Il2CppScheduleOne.GameTime.TimeManager;
using UtilityType = Il2CppScheduleOne.DevUtilities.StaggeredCallbackUtility;
using IndexedAction = Il2CppSystem.Action<int>;
using CompletionAction = Il2CppSystem.Action;
#else
using LoadManagerType = ScheduleOne.Persistence.LoadManager;
using TimeManagerType = ScheduleOne.GameTime.TimeManager;
using UtilityType = ScheduleOne.DevUtilities.StaggeredCallbackUtility;
using IndexedAction = System.Action<int>;
using CompletionAction = System.Action;
#endif

namespace DedicatedServerMod.Shared.Patches
{
    /// <summary>Observes native cleanup too: network disconnect can unload a world before the mod handler runs.</summary>
    [HarmonyPatch]
    internal static class SceneCleanupCallbackPatches
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return typeof(LoadManagerType).GetMethods(flags)
                .Where(method => method.Name == "ExitToMenu" || method.Name == "CleanUp")
                .Concat(typeof(TimeManagerType).GetMethods(flags).Where(method => method.Name == "Clean"));
        }

        private static void Prefix()
        {
            // The dedicated-session flag may already be cleared when native disconnect cleanup runs.
            // Only dedicated callbacks capture this generation; ordinary callbacks remain unwrapped.
            SceneCallbackLifetime.Invalidate();
        }
    }

    /// <summary>Cancels native weather and other staggered callbacks when their originating scene has ended.</summary>
    [HarmonyPatch]
    internal static class StaggeredCallbackLifetimePatches
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return typeof(UtilityType).GetMethods(flags).Where(method =>
            {
                var parameters = method.GetParameters();
                return method.Name == "InvokeStaggered" && parameters.Length == 4
                    && parameters[2].ParameterType == typeof(IndexedAction)
                    && parameters[3].ParameterType == typeof(CompletionAction);
            });
        }

        private static void Prefix(ref IndexedAction callback, ref CompletionAction onComplete)
        {
            if (!DedicatedRuntimeContext.IsActive) return;
            long generation = SceneCallbackLifetime.Generation;
            IndexedAction originalCallback = callback;
            CompletionAction originalCompletion = onComplete;
            if (originalCallback != null)
            {
                callback = new Action<int>(index =>
                {
                    if (SceneCallbackLifetime.IsCurrent(generation)) originalCallback.Invoke(index);
                });
            }
            if (originalCompletion != null)
            {
                onComplete = new Action(() =>
                {
                    if (SceneCallbackLifetime.IsCurrent(generation)) originalCompletion.Invoke();
                });
            }
        }
    }
}
