using System;
using System.Reflection;
using DedicatedServerMod.Utils;
using HarmonyLib;
using UnityEngine;

namespace DedicatedServerMod.Shared.Patches
{
    /// <summary>
    /// Bypasses Schedule I's feedback-form log buffer in dedicated server and client sessions.
    /// The current game uses ScheduleOne.Reporting.DebugLogCollector rather than the legacy EasyFeedback type.
    /// Its callback can throw while appending logs, including during NPC relationship delivery and region unlocks.
    /// Other Unity/MelonLoader log listeners remain active, and ordinary client sessions retain the native collector.
    /// </summary>
    [HarmonyPatch]
    internal static class ReportingLogCollectorPatches
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return ResolveTargetMethod() != null;
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            return ResolveTargetMethod();
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !DedicatedRuntimeContext.IsActive;
        }

        private static MethodBase ResolveTargetMethod()
        {
#if IL2CPP
            const string CollectorTypeName = "Il2CppScheduleOne.Reporting.DebugLogCollector";
#else
            const string CollectorTypeName = "ScheduleOne.Reporting.DebugLogCollector";
#endif
            Type collectorType = SafeReflection.FindType(CollectorTypeName);
            return SafeReflection.FindMethod(
                collectorType,
                "HandleLog",
                typeof(string),
                typeof(string),
                typeof(LogType));
        }
    }
}
