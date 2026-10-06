using System.Runtime.CompilerServices;
namespace DedicatedServerMod.Server.Game.Patches.Common {
 internal static class DedicatedServerPatchCommon { internal static bool Visuals; internal static bool ShouldRunClientVisuals()=>Visuals; }
}
#if IL2CPP
namespace Il2CppScheduleOne.Avatar.Impostors { public sealed class AvatarImpostor { public void LateUpdate(){} } }
namespace Il2CppScheduleOne.Instancing {
#else
namespace ScheduleOne.Avatar.Impostors { public sealed class AvatarImpostor { public void LateUpdate(){} } }
namespace ScheduleOne.Instancing {
#endif
 public sealed class InstancingManager {
  public int Initializations, Draws, Releases;
  [MethodImpl(MethodImplOptions.NoInlining)] public void Start()=>Initializations++;
  [MethodImpl(MethodImplOptions.NoInlining)] public void UpdateAndDrawInstances()=>Draws++;
  public void OnDestroy()=>Releases++;
 }
}
#if IL2CPP
namespace Il2CppVolumetricFogAndMist2 {
#else
namespace VolumetricFogAndMist2 {
#endif
 public sealed class VolumetricFog {
  public int Captures, Updates, Releases;
  [MethodImpl(MethodImplOptions.NoInlining)] public void UpdateMaterialPropertiesNow(bool skipTerrainCapture,bool forceTerrainCaptureUpdate)=>Captures++;
  [MethodImpl(MethodImplOptions.NoInlining)] public void LateUpdate()=>Updates++;
  public void OnDestroy()=>Releases++;
 }
}
