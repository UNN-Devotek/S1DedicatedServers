using System.Reflection;
using DedicatedServerMod.Client.Managers;
using HarmonyLib;
#if IL2CPP
using Game = Il2CppScheduleOne;
using Finder = Il2CppFishNet.InstanceFinder;
#else
using Game = ScheduleOne;
using Finder = FishNet.InstanceFinder;
#endif
int checks=0; var failures=new List<string>();
var harmony=new Harmony("s1ds.tests.beta-runtime");
foreach(var type in Assembly.GetExecutingAssembly().GetTypes().Where(t=>t.GetCustomAttributes(typeof(HarmonyPatch),false).Length>0))
 harmony.CreateClassProcessor(type).Patch();
bool beta=
#if GAME_BETA
true;
#else
false;
#endif
var loading=new Game.UI.LoadingScreen();
DedicatedServerMod.Client.Core.ClientBootstrap.Instance.ConnectionManager.ShouldBlockLoadingScreenClose=true;
loading.Close(); Check(loading.IsOpen && loading.Closures==0,"Verification must keep the loading screen open.");
DedicatedServerMod.Client.Core.ClientBootstrap.Instance.ConnectionManager.ShouldBlockLoadingScreenClose=false;
loading.Close();loading.Close();Check(loading.Closures==1 && loading.InvalidPops==0,"Duplicate Close must not pop a missing loading state.");
var server=new Game.EntityFramework.BuildableItem{IsServerInitialized=true};
var client=new Game.EntityFramework.BuildableItem{DeliverToServer=server.RpcLogic___Destroy_Server_2166136261};
client.PickupItem();
Check(client.Requests==1 && client.InventoryCredits==1 && server.AuthoritativeDespawns==1,"Pickup must transmit one request, credit inventory once and remove the authoritative item.");
Check(client.PredictedDespawns==(beta?0:1),"Beta client must not predict despawn of a buildable child.");
var flee=new Game.NPCs.Behaviour.FleeBehaviour();flee.StartFlee();
Check(flee.DestinationRequests==(beta?0:1),"Beta client must not choose the server's flee destination.");
Check(flee.Animations==1 && flee.SpeedChanges==1,"Flee presentation must still activate.");
flee.IsServerInitialized=true;flee.StartFlee();Check(flee.DestinationRequests==(beta?1:2),"Server flee pathfinding must remain active.");
var ghost=new Game.PlayerScripts.Player{OwnerId=0};ghost.OnStartClient();
Check(!ghost.Rendered && !ghost.Avatar.Visible,"Native post-spawn visibility must not reveal the ghost.");
ghost.Avatar.SetVisible(true);ghost.ThirdPersonMeshesVisibleToLocalPlayer=true;ghost.ApplyThirdPersonMeshVisibility();
Check(!ghost.Rendered && !ghost.Avatar.Visible,"Later appearance changes must not reveal the ghost.");
foreach(var player in new[]{new Game.PlayerScripts.Player{OwnerId=1},new Game.PlayerScripts.Player{OwnerId=0,IsOwner=true}}) {
 player.Avatar.SetVisible(true);player.ThirdPersonMeshesVisibleToLocalPlayer=true;player.ApplyThirdPersonMeshVisibility();
 Check(player.Rendered && player.Avatar.Visible,"Real/local player visibility must remain native.");
}
Finder.IsServer=true;ghost.ThirdPersonMeshesVisibleToLocalPlayer=true;ghost.ApplyThirdPersonMeshVisibility();
Check(ghost.Rendered,"Host/server visibility must remain native.");Finder.IsServer=false;
var assigned=Guid.NewGuid();var quest=new Game.Quests.Quest{GUID=assigned};quest.Start();
Check(beta?quest.GUID==assigned:quest.GUID!=assigned,"Beta Quest.Start must preserve the already assigned network GUID.");
Check(quest.NativeStarts==1,"Native quest initialization must still run.");
var authored=Guid.NewGuid();quest=new(){GUID=assigned,StaticGUID=authored.ToString()};quest.Start();Check(quest.GUID==authored,"Valid authored quest GUID must win.");
quest=new();quest.Start();Check(quest.GUID!=Guid.Empty,"Unassigned quest must retain native GUID generation.");
ClientConnectionManager.IsDedicatedServerSessionActive=false;
loading=new();loading.Close();loading.Close();Check(loading.InvalidPops==1,"Ordinary loading behavior must remain native.");
client=new();client.PickupItem();Check(client.PredictedDespawns==1,"Ordinary pickup behavior must remain native.");
flee=new();flee.StartFlee();Check(flee.DestinationRequests==1,"Ordinary flee behavior must remain native.");
ghost.ThirdPersonMeshesVisibleToLocalPlayer=true;ghost.Avatar.SetVisible(true);ghost.ApplyThirdPersonMeshVisibility();
Check(ghost.Rendered && ghost.Avatar.Visible,"Ordinary host presentation must remain native.");
quest=new(){GUID=assigned};quest.Start();Check(quest.GUID!=assigned,"Ordinary quest initialization must remain native.");
harmony.UnpatchAll(harmony.Id);
foreach(var failure in failures)Console.WriteLine("FAIL|"+failure);
if(failures.Count>0)throw new Exception($"{failures.Count} of {checks} checks failed.");
Console.WriteLine($"PASS|BetaRuntimeTests|checks={checks}|beta={beta}");
void Check(bool value,string message){checks++;if(!value)failures.Add(message);}
