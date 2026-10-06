using System.Reflection;
using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
#if IL2CPP
using Instancing = Il2CppScheduleOne.Instancing.InstancingManager;
using Fog = Il2CppVolumetricFogAndMist2.VolumetricFog;
using Reflection = Il2CppScheduleOne.Reflections.ReflectionProbeManager;
using Mask = Il2CppScheduleOne.Weather.MaskController;
using Rays = Il2CppCorgiGodRays.GodRaysRenderFeature;
using Light = Il2CppScheduleOne.DevUtilities.OptimizedLight;
#else
using Instancing = ScheduleOne.Instancing.InstancingManager;
using Fog = VolumetricFogAndMist2.VolumetricFog;
using Reflection = ScheduleOne.Reflections.ReflectionProbeManager;
using Mask = ScheduleOne.Weather.MaskController;
using Rays = CorgiGodRays.GodRaysRenderFeature;
using Light = ScheduleOne.DevUtilities.OptimizedLight;
#endif
var harmony=new Harmony("s1ds.tests.headless-gpu");
foreach(var type in Assembly.GetExecutingAssembly().GetTypes().Where(t=>t.Name.StartsWith("Headless") && t.GetCustomAttributes(typeof(HarmonyPatch),false).Length>0))
 harmony.CreateClassProcessor(type).Patch();
var grass=new Instancing();var fog=new Fog();
grass.Start();grass.UpdateAndDrawInstances();fog.UpdateMaterialPropertiesNow(false,false);fog.LateUpdate();
if(grass.Initializations!=0 || grass.Draws!=0 || fog.Captures!=0 || fog.Updates!=0)
 throw new Exception("Headless visual initialization must not reach GPU kernels, buffers or fog camera captures.");
grass.OnDestroy();fog.OnDestroy();
if(grass.Releases!=1 || fog.Releases!=1)throw new Exception("Native cleanup must remain available.");
var reflection=new Reflection();var mask=new Mask();var rays=new Rays();
reflection.Start();reflection.SetCubemaps("sun","rain");reflection.UpdateProbes();
mask.UpdateMaskMap();mask.RunWetMaskShader();rays.Create();rays.AddRenderPasses();
if(reflection.Kernels+reflection.Updates+reflection.Assignments+mask.Masks+mask.WetMaps+rays.Buffers+rays.Passes!=0)
 throw new Exception("Headless reflections, weather masks and god rays must avoid GPU work.");
int completions=0;mask.BuildTextureArrayAsync(()=>completions++);reflection.OnDestroy();mask.OnDestroy();rays.Dispose(true);
if(completions!=1 || mask.Completions!=1 || reflection.Releases!=1 || mask.Releases!=1 || rays.Releases!=1)
 throw new Exception("Native completion and resource disposal must be preserved.");
var light=new Light();light.UpdateCull();
if(light.Culls!=0)throw new Exception("Headless light culling must not dereference camera/light transforms.");
DedicatedServerPatchCommon.Visuals=true;
light.UpdateCull();if(light.Culls!=1)throw new Exception("Visual-capable light culling must remain native.");
reflection.Start();reflection.SetCubemaps("sun","rain");reflection.UpdateProbes();
mask.UpdateMaskMap();mask.RunWetMaskShader();rays.Create();rays.AddRenderPasses();
if(reflection.Kernels!=1 || reflection.Updates!=1 || reflection.Assignments!=1 || mask.Masks!=1 || mask.WetMaps!=1 || rays.Buffers!=1 || rays.Passes!=1)
 throw new Exception("Visual-capable reflection/mask/ray work must remain native.");
grass.Start();grass.UpdateAndDrawInstances();fog.UpdateMaterialPropertiesNow(false,false);fog.LateUpdate();
if(grass.Initializations!=1 || grass.Draws!=1 || fog.Captures!=1 || fog.Updates!=1)
 throw new Exception("Visual-capable sessions must retain native rendering.");
harmony.UnpatchAll(harmony.Id);Console.WriteLine("PASS|HeadlessVisualTests|checks=31");
