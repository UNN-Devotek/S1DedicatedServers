using System.Runtime.CompilerServices;

namespace DedicatedServerMod.Client.Managers
{
    internal static class ClientConnectionManager { internal static bool IsDedicatedServerSessionActive; }
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
namespace Il2CppScheduleOne.DevUtilities
#else
namespace ScheduleOne.DevUtilities
#endif
{
    public static class CoroutineService { public static bool InstanceExists = true; }
}
#if IL2CPP
namespace Il2CppScheduleOne.NPCs
#else
namespace ScheduleOne.NPCs
#endif
{
    public class NPC
    {
        public object Movement = new();
        public TestAvatar Avatar = new();
    }
    public class TestAvatar { public object Animation = new(); }
}
#if IL2CPP
namespace Il2CppScheduleOne.NPCs.Schedules
#else
namespace ScheduleOne.NPCs.Schedules
#endif
{
    public class NPCAction
    {
#if IL2CPP
        // Match generated wrappers: the native field is a managed property.
        private NPC _npc;
        public bool FailRead;
        public NPC npc
        {
            get => FailRead ? throw new NullReferenceException("destroyed native action") : _npc;
            set => _npc = value;
        }
#else
        // Match Mono's field shape, including a non-public member on the base class.
        protected internal NPC npc;
#endif
    }
    public class NPCEvent_StayInBuilding : NPCAction
    {
        public int Calls;
        public void SetNpc(NPC value) => npc = value;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void RpcLogic___PlayEnterAnimation_2166136261() => Calls++;
    }
}
