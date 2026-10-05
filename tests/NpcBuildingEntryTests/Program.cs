using System.Reflection;
using DedicatedServerMod.Client.Managers;
using HarmonyLib;
#if IL2CPP
using NPC = Il2CppScheduleOne.NPCs.NPC;
using Entry = Il2CppScheduleOne.NPCs.Schedules.NPCEvent_StayInBuilding;
using CoroutineService = Il2CppScheduleOne.DevUtilities.CoroutineService;
#else
using NPC = ScheduleOne.NPCs.NPC;
using Entry = ScheduleOne.NPCs.Schedules.NPCEvent_StayInBuilding;
using CoroutineService = ScheduleOne.DevUtilities.CoroutineService;
#endif

int checks = 0;
Type patch = Assembly.GetExecutingAssembly().GetType("DedicatedServerMod.Client.Patches.NPCEventStayInBuildingEnterAnimationClientPatches");
Harmony harmony = new("s1ds.tests.npc-building-entry");
harmony.CreateClassProcessor(patch).Patch();
MethodInfo original = typeof(Entry).GetMethod("RpcLogic___PlayEnterAnimation_2166136261");
Require(Harmony.GetPatchInfo(original)?.Owners.Contains(harmony.Id) == true, "Guard did not register against the runtime's NPC member shape.");
ClientConnectionManager.IsDedicatedServerSessionActive = true;
Entry action = new();
NPC npc = new();
action.SetNpc(npc);
ExpectCall(true, "healthy NPC entry");
action.SetNpc(null);
ExpectCall(false, "missing NPC");
action.SetNpc(npc);
npc.Movement = null;
ExpectCall(false, "missing movement");
npc.Movement = new();
var avatar = npc.Avatar;
npc.Avatar = null;
ExpectCall(false, "missing avatar");
npc.Avatar = avatar;
avatar.Animation = null;
ExpectCall(false, "missing animation");
avatar.Animation = new();
CoroutineService.InstanceExists = false;
ExpectCall(false, "missing coroutine service");
CoroutineService.InstanceExists = true;
ExpectCall(true, "reconnected healthy NPC");
#if IL2CPP
action.FailRead = true;
ExpectCall(false, "throwing native property getter");
action.FailRead = false;
#endif
ClientConnectionManager.IsDedicatedServerSessionActive = false;
action.SetNpc(null);
CoroutineService.InstanceExists = false;
ExpectCall(true, "ordinary co-op retains native handling");
harmony.UnpatchAll(harmony.Id);
ClientConnectionManager.IsDedicatedServerSessionActive = true;
ExpectCall(true, "unpatched native handling");
Console.WriteLine($"PASS|NpcBuildingEntryTests|checks={checks}");

void Require(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
void ExpectCall(bool shouldCall, string scenario)
{
    int before = action.Calls;
    action.RpcLogic___PlayEnterAnimation_2166136261();
    Require(action.Calls == before + (shouldCall ? 1 : 0), scenario);
}
