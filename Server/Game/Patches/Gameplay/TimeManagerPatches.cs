#if GAME_BETA && IL2CPP
using SleepOwnerType = Il2CppScheduleOne.GameTime.SleepController;
#elif GAME_BETA
using SleepOwnerType = ScheduleOne.GameTime.SleepController;
#elif IL2CPP
using SleepOwnerType = Il2CppScheduleOne.GameTime.TimeManager;
#else
using SleepOwnerType = ScheduleOne.GameTime.TimeManager;
#endif
using HarmonyLib;
using DedicatedServerMod.Server.Core;
using DedicatedServerMod.Server.Game.Patches.Common;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
using LoadManagerType = Il2CppScheduleOne.Persistence.LoadManager;
using TimeManagerType = Il2CppScheduleOne.GameTime.TimeManager;
#else
using FishNet;
using ScheduleOne.DevUtilities;
using LoadManagerType = ScheduleOne.Persistence.LoadManager;
using TimeManagerType = ScheduleOne.GameTime.TimeManager;
#endif
using UnityEngine;

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    internal static class TimeManagerPausePolicy
    {
        internal static bool ShouldPauseGame()
        {
            if (!InstanceFinder.IsServer ||
                !Application.isBatchMode ||
                !ServerConfig.Instance.PauseGameWhenEmpty)
            {
                return false;
            }

            return ServerBootstrap.Players != null && ServerBootstrap.Players.ConnectedPlayerCount == 0;
        }
    }

    [HarmonyPatch(typeof(TimeManagerType), nameof(TimeManagerType.SetTimeSpeedMultiplier))]
    internal static class TimeManagerSetTimeSpeedMultiplierPatches
    {
        private static void Prefix(ref float multiplier)
        {
            if (!InstanceFinder.IsServer ||
                !Application.isBatchMode ||
                multiplier > 0f ||
                (multiplier == 0f && TimeManagerPausePolicy.ShouldPauseGame()))
            {
                return;
            }

            multiplier = 1f;
        }
    }

    [HarmonyPatch(typeof(TimeManagerType), "Update")]
    internal static class TimeManagerUpdatePatches
    {
        private static void Postfix(TimeManagerType __instance)
        {
            if (!InstanceFinder.IsServer || !Application.isBatchMode)
            {
                return;
            }

            var loadManager = Singleton<LoadManagerType>.Instance;
            if (loadManager == null || loadManager.IsLoading || !loadManager.IsGameLoaded || SleepRuntime.IsSleepInProgress)
            {
                return;
            }

            bool shouldPauseGame = TimeManagerPausePolicy.ShouldPauseGame();
            if (shouldPauseGame && __instance.TimeSpeedMultiplier != 0f)
            {
                __instance.SetTimeSpeedMultiplier(0f);
            }
            else if (!shouldPauseGame && __instance.TimeSpeedMultiplier <= 0f)
            {
                __instance.SetTimeSpeedMultiplier(1f);
            }
        }
    }


    [HarmonyPatch(typeof(SleepOwnerType), "StartSleep")]
    internal static class TimeManagerStartSleepPatches
    {
        private static bool Prefix()
        {
            if (!InstanceFinder.IsServer)
            {
                return true;
            }

            return DedicatedServerPatchCommon.CountSleepEligiblePlayers() > 0;
        }
    }

    [HarmonyPatch(typeof(SleepOwnerType), "StartSleep")]
    internal static class TimeManagerStartSleepHeadlessPatches
    {
        private static void Postfix(SleepOwnerType __instance)
        {
            ForceHeadlessHostSleepDone(__instance);
        }

        public static void ForceHeadlessHostSleepDone(SleepOwnerType __instance)
        {
            if (!InstanceFinder.IsServer || !DedicatedServerPatchCommon.IsDedicatedHeadlessServer())
            {
                return;
            }

            if (__instance == null || !__instance.IsSleepInProgress)
            {
                return;
            }
#if GAME_BETA
            if (__instance.IsHostReadyToProceed)
#else
            if (__instance.HostSleepDone)
#endif
            {
                return;
            }

#if GAME_BETA
            __instance.IsHostReadyToProceed = true;
#else
            __instance.SetHostSleepDone(done: true);
#endif
        }
    }
}
