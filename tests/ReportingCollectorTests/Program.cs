using System.Reflection;
using CollectorTests;
using DedicatedServerMod.Client.Managers;
using HarmonyLib;
#if IL2CPP
using Collector = Il2CppScheduleOne.Reporting.DebugLogCollector;
#else
using Collector = ScheduleOne.Reporting.DebugLogCollector;
#endif

Collector collector = new();
int checks = 0;
ClientConnectionManager.IsDedicatedServerSessionActive = true;
Throws<NullReferenceException>(() => Broadcast("unpatched NPC relationship delivery"));
checks++;
Type patchType = Assembly.GetExecutingAssembly().GetType("DedicatedServerMod.Shared.Patches.ReportingLogCollectorPatches");
Require(patchType != null, "Current game's reporting collector has no dedicated-session patch.");
Harmony harmony = new("s1ds.tests.reporting-collector");
harmony.CreateClassProcessor(patchType).Patch();
MethodInfo original = typeof(Collector).GetMethod("HandleLog", BindingFlags.NonPublic | BindingFlags.Instance);
Require(Harmony.GetPatchInfo(original)?.Owners.Contains(harmony.Id) == true, "Patch missed the current game's HandleLog target.");
checks++;
RecordedState.CollectorCalls = 0;
foreach (string message in new[] { "NPC relationship data", "region unlocked", "sample processed" }) Broadcast(message);
Require(RecordedState.CollectorCalls == 0, "Broken feedback collector still ran during a dedicated session.");
Require(RecordedState.DiskMessages.TakeLast(3).SequenceEqual(new[] { "NPC relationship data", "region unlocked", "sample processed" }), "Other logging listener was suppressed.");
checks += 2;
ClientConnectionManager.IsDedicatedServerSessionActive = false;
#if SERVER
Broadcast("server build remains dedicated");
Require(RecordedState.CollectorCalls == 0, "Server build depends on client session state.");
checks++;
#else
Throws<NullReferenceException>(() => Broadcast("ordinary single-player logging"));
RecordedState.Broken = false;
RecordedState.CollectorCalls = 0;
Broadcast("ordinary co-op logging");
Require(RecordedState.CollectorCalls == 1, "Collector was disabled outside a dedicated session.");
checks += 2;
#endif
harmony.UnpatchAll(harmony.Id);
RecordedState.Broken = false;
RecordedState.CollectorCalls = 0;
Broadcast("unpatched ordinary logging");
Require(RecordedState.CollectorCalls == 1, "Unpatch did not restore the collector.");
checks++;
Console.WriteLine($"PASS|ReportingCollectorTests|checks={checks}");

void Broadcast(string message)
{
    // Two independent Unity log listeners: a disk sink and the game's feedback collector.
    RecordedState.DiskMessages.Add(message);
    collector.Receive(message);
}
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}
