using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    /// <summary>
    /// Avoids GPU instancing initialization and drawing on the headless server.
    /// With a null graphics device, Start cannot resolve the compute kernel and
    /// drawing subsequently throws on every frame. Native cleanup remains intact.
    /// </summary>
    [HarmonyPatch]
    internal static class HeadlessInstancingPatches
    {
        private static readonly string[] TypeNames =
        {
            "Il2CppScheduleOne.Instancing.InstancingManager",
            "ScheduleOne.Instancing.InstancingManager"
        };

        [HarmonyPrepare]
        private static bool Prepare()
        {
            return OptionalClientVisualPatchTargets.ResolveFirst("Start", TypeNames) != null;
        }

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "Start", "UpdateAndDrawInstances" })
            {
                foreach (MethodBase method in OptionalClientVisualPatchTargets.Resolve(name, TypeNames))
                {
                    yield return method;
                }
            }
        }

        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientVisuals();
        }
    }

    /// <summary>
    /// Avoids fog material updates and camera terrain captures on the headless server.
    /// Those captures initialize a render pipeline that requires GPU graphics buffers.
    /// Weather simulation and fog component cleanup remain native.
    /// </summary>
    [HarmonyPatch]
    internal static class HeadlessFogPatches
    {
        private static readonly string[] TypeNames =
        {
            "Il2CppVolumetricFogAndMist2.VolumetricFog",
            "VolumetricFogAndMist2.VolumetricFog"
        };

        [HarmonyPrepare]
        private static bool Prepare()
        {
            return OptionalClientVisualPatchTargets.ResolveFirst("UpdateMaterialPropertiesNow", TypeNames) != null;
        }

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "UpdateMaterialPropertiesNow", "LateUpdate" })
            {
                foreach (MethodBase method in OptionalClientVisualPatchTargets.Resolve(name, TypeNames))
                {
                    yield return method;
                }
            }
        }

        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientVisuals();
        }
    }

    /// <summary>
    /// Skips reflection compute kernels, weather-mask textures and god-ray render
    /// buffers on the server. Native weather initialization callbacks and disposal
    /// are preserved; only the GPU work is intercepted.
    /// </summary>
    [HarmonyPatch]
    internal static class HeadlessAdditionalGpuPatches
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return TargetMethods().Any();
        }

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string methodName in new[] { "Start", "UpdateProbes", "SetCubemaps" })
            {
                foreach (MethodBase method in OptionalClientVisualPatchTargets.Resolve(methodName,
                    "Il2CppScheduleOne.Reflections.ReflectionProbeManager",
                    "ScheduleOne.Reflections.ReflectionProbeManager"))
                {
                    yield return method;
                }
            }

            foreach (string methodName in new[] { "UpdateMaskMap", "RunWetMaskShader" })
            {
                foreach (MethodBase method in OptionalClientVisualPatchTargets.Resolve(methodName,
                    "Il2CppScheduleOne.Weather.MaskController",
                    "ScheduleOne.Weather.MaskController"))
                {
                    yield return method;
                }
            }

            foreach (string methodName in new[] { "Create", "AddRenderPasses" })
            {
                foreach (MethodBase method in OptionalClientVisualPatchTargets.Resolve(methodName,
                    "Il2CppCorgiGodRays.GodRaysRenderFeature",
                    "CorgiGodRays.GodRaysRenderFeature"))
                {
                    yield return method;
                }
            }
        }

        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientVisuals();
        }
    }
}
