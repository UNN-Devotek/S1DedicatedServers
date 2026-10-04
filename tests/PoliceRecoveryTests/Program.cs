using System.Reflection;
using System.Runtime.ExceptionServices;
using DedicatedServerMod.Server.Game.Patches.Gameplay;
using DedicatedServerMod.Utils;
using UnityEngine;
#if IL2CPP
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Police;
using InstanceFinder = Il2CppFishNet.InstanceFinder;
#else
using ScheduleOne.NPCs;
using ScheduleOne.NPCs.Behaviour;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Police;
using InstanceFinder = FishNet.InstanceFinder;
#endif

int passed = 0, failed = 0;
Run("null target guard disables the concrete pursuit", () =>
{
    PursuitBehaviour pursuit = new();
    PoliceOfficer.Officers.Add(new() { PursuitBehaviour = pursuit });
    Equal(false, InvokePatch(typeof(PursuitBehaviourUpdatePatches), "Prefix", pursuit));
    Equal(false, pursuit.Enabled);
    Equal(1, pursuit.DisableCalls);
});
Run("ghost target guard releases pursuit", () =>
{
    PursuitBehaviour pursuit = new() { TargetPlayer = new() { IsGhost = true } };
    PoliceOfficer.Officers.Add(new() { PursuitBehaviour = pursuit });
    InvokePatch(typeof(CombatBehaviourUpdatePatches), "Prefix", pursuit);
    Equal(false, pursuit.Enabled);
});
Run("null target visibility guard releases pursuit", () =>
{
    PursuitBehaviour pursuit = new();
    InvokePatch(typeof(CombatBehaviourCheckTargetVisibilityPatches), "Prefix", pursuit);
    Equal(false, pursuit.Enabled);
});
Run("invalid selected pursuit is removed before pausing a healthy behaviour", () =>
{
    Behaviour healthy = new() { Active = true };
    PursuitBehaviour pursuit = new();
    NPCBehaviour controller = Controller(pursuit);
    controller.activeBehaviour = healthy;
    InvokePatch(typeof(NpcBehaviourUpdatePatches), "Prefix", controller);
    Equal(0, pursuit.ActivateCalls);
    Equal(0, healthy.PauseCalls);
    Equal(false, pursuit.Enabled);
});
Run("disconnected selected pursuit is not resumed", () =>
{
    PursuitBehaviour pursuit = new() { Started = true, TargetPlayer = new() };
    pursuit.TargetPlayer.Owner.IsActive = false;
    InvokePatch(typeof(NpcBehaviourUpdatePatches), "Prefix", Controller(pursuit));
    Equal(0, pursuit.ResumeCalls);
    Equal(false, pursuit.Enabled);
});
Run("null body-search target is recognized", () =>
{
    BodySearchBehaviour search = new();
    InvokePatch(typeof(NpcBehaviourUpdatePatches), "Prefix", Controller(search));
    Equal(0, search.ActivateCalls);
    Equal(false, search.Enabled);
});
Run("valid pursuit activation follows the ordinary update path", () =>
{
    PursuitBehaviour pursuit = new() { TargetPlayer = new() };
    InvokePatch(typeof(NpcBehaviourUpdatePatches), "Prefix", Controller(pursuit));
    Equal(1, pursuit.ActivateCalls);
    Equal(1, pursuit.UpdateCalls);
    Equal(0, pursuit.DisableCalls);
});
Run("police activation null-reference is logged and disabled", () =>
{
    PursuitBehaviour pursuit = new() { TargetPlayer = new(), OnActivate = () => throw new NullReferenceException("StartCombat") };
    InvokePatch(typeof(NpcBehaviourUpdatePatches), "Prefix", Controller(pursuit));
    Equal(false, pursuit.Enabled);
    Equal(0, pursuit.UpdateCalls);
    Require(DebugLog.Messages.Any(message => message.Contains("StartCombat")), "Missing activation exception diagnostic.");
});
Run("police resume null-reference is disabled", () =>
{
    VehiclePursuitBehaviour pursuit = new() { Started = true, Target = new(), OnResume = () => throw new NullReferenceException("Resume") };
    InvokePatch(typeof(NpcBehaviourUpdatePatches), "Prefix", Controller(pursuit));
    Equal(false, pursuit.Enabled);
});
Run("cleanup is not blocked by another officer's logging cooldown", () =>
{
    for (int i = 0; i < 2; i++)
    {
        PursuitBehaviour pursuit = new() { TargetPlayer = new(), OnActivate = () => throw new NullReferenceException("StartCombat") };
        InvokePatch(typeof(NpcBehaviourUpdatePatches), "Prefix", Controller(pursuit));
        Equal(false, pursuit.Enabled);
    }
});
Run("unrelated activation exceptions propagate", () =>
{
    Behaviour behaviour = new() { OnActivate = () => throw new NullReferenceException("unrelated") };
    Throws<NullReferenceException>(() => InvokePatch(typeof(NpcBehaviourUpdatePatches), "Prefix", Controller(behaviour)));
    Equal(0, behaviour.DisableCalls);
});
Run("unexpected police activation exceptions propagate", () =>
{
    PursuitBehaviour pursuit = new() { TargetPlayer = new(), OnActivate = () => throw new InvalidOperationException("unexpected") };
    Throws<InvalidOperationException>(() => InvokePatch(typeof(NpcBehaviourUpdatePatches), "Prefix", Controller(pursuit)));
});
Run("valid-target update exceptions propagate", () =>
{
    PursuitBehaviour pursuit = new() { TargetPlayer = new(), Active = true, OnUpdate = () => throw new NullReferenceException("update") };
    NPCBehaviour controller = Controller(pursuit);
    controller.activeBehaviour = pursuit;
    Throws<NullReferenceException>(() => InvokePatch(typeof(NpcBehaviourUpdatePatches), "Prefix", controller));
});
Run("invalid target during activation is cleaned up even for a non-null-reference exception", () =>
{
    PursuitBehaviour pursuit = new() { TargetPlayer = new() };
    pursuit.OnActivate = () => { pursuit.TargetPlayer = null; throw new InvalidOperationException("target disconnected"); };
    InvokePatch(typeof(NpcBehaviourUpdatePatches), "Prefix", Controller(pursuit));
    Equal(false, pursuit.Enabled);
});
Run("debug mode leaves the original update enabled", () =>
{
    NPCBehaviour controller = Controller(new PursuitBehaviour());
    controller.DEBUG_MODE = true;
    Equal(true, InvokePatch(typeof(NpcBehaviourUpdatePatches), "Prefix", controller));
});
Run("non-server guards do not disable pursuit", () =>
{
    InstanceFinder.IsServer = false;
    PursuitBehaviour pursuit = new();
    Equal(true, InvokePatch(typeof(PursuitBehaviourUpdatePatches), "Prefix", pursuit));
    Equal(0, pursuit.DisableCalls);
});
Run("null-player sweep removes invalid targets but preserves a valid pursuit", () =>
{
    PursuitBehaviour invalid = new(), valid = new() { TargetPlayer = new() };
    VehiclePursuitBehaviour vehicle = new();
    BodySearchBehaviour bodySearch = new();
    PoliceOfficer.Officers.Add(new() { PursuitBehaviour = invalid, BodySearchBehaviour = bodySearch, VehiclePursuitBehaviour = vehicle });
    PoliceOfficer.Officers.Add(new() { PursuitBehaviour = valid });
    DedicatedPolicePursuitAuthority.ClearPoliceTargeting(null);
    Equal(false, invalid.Enabled); Equal(false, bodySearch.Enabled); Equal(false, vehicle.Enabled);
    Equal(true, valid.Enabled);
});
Run("healthy ragdoll waits eight seconds before fallback", () =>
{
    NPCMovement movement = new();
    Tick(movement, 0); Tick(movement, 7.9f); Equal(0, movement.DeactivateCalls);
    Tick(movement, 8); Equal(1, movement.DeactivateCalls);
});
Run("paused ragdoll resets continuous eligibility", () =>
{
    NPCMovement movement = new();
    Tick(movement, 0); movement.IsPaused = true; Tick(movement, 8);
    movement.IsPaused = false; Tick(movement, 9); Tick(movement, 16);
    Equal(0, movement.DeactivateCalls); Tick(movement, 17); Equal(1, movement.DeactivateCalls);
});
Run("dead, knocked-out, and unconscious NPCs are excluded", () =>
{
    foreach (Action<NPC> makeIneligible in new Action<NPC>[] { npc => npc.Health.IsDead = true, npc => npc.Health.IsKnockedOut = true, npc => npc.IsConscious = false })
    {
        NPCMovement movement = new(); makeIneligible(movement.npc);
        Tick(movement, 0); Tick(movement, 8); Equal(0, movement.DeactivateCalls);
    }
});
Run("non-finite root blocks fallback", () =>
{
    NPCMovement movement = new(); movement.npc.transform.position.x = float.NaN;
    Tick(movement, 0); Tick(movement, 8); Equal(0, movement.DeactivateCalls);
});
Run("non-finite child bone blocks fallback", () =>
{
    NPCMovement movement = new(); movement.npc.Avatar.Animation.Bones[0].position.y = float.PositiveInfinity;
    Tick(movement, 0); Tick(movement, 8); Equal(0, movement.DeactivateCalls);
});
Run("non-finite collider matrix blocks fallback", () =>
{
    NPCMovement movement = new(); movement.npc.Avatar.HipBone.localToWorldMatrix.m03 = float.NaN;
    Tick(movement, 0); Tick(movement, 8); Equal(0, movement.DeactivateCalls);
});
Run("missing hip bone blocks fallback", () =>
{
    NPCMovement movement = new(); movement.npc.Avatar.HipBone = null;
    Tick(movement, 0); Tick(movement, 8); Equal(0, movement.DeactivateCalls);
});
Run("missing bone set blocks fallback", () =>
{
    NPCMovement movement = new(); movement.npc.Avatar.Animation.Bones = null;
    Tick(movement, 0); Tick(movement, 8); Equal(0, movement.DeactivateCalls);
});
Run("invalid pose warnings are limited and a repaired pose can recover", () =>
{
    NPCMovement movement = new(); movement.npc.Avatar.HipBone.rotation.w = float.NaN;
    Tick(movement, 0); Tick(movement, 8); Tick(movement, 16); Tick(movement, 24);
    Equal(0, movement.DeactivateCalls);
    Equal(1, DebugLog.Messages.Count(message => message.Contains("invalid pose")));
    movement.npc.Avatar.HipBone.rotation.w = 1;
    Tick(movement, 32); Tick(movement, 40); Equal(1, movement.DeactivateCalls);
});
Run("RPC request is not reported as confirmed recovery", () =>
{
    NPCMovement movement = new(); Tick(movement, 0); Tick(movement, 8);
    Require(!DebugLog.Messages.Any(message => message.StartsWith("Recovered conscious NPC")), "Premature recovery confirmation.");
    Require(DebugLog.Messages.Any(message => message.Contains("Requested") && message.Contains("Test Officer")), "Missing request diagnostic.");
});
Run("invalid pose after deactivation is diagnosed", () =>
{
    NPCMovement movement = new();
    movement.OnDeactivate = () => movement.npc.Avatar.HipBone.position.z = float.NaN;
    Tick(movement, 0); Tick(movement, 8);
    Require(DebugLog.Messages.Any(message => message.Contains("invalid pose") && message.Contains("after")), "Missing post-transition diagnostic.");
});

Console.WriteLine($"{(failed == 0 ? "PASS" : "FAIL")}|PoliceRecoveryTests|passed={passed}|failed={failed}");
return failed == 0 ? 0 : 1;

void Run(string name, Action test)
{
    Time.realtimeSinceStartup = 100;
    InstanceFinder.IsServer = true;
    PoliceOfficer.Officers.Clear(); DebugLog.Messages.Clear();
    // Reset only process-wide log throttling; each case constructs new game objects.
    foreach (FieldInfo field in typeof(NpcBehaviourUpdatePatches).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
    {
        if (!field.IsLiteral && field.FieldType == typeof(float)) field.SetValue(null, -100f);
        if (!field.IsLiteral && field.FieldType == typeof(int)) field.SetValue(null, 0);
    }
    try { test(); passed++; }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL|{name}|{ex.Message}"); }
}
static NPCBehaviour Controller(Behaviour behaviour)
{
    NPCBehaviour controller = new(); controller.enabledBehaviours.Add(behaviour);
    behaviour.Controller = controller; behaviour.Npc = new PoliceOfficer(); return controller;
}
static object InvokePatch(Type type, string method, object instance)
{
    try { return type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new[] { instance }); }
    catch (TargetInvocationException ex) when (ex.InnerException != null) { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
}
static void Tick(NPCMovement movement, float time)
{
    Time.realtimeSinceStartup = time; InvokePatch(typeof(NpcRagdollRecoveryPatches), "Postfix", movement);
}
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}.");
}
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}.");
}
