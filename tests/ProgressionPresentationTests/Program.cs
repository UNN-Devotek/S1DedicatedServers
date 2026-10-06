using HarmonyLib;
using DedicatedServerMod.Client.Managers;
using UnityEngine.InputSystem;
#if IL2CPP
using Game=Il2CppScheduleOne;
using Finder=Il2CppFishNet.InstanceFinder;
#else
using Game=ScheduleOne;
using Finder=FishNet.InstanceFinder;
#endif
var harmony=new Harmony("tests.progression-presentation");harmony.PatchAll(typeof(Program).Assembly);
var failures=new List<string>();int checks=0;
#if GAME_BETA
const bool beta=true;
#else
const bool beta=false;
#endif
var friend=new Game.NPCs.NPC();var npc=new Game.NPCs.NPC();
var customer=new Game.Economy.Customer{NPC=npc,Friend=friend};npc.Customer=customer;
Game.NPCs.NPCManager.NPCRegistry.Add(npc);Game.NPCs.NPCManager.NPCRegistry.Add(friend);Game.NPCs.NPCManager.NPCRegistry.Add(null);
customer.UpdatePotentialCustomerPoI();friend.RelationData.Unlocked=true;
var map=new Game.UI.Phone.Map.MapApp();map.SetOpen(true);
Check(customer.Marker,"Opening the map must reflect an unlocked friend even if its initial replication missed callbacks.");
Check(map.NativeOpens==1 && !npc.RelationData.Unlocked,"Refresh must preserve native map opening and customer unlock state.");
npc.RelationData.Unlocked=true;map.SetOpen(true);Check(!customer.Marker,"An unlocked customer must lose its potential-customer marker.");
int refreshes=customer.Refreshes;map.SetOpen(false);Check(customer.Refreshes==refreshes,"Closing the map must not scan customers.");
Finder.IsServer=true;map.SetOpen(true);Check(customer.Refreshes==refreshes,"Host/server map behavior must remain native.");Finder.IsServer=false;
ClientConnectionManager.IsDedicatedServerSessionActive=false;map.SetOpen(true);Check(customer.Refreshes==refreshes,"Ordinary sessions must retain native markers.");ClientConnectionManager.IsDedicatedServerSessionActive=true;
var actions=new InputActionMap();var fill=new InputAction{name="FillContainer",actionMap=actions,bindings=new(){"<Gamepad>/rightStick/x"}};
var click=new InputAction{name="PrimaryClick",actionMap=actions,bindings=new(){"<Mouse>/leftButton","<Gamepad>/rightTrigger"}};actions.actions.Add(fill);actions.actions.Add(click);
var reference=InputActionReference.Create(fill);var descriptor=new Game.UI.Input.InputPromptsDescriptorData{name="Descriptor_FillContainer"};descriptor.Actions.Add(reference);
var prompts=new Game.UI.Input.InputPromptsManager();prompts.GetPromptBindingsForCurrentControlScheme(descriptor,out var bindings);
Check(beta?bindings.SequenceEqual(new[]{"<Mouse>/leftButton"}):bindings.Count==0,"Beta tap hints must use the existing mouse click binding.");
Check(descriptor.Actions.Count==1 && descriptor.Actions[0]==reference && fill.bindings.Count==1,"Original assets and controls must remain unchanged.");
Check(!beta||prompts.Received.Destroyed,"The temporary descriptor must be released after rendering bindings.");
Game.GameInput.Gamepad=true;prompts.GetPromptBindingsForCurrentControlScheme(descriptor,out bindings);Check(bindings.SequenceEqual(new[]{"<Gamepad>/rightStick/x"}),"Gamepad prompts must retain their native axis.");Game.GameInput.Gamepad=false;
click.bindings[0]="<Keyboard>/e";prompts.GetPromptBindingsForCurrentControlScheme(descriptor,out bindings);Check(!beta||bindings.SequenceEqual(new[]{"<Keyboard>/e"}),"Hint must respect click rebinding.");
fill.bindings.Add("<Mouse>/delta");prompts.GetPromptBindingsForCurrentControlScheme(descriptor,out bindings);Check(bindings.SequenceEqual(new[]{"<Mouse>/delta"}),"A beta version with a valid mouse fill binding must remain native.");fill.bindings.RemoveAt(1);
click.bindings.Clear();prompts.GetPromptBindingsForCurrentControlScheme(descriptor,out bindings);Check(bindings.Count==0,"A deliberately unbound click must not receive invented bindings.");click.bindings.Add("<Mouse>/leftButton");
descriptor.name="Descriptor_Other";prompts.GetPromptBindingsForCurrentControlScheme(descriptor,out bindings);Check(bindings.Count==0,"Unrelated descriptors must remain native.");descriptor.name="Descriptor_FillContainer";
prompts.Throw=true;try{prompts.GetPromptBindingsForCurrentControlScheme(descriptor,out bindings);Check(false,"Native errors must propagate.");}catch(InvalidOperationException){Check(!beta||prompts.Received.Destroyed,"Temporary assets must be released when native rendering fails.");}prompts.Throw=false;
ClientConnectionManager.IsDedicatedServerSessionActive=false;prompts.GetPromptBindingsForCurrentControlScheme(descriptor,out bindings);Check(bindings.Count==0,"Ordinary beta games must remain native.");
harmony.UnpatchAll(harmony.Id);
foreach(var failure in failures)Console.WriteLine("FAIL|"+failure);
if(failures.Count>0)throw new Exception($"{failures.Count} of {checks} checks failed.");
Console.WriteLine($"PASS|ProgressionPresentationTests|checks={checks}|beta={beta}");
void Check(bool value,string message){checks++;if(!value)failures.Add(message);}
