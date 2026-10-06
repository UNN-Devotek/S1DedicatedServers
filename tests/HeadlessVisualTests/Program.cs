using System.Reflection;
using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
#if IL2CPP
using Instancing = Il2CppScheduleOne.Instancing.InstancingManager;
using Fog = Il2CppVolumetricFogAndMist2.VolumetricFog;
#else
using Instancing = ScheduleOne.Instancing.InstancingManager;
using Fog = VolumetricFogAndMist2.VolumetricFog;
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
DedicatedServerPatchCommon.Visuals=true;
grass.Start();grass.UpdateAndDrawInstances();fog.UpdateMaterialPropertiesNow(false,false);fog.LateUpdate();
if(grass.Initializations!=1 || grass.Draws!=1 || fog.Captures!=1 || fog.Updates!=1)
 throw new Exception("Visual-capable sessions must retain native rendering.");
harmony.UnpatchAll(harmony.Id);Console.WriteLine("PASS|HeadlessVisualTests|checks=10");
