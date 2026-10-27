#if GAME_047
#if IL2CPP
using SleepOwner = Il2CppScheduleOne.GameTime.SleepController;
#else
using SleepOwner = ScheduleOne.GameTime.SleepController;
#endif
#else
#if IL2CPP
using SleepOwner = Il2CppScheduleOne.GameTime.TimeManager;
#else
using SleepOwner = ScheduleOne.GameTime.TimeManager;
#endif
#endif

namespace DedicatedServerMod.Utils
{
    /// <summary>Reads sleep state from the owning system for the selected native game API.</summary>
    internal static class SleepRuntime
    {
        internal static bool IsSleepInProgress => SleepOwner.Instance != null && SleepOwner.Instance.IsSleepInProgress;
    }
}
