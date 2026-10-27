using HarmonyLib;
using System.Reflection;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.Product;
#else
using FishNet;
using ScheduleOne.Product;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    /// <summary>Skips runtime product icon rendering on headless hosts across game API versions.</summary>
    [HarmonyPatch]
    internal static class ProductIconManagerPatches
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ProductIconManager), "GenerateRuntimeIcons")
                ?? AccessTools.Method(typeof(ProductIconManager), "GenerateIcons");
        }

        private static bool Prefix()
        {
            return !InstanceFinder.IsServer && !UnityEngine.Application.isBatchMode;
        }
    }
}
