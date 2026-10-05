using System.Collections;
using System.Runtime.CompilerServices;
#if IL2CPP
using Callback = Il2CppSystem.Action<int>;
using Completion = Il2CppSystem.Action;
#else
using Callback = System.Action<int>;
using Completion = System.Action;
#endif

namespace UnityEngine
{
    public static class Mathf { public static float Max(float a, float b) => Math.Max(a,b); }
    public sealed class WaitForSeconds { public WaitForSeconds(float seconds) { } }
}
namespace MelonLoader
{
    public static class MelonCoroutines
    {
        public static readonly List<IEnumerator> Pending = new();
        public static void Start(IEnumerator routine) => Pending.Add(routine);
    }
}
namespace DedicatedServerMod.Client.Managers
{
    internal sealed class ClientConnectionManager
    {
        internal static bool IsDedicatedServerSessionActive = true;
        internal bool IsReturningToMenu;
    }
}
namespace DedicatedServerMod.Client.Core
{
    internal sealed class ClientBootstrap
    {
        internal static ClientBootstrap Instance = new();
        internal DedicatedServerMod.Client.Managers.ClientConnectionManager ConnectionManager = new();
    }
}
namespace DedicatedServerMod.Utils
{
    internal static class DebugLog
    {
        internal static readonly List<string> Warnings = new();
        internal static void Warning(string message) => Warnings.Add(message);
    }
}
#if IL2CPP
namespace Il2CppSystem
{
    public sealed class Action
    {
        private readonly System.Action _callback;
        public Action(System.Action callback) => _callback = callback;
        public void Invoke() => _callback();
        public static implicit operator Action(System.Action callback) => new(callback);
    }
    public sealed class Action<T>
    {
        private readonly System.Action<T> _callback;
        public Action(System.Action<T> callback) => _callback = callback;
        public void Invoke(T value) => _callback(value);
        public static implicit operator Action<T>(System.Action<T> callback) => new(callback);
    }
}
namespace Il2CppSystem.Collections.Generic { public sealed class List<T> : System.Collections.Generic.List<T> { } }
namespace Il2Cpp
{
#endif
    public sealed class ActionList
    {
#if IL2CPP
        public Il2CppSystem.Collections.Generic.List<Il2CppSystem.Action> list = new();
#else
        public List<System.Action> list = new();
#endif
        public int NativeCalls;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void InvokeAllStaggered(float staggerTime) => NativeCalls++;
    }
#if IL2CPP
}
#endif
#if IL2CPP
namespace Il2CppScheduleOne.Persistence
#else
namespace ScheduleOne.Persistence
#endif
{
    public sealed class LoadManager
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public void ExitToMenu() { }
        [MethodImpl(MethodImplOptions.NoInlining)] public void CleanUp() { }
    }
}
#if IL2CPP
namespace Il2CppScheduleOne.GameTime
#else
namespace ScheduleOne.GameTime
#endif
{
    public sealed class TimeManager { [MethodImpl(MethodImplOptions.NoInlining)] public void Clean() { } }
}
#if IL2CPP
namespace Il2CppScheduleOne.DevUtilities
#else
namespace ScheduleOne.DevUtilities
#endif
{
    public sealed class StaggeredCallbackUtility
    {
        public Callback PendingCallback;
        public Completion PendingCompletion;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void InvokeStaggered(int totalCalls, float totalTime, Callback callback, Completion onComplete = null)
        { PendingCallback = callback; PendingCompletion = onComplete; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void InvokeStaggered(int totalCalls, int callsPerSecond, Callback callback, Completion onComplete = null)
        { PendingCallback = callback; PendingCompletion = onComplete; }
    }
}
