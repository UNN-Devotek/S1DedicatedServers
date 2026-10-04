using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public enum LogType { Error, Assert, Warning, Log, Exception }
}
namespace DedicatedServerMod.Client.Managers
{
    internal static class ClientConnectionManager { internal static bool IsDedicatedServerSessionActive; }
}
namespace CollectorTests
{
    internal static class RecordedState
    {
        internal static bool Broken = true;
        internal static int CollectorCalls;
        internal static readonly List<string> DiskMessages = new();
    }
}
// The test supplies the current game's exact type/method name for both runtime families.
#if IL2CPP
namespace Il2CppScheduleOne.Reporting
#else
namespace ScheduleOne.Reporting
#endif
{
    public sealed class DebugLogCollector
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void HandleLog(string logString, string stackTrace, UnityEngine.LogType logType)
        {
            CollectorTests.RecordedState.CollectorCalls++;
            if (CollectorTests.RecordedState.Broken) throw new NullReferenceException("feedback buffer");
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Receive(string message) => HandleLog(message, "", UnityEngine.LogType.Log);
    }
}
