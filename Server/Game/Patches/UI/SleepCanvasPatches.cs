#if GAME_BETA
#if IL2CPP
using SleepMenuType = Il2CppScheduleOne.UI.SleepMenu;
#else
using SleepMenuType = ScheduleOne.UI.SleepMenu;
#endif
#else
#if IL2CPP
using SleepMenuType = Il2CppScheduleOne.UI.SleepCanvas;
#else
using SleepMenuType = ScheduleOne.UI.SleepCanvas;
#endif
#endif
using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.UI;
#else
using FishNet;
using ScheduleOne.UI;
#endif
using UnityEngine;

namespace DedicatedServerMod.Server.Game.Patches.UI
{
    [HarmonyPatch(typeof(SleepMenuType),
#if GAME_BETA
                "OnSleepStart"
#else
                "SleepStart"
#endif
)]
    internal static class SleepCanvasPatches
    {
        private static bool Prefix()
        {
            if (!InstanceFinder.IsServer || !DedicatedServerPatchCommon.IsDedicatedHeadlessServer())
            {
                return true;
            }

            return false;
        }
    }
}
