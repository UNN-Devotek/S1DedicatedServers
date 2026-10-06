using System.Runtime.CompilerServices;
namespace DedicatedServerMod.Client.Managers {
 internal sealed class ClientConnectionManager {
  internal static bool IsDedicatedServerSessionActive = true;
  internal bool ShouldBlockLoadingScreenClose;
 }
}
namespace DedicatedServerMod.Client.Core {
 internal sealed class ClientBootstrap {
  internal static ClientBootstrap Instance = new();
  internal DedicatedServerMod.Client.Managers.ClientConnectionManager ConnectionManager = new();
 }
}
namespace DedicatedServerMod.Utils {
 internal static class DebugLog {
  internal static void Debug(string value) { }
  internal static void StartupDebug(string value) { }
 }
 internal static class PlayerGhostHostExtensions {
  internal static bool IsGhostHost(this
#if IL2CPP
  Il2CppScheduleOne.PlayerScripts.Player
#else
  ScheduleOne.PlayerScripts.Player
#endif
  player) => player.OwnerId == 0 && !player.IsOwner;
 }
}
#if IL2CPP
namespace Il2CppFishNet { public static class InstanceFinder { public static bool IsServer; } }
namespace Il2CppScheduleOne {
#else
namespace FishNet { public static class InstanceFinder { public static bool IsServer; } }
namespace ScheduleOne {
#endif
 namespace Persistence { public sealed class LoadManager { public string GetLoadStatusText() => "Loading"; } }
 namespace UI {
  public sealed class LoadingScreen {
   public bool IsOpen = true; public int Closures; public int InvalidPops;
   [MethodImpl(MethodImplOptions.NoInlining)] public void Close() { if (!IsOpen) InvalidPops++; IsOpen=false; Closures++; }
  }
 }
 namespace EntityFramework {
  public sealed class BuildableItem {
   public bool IsServerInitialized;
   public int Requests, InventoryCredits, LocalRemoval, PredictedDespawns, AuthoritativeDespawns;
   public Action DeliverToServer;
   [MethodImpl(MethodImplOptions.NoInlining)] public void PickupItem() { InventoryCredits++; Destroy_Server(); }
   [MethodImpl(MethodImplOptions.NoInlining)] public void Destroy_Server() { Requests++; DeliverToServer?.Invoke(); RpcLogic___Destroy_Server_2166136261(); }
   [MethodImpl(MethodImplOptions.NoInlining)] public void RpcLogic___Destroy_Server_2166136261() {
    LocalRemoval++; if(IsServerInitialized) AuthoritativeDespawns++; else PredictedDespawns++;
   }
  }
 }
 namespace NPCs.Behaviour {
  public sealed class FleeBehaviour {
   public bool IsServerInitialized;
   public int DestinationRequests, Animations, SpeedChanges;
   [MethodImpl(MethodImplOptions.NoInlining)] public void StartFlee() { Flee(); Animations++; SpeedChanges++; }
   [MethodImpl(MethodImplOptions.NoInlining)] public void Flee() { DestinationRequests++; }
  }
 }
 namespace PlayerScripts {
  public sealed class Avatar { public bool Visible = true; public void SetVisible(bool value) => Visible=value; }
  public sealed class Player {
   public int OwnerId; public bool IsOwner;
   public bool ThirdPersonMeshesVisibleToLocalPlayer;
   public Avatar Avatar = new();
   public bool Rendered;
   [MethodImpl(MethodImplOptions.NoInlining)] public void OnStartClient() {
    Avatar.SetVisible(false); // Existing onPlayerSpawned callback.
    ThirdPersonMeshesVisibleToLocalPlayer=!IsOwner;
    ApplyThirdPersonMeshVisibility();
   }
   [MethodImpl(MethodImplOptions.NoInlining)] public void ApplyThirdPersonMeshVisibility() {
    Rendered=ThirdPersonMeshesVisibleToLocalPlayer;
   }
  }
 }
 namespace Quests {
  public sealed class Quest {
   public Guid GUID; public string StaticGUID;
   public int NativeStarts;
   [MethodImpl(MethodImplOptions.NoInlining)] public void Start() {
    NativeStarts++;
    GUID=Guid.TryParse(StaticGUID,out var id) ? id : Guid.NewGuid();
   }
  }
 }
}
