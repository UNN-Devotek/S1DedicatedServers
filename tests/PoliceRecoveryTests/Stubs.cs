// These managed doubles exercise the real patch control flow without starting Unity.
// Full mod builds still use the actual game, FishNet, and Unity assemblies.
using System.Reflection;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string name) { }
    }
    public static class AccessTools
    {
        public static PropertyInfo Property(Type type, string name) => type.GetProperty(name);
        public static FieldInfo Field(Type type, string name) => type.GetField(name);
        public static MethodInfo Method(Type type, string name) => type.GetMethod(name);
    }
}
namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup; }
    public static class Application { public static bool isBatchMode; }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }
    public struct Matrix4x4
    {
        public float m00, m01, m02, m03, m10, m11, m12, m13;
        public float m20, m21, m22, m23, m30, m31, m32, m33;
    }
    public class Transform
    {
        public string name = "bone";
        public Vector3 position;
        public Quaternion rotation = new(0, 0, 0, 1);
        public Vector3 lossyScale = new(1, 1, 1);
        public Matrix4x4 localToWorldMatrix;
    }
}
namespace DedicatedServerMod.Utils
{
    public static class DebugLog
    {
        public static readonly List<string> Messages = new();
        public static void Info(string message) => Messages.Add(message);
        public static void Warning(string message) => Messages.Add(message);
        public static void Warning(string message, Exception exception) => Messages.Add(message + " " + exception.Message);
    }
    public static class SafeReflection
    {
        public static bool TrySetInstanceFieldOrProperty(object value, string name, object fieldValue) => true;
        public static bool TryGetInstanceFieldOrProperty<T>(object value, string name, out T fieldValue)
        { fieldValue = default; return false; }
    }
}
namespace DedicatedServerMod.Server.Game.Patches.Common
{
    public static class DedicatedServerPatchCommon
    {
        public static bool IsDedicatedHeadlessServer() => true;
#if IL2CPP
        public static bool IsGhostOrLoopbackPlayer(Il2CppScheduleOne.PlayerScripts.Player player) => player.IsGhost;
#else
        public static bool IsGhostOrLoopbackPlayer(ScheduleOne.PlayerScripts.Player player) => player.IsGhost;
#endif
    }
}
#if IL2CPP
namespace Il2CppFishNet
#else
namespace FishNet
#endif
{
    public static class InstanceFinder { public static bool IsServer = true; }
}
#if IL2CPP
namespace Il2CppSystem.Collections.Generic
{
    public class List<T> : System.Collections.Generic.List<T> { }
}
namespace Il2CppScheduleOne.PlayerScripts
#else
namespace ScheduleOne.PlayerScripts
#endif
{
    public class Player
    {
        public OwnerData Owner = new();
        public object NetworkObject = new(), Avatar = new();
        public PlayerCrimeData CrimeData = new();
        public bool IsArrested, IsUnconscious, IsOwner, IsGhost;
    }
    public class OwnerData { public bool IsActive = true; }
    public class PlayerCrimeData
    {
        public enum EPursuitLevel { None, Investigating, Arresting, NonLethal, Lethal }
        public EPursuitLevel CurrentPursuitLevel = EPursuitLevel.Arresting;
        public Player Player;
        public float TimeSinceSighted, CurrentPursuitLevelDuration, CurrentArrestProgress;
        public void RecordLastKnownPosition(bool resetTimeSinceSighted) { }
        public void Escalate() { }
        public void SetPursuitLevel(EPursuitLevel level) => CurrentPursuitLevel = level;
        public void SetArrestProgress(float progress) => CurrentArrestProgress = progress;
    }
}
#if IL2CPP
namespace Il2CppScheduleOne.NPCs.Behaviour
#else
namespace ScheduleOne.NPCs.Behaviour
#endif
{
#if IL2CPP
    using Il2CppScheduleOne.PlayerScripts;
    using Il2CppScheduleOne.Combat;
    using Il2CppScheduleOne.NPCs;
#else
    using ScheduleOne.PlayerScripts;
    using ScheduleOne.Combat;
    using ScheduleOne.NPCs;
#endif
    public class Behaviour
    {
        public bool Started, Active, Enabled = true;
        public NPC Npc;
        public int ActivateCalls, ResumeCalls, PauseCalls, UpdateCalls, DisableCalls;
        public Action OnActivate, OnResume, OnPause, OnUpdate, OnDisable;
        public NPCBehaviour Controller;
        public void Pause_Server() { PauseCalls++; OnPause?.Invoke(); Active = false; }
        public void Resume_Server() { ResumeCalls++; OnResume?.Invoke(); Active = true; if (Controller != null) Controller.activeBehaviour = this; }
        public void Activate_Server(object conn) { ActivateCalls++; OnActivate?.Invoke(); Started = Active = true; if (Controller != null) Controller.activeBehaviour = this; }
        public void BehaviourUpdate() { UpdateCalls++; OnUpdate?.Invoke(); }
        public void Disable_Networked(object conn)
        {
            DisableCalls++; OnDisable?.Invoke(); Enabled = Active = false;
            Controller?.enabledBehaviours.Remove(this);
            if (Controller?.activeBehaviour == this) Controller.activeBehaviour = null;
        }
    }
    public class PursuitBehaviour : CombatBehaviour
    {
        public Player TargetPlayer;
    }
    public class VehiclePursuitBehaviour : Behaviour
    {
        public Player Target;
        public bool IsTargetRecentlyVisible;
        public Vehicle vehicle = new();
        public bool IsTargetVisible() => true;
    }
    public class BodySearchBehaviour : Behaviour { public Player TargetPlayer { get; set; } }
    public class NPCBehaviour
    {
        public bool DEBUG_MODE, IsServerInitialized = true;
        public Behaviour activeBehaviour;
#if IL2CPP
        public Il2CppSystem.Collections.Generic.List<Behaviour> enabledBehaviours = new();
#else
        public List<Behaviour> enabledBehaviours = new();
#endif
    }
    public class Vehicle { public Agent Agent = new(); }
    public class Agent
    {
#if IL2CPP
        public Il2CppScheduleOne.Vehicles.AI.DriveFlags Flags = new();
#else
        public ScheduleOne.Vehicles.AI.DriveFlags Flags = new();
#endif
    }
}
#if IL2CPP
namespace Il2CppScheduleOne.Combat
#else
namespace ScheduleOne.Combat
#endif
{
#if IL2CPP
    public class CombatBehaviour : Il2CppScheduleOne.NPCs.Behaviour.Behaviour
#else
    public class CombatBehaviour : ScheduleOne.NPCs.Behaviour.Behaviour
#endif
    {
        public bool IsTargetRecentlyVisible;
        public bool IsTargetVisibleThisFrame() => true;
    }
}
#if IL2CPP
namespace Il2CppScheduleOne.NPCs
#else
namespace ScheduleOne.NPCs
#endif
{
    public class NPC
    {
        public string FullName = "Test Officer";
        public bool IsConscious = true;
        public Avatar Avatar = new();
        public Health Health = new();
        public UnityEngine.Transform transform = new();
    }
    public class Avatar
    {
        public bool Ragdolled = true;
        public UnityEngine.Transform transform = new(), HipBone = new();
        public AvatarAnimation Animation = new();
    }
    public class AvatarAnimation { public UnityEngine.Transform[] Bones = { new() }; }
    public class Health { public bool IsDead, IsKnockedOut; }
    public class NPCMovement
    {
        public bool IsPaused, IsServerInitialized = true, CanRecover = true;
        public NPC npc = new();
        public int DeactivateCalls;
        public Action OnDeactivate;
        public bool CanRecoverFromRagdoll() => CanRecover;
        public void DeactivateRagdoll() { DeactivateCalls++; OnDeactivate?.Invoke(); }
    }
}
#if IL2CPP
namespace Il2CppScheduleOne.Police
#else
namespace ScheduleOne.Police
#endif
{
#if IL2CPP
    using Il2CppScheduleOne.NPCs;
    using Il2CppScheduleOne.NPCs.Behaviour;
#else
    using ScheduleOne.NPCs;
    using ScheduleOne.NPCs.Behaviour;
#endif
    public class PoliceOfficer : NPC
    {
        public static List<PoliceOfficer> Officers = new();
        public BodySearchBehaviour BodySearchBehaviour;
        public PursuitBehaviour PursuitBehaviour;
        public VehiclePursuitBehaviour VehiclePursuitBehaviour;
    }
}
#if IL2CPP
namespace Il2CppScheduleOne.Map
#else
namespace ScheduleOne.Map
#endif
{
    public class PoliceStation { public void Dispatch() { } }
}
#if IL2CPP
namespace Il2CppScheduleOne.Vision
#else
namespace ScheduleOne.Vision
#endif
{
    public class VisionEventReceipt { public object Target; }
}
#if IL2CPP
namespace Il2CppScheduleOne.Vehicles.AI
#else
namespace ScheduleOne.Vehicles.AI
#endif
{
    public class DriveFlags
    {
        public enum EObstacleMode { Default }
        public bool OverrideSpeed, AutoBrakeAtDestination, UseRoads;
        public float OverriddenSpeed, OverriddenReverseSpeed;
        public EObstacleMode ObstacleMode;
    }
}
namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    public enum ServerPressureLevel { Low, Medium, High }
    public static class ServerAdaptivePerformanceTuning
    {
        public static Snapshot GetSnapshot() => new();
        public class Snapshot { public ServerPressureLevel PressureLevel; }
    }
}
#if IL2CPP
namespace Il2CppInterop.Runtime
{
    public class Il2CppException : Exception
    {
        public Il2CppException(string message) : base(message) { }
    }
}
#endif
