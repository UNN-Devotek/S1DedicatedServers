using System.Reflection;
using DedicatedServerMod.Client.Managers;
using DedicatedServerMod.Utils;
using HarmonyLib;
using MelonLoader;
#if IL2CPP
using ActionList = Il2Cpp.ActionList;
using Callback = Il2CppSystem.Action<int>;
using Completion = Il2CppSystem.Action;
using LoadManager = Il2CppScheduleOne.Persistence.LoadManager;
using TimeManager = Il2CppScheduleOne.GameTime.TimeManager;
using Utility = Il2CppScheduleOne.DevUtilities.StaggeredCallbackUtility;
#else
using Callback = System.Action<int>;
using Completion = System.Action;
using LoadManager = ScheduleOne.Persistence.LoadManager;
using TimeManager = ScheduleOne.GameTime.TimeManager;
using Utility = ScheduleOne.DevUtilities.StaggeredCallbackUtility;
#endif

int checks = 0;
Harmony harmony = new("s1ds.tests.scene-callbacks");
foreach (string name in new[] { "ActionListInvokeAllStaggeredPatches", "SceneCleanupCallbackPatches", "StaggeredCallbackLifetimePatches" })
{
    var patch = Assembly.GetExecutingAssembly().GetType("DedicatedServerMod.Shared.Patches."+name);
    if (patch == null) throw new Exception("Missing production patch: " + name);
    harmony.CreateClassProcessor(patch).Patch();
}
ActionList source = new();
int first = 0, stale = 0;
source.list.Add(new System.Action(() => first++));
source.list.Add(new System.Action(() => { stale++; throw new NullReferenceException("destroyed audio source"); }));
var removed = source.list[1];
source.InvokeAllStaggered(1f);
var routine = MelonCoroutines.Pending.Last();
Require(routine.MoveNext() && first == 1, "First healthy callback must run before the stagger delay.");
source.list.Remove(removed);
while (routine.MoveNext()) { }
Require(stale == 0 && DebugLog.Warnings.Count == 0, "An unsubscribed callback ran against its destroyed target.");

foreach (System.Action clean in new System.Action[] { () => new LoadManager().ExitToMenu(), () => new LoadManager().CleanUp(), () => new TimeManager().Clean() })
{
    ActionList old = new(); int calls = 0;
    old.list.Add(new System.Action(() => calls++)); old.list.Add(new System.Action(() => calls++));
    old.InvokeAllStaggered(1f); var pending = MelonCoroutines.Pending.Last(); pending.MoveNext();
    clean();
    // A reconnect finishes before the old coroutine is next resumed.
    DedicatedServerMod.Client.Core.ClientBootstrap.Instance.ConnectionManager.IsReturningToMenu = false;
    while (pending.MoveNext()) { }
    Require(calls == 1, "Old scene callbacks survived cleanup/reconnect.");
}
Utility utility = new(); int weather = 0, completion = 0;
Callback cb = new System.Action<int>(_ => weather++);
Completion done = new System.Action(() => completion++);
utility.InvokeStaggered(2, 1f, cb, done);
utility.PendingCallback.Invoke(0);
new LoadManager().ExitToMenu();
utility.PendingCallback.Invoke(1); utility.PendingCompletion.Invoke();
Require(weather == 1 && completion == 0, "Weather/native completion ran after scene exit.");
utility.InvokeStaggered(2, 2, cb, done);
utility.PendingCallback.Invoke(0); utility.PendingCallback.Invoke(1); utility.PendingCompletion.Invoke();
Require(weather == 3 && completion == 1, "New-scene callbacks/completion were suppressed.");

// Disconnect can reset the dedicated-session flag before native cleanup starts.
utility.InvokeStaggered(2, 1f, cb, done);
ClientConnectionManager.IsDedicatedServerSessionActive = false;
new LoadManager().CleanUp();
ClientConnectionManager.IsDedicatedServerSessionActive = true;
utility.PendingCallback.Invoke(0); utility.PendingCompletion.Invoke();
Require(weather == 3 && completion == 1, "Old callbacks survived cleanup after the session flag was cleared.");

ActionList healthy = new(); int invoked = 0;
healthy.list.Add(new System.Action(() => { invoked++; healthy.list.Clear(); healthy.list.Add(new System.Action(() => invoked += 100)); }));
healthy.InvokeAllStaggered(0f); while (MelonCoroutines.Pending.Last().MoveNext()) { }
Require(invoked == 1, "A callback added mid-snapshot should wait for the next invocation.");
healthy.InvokeAllStaggered(0f); while (MelonCoroutines.Pending.Last().MoveNext()) { }
Require(invoked == 101, "Next snapshot missed the newly subscribed callback.");

ClientConnectionManager.IsDedicatedServerSessionActive = false;
ActionList ordinary = new(); ordinary.InvokeAllStaggered(1f);
Require(ordinary.NativeCalls == 1, "Ordinary sessions should retain native scheduling.");
utility.InvokeStaggered(1,1f,cb,done); new LoadManager().CleanUp();
utility.PendingCallback.Invoke(0); utility.PendingCompletion.Invoke();
Require(weather == 4 && completion == 2, "Ordinary native callbacks changed.");
harmony.UnpatchAll(harmony.Id);
Console.WriteLine($"PASS|SceneCallbackTests|checks={checks}");

void Require(bool success, string message) { if (!success) throw new Exception(message); checks++; }
