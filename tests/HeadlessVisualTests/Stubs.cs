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

#if IL2CPP
namespace Il2CppScheduleOne.Reflections {
#else
namespace ScheduleOne.Reflections {
#endif
 public sealed class ReflectionProbeManager {
  public int Kernels, Updates, Assignments, Releases;
  [MethodImpl(MethodImplOptions.NoInlining)] public void Start()=>Kernels++;
  [MethodImpl(MethodImplOptions.NoInlining)] public void UpdateProbes()=>Updates++;
  [MethodImpl(MethodImplOptions.NoInlining)] public void SetCubemaps(string activeProfile,string neighbourProfile)=>Assignments++;
  public void OnDestroy()=>Releases++;
 }
}
#if IL2CPP
namespace Il2CppScheduleOne.Weather {
#else
namespace ScheduleOne.Weather {
#endif
 public sealed class MaskController {
  public int Masks, WetMaps, Completions, Releases;
  [MethodImpl(MethodImplOptions.NoInlining)] public void UpdateMaskMap()=>Masks++;
  [MethodImpl(MethodImplOptions.NoInlining)] public void RunWetMaskShader()=>WetMaps++;
  public void BuildTextureArrayAsync(Action done){Completions++;done();}
  public void OnDestroy()=>Releases++;
 }
}
#if IL2CPP
namespace Il2CppCorgiGodRays {
#else
namespace CorgiGodRays {
#endif
 public sealed class GodRaysRenderFeature {
  public int Buffers, Passes, Releases;
  [MethodImpl(MethodImplOptions.NoInlining)] public void Create()=>Buffers++;
  [MethodImpl(MethodImplOptions.NoInlining)] public void AddRenderPasses()=>Passes++;
  public void Dispose(bool disposing)=>Releases++;
 }
}

#if IL2CPP
namespace Il2CppScheduleOne.DevUtilities {
#else
namespace ScheduleOne.DevUtilities {
#endif
 public sealed class OptimizedLight {
  public int Culls;
  [MethodImpl(MethodImplOptions.NoInlining)] public void UpdateCull()=>Culls++;
 }
}
