using System.Runtime.CompilerServices;
namespace DedicatedServerMod.Client.Managers { internal static class ClientConnectionManager { internal static bool IsDedicatedServerSessionActive = true; } }
namespace DedicatedServerMod.Utils { internal static class DebugLog { internal static void Debug(string value) { } } }
namespace UnityEngine {
 public class Object {
  public bool Destroyed; public string name;
  public static T Instantiate<T>(T original) where T:Object => (T)original.MemberwiseClone();
  public static void Destroy(Object value) { if(value!=null)value.Destroyed=true; }
 }
}
namespace UnityEngine.InputSystem {
 public struct InputBinding {
  public string effectivePath;
  public static implicit operator InputBinding(string path)=>new(){effectivePath=path};
 }
 public sealed class InputAction {
  public string name; public InputActionMap actionMap; public List<InputBinding> bindings = new();
 }
 public sealed class InputActionMap {
  public string name="Generic"; public List<InputAction> actions=new();
  public InputAction FindAction(string name,bool throwIfNotFound=false) => actions.Find(a=>a.name==name);
 }
 public sealed class InputActionReference:UnityEngine.Object {
  public InputAction action;
  public static InputActionReference Create(InputAction action)=>new(){action=action};
 }
}
#if IL2CPP
namespace Il2CppSystem.Collections.Generic { public class List<T>:System.Collections.Generic.List<T> { } }
namespace Il2CppFishNet { public static class InstanceFinder { public static bool IsServer; } }
namespace Il2CppScheduleOne {
#else
namespace FishNet { public static class InstanceFinder { public static bool IsServer; } }
namespace ScheduleOne {
#endif
 public static class GameInput { public static bool Gamepad; public static bool GetCurrentInputDeviceIsGamepad()=>Gamepad; }
 namespace NPCs {
  public sealed class NPC {
   public Economy.Customer Customer; public RelationData RelationData=new(); public bool IsActive=true;
   public T GetComponent<T>() where T:class => Customer as T;
  }
  public sealed class RelationData { public bool Unlocked; }
  public static class NPCManager { public static List<NPC> NPCRegistry=new(); }
 }
 namespace Economy {
  public sealed class Customer {
   public NPCs.NPC NPC; public NPCs.NPC Friend; public bool Marker; public int Refreshes;
   [MethodImpl(MethodImplOptions.NoInlining)] public void UpdatePotentialCustomerPoI() {
    Refreshes++; Marker=!NPC.RelationData.Unlocked && (Friend?.RelationData.Unlocked??false);
   }
  }
 }
 namespace UI.Phone.Map {
  public sealed class MapApp { public int NativeOpens;
   [MethodImpl(MethodImplOptions.NoInlining)] public void SetOpen(bool open) { NativeOpens++; }
  }
 }
 namespace UI.Input {
  public sealed class InputPromptsDescriptorData:UnityEngine.Object {
#if IL2CPP
   public Il2CppSystem.Collections.Generic.List<UnityEngine.InputSystem.InputActionReference> Actions=new();
#else
   public List<UnityEngine.InputSystem.InputActionReference> Actions=new();
#endif
  }
  public sealed class InputPromptsManager {
   public InputPromptsDescriptorData Received; public int Warnings; public bool Throw;
   public bool HasCorrectControlScheme(string effectivePath)=>GameInput.Gamepad?effectivePath.StartsWith("<Gamepad>"):effectivePath.StartsWith("<Mouse>")||effectivePath.StartsWith("<Keyboard>");
   [MethodImpl(MethodImplOptions.NoInlining)] public void GetPromptBindingsForCurrentControlScheme(InputPromptsDescriptorData descriptor,
#if IL2CPP
    out Il2CppSystem.Collections.Generic.List<string> bindingDisplayStrings
#else
    out List<string> bindingDisplayStrings
#endif
   ) {
    Received=descriptor;
    if(Throw)throw new InvalidOperationException("Native prompt failure");
    bindingDisplayStrings=new();
    foreach(var reference in descriptor.Actions)foreach(var binding in reference.action.bindings)if(HasCorrectControlScheme(binding.effectivePath))bindingDisplayStrings.Add(binding.effectivePath);
    if(bindingDisplayStrings.Count==0)Warnings++;
   }
  }
 }
}
