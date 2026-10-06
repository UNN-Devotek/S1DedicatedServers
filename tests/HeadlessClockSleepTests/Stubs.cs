using System.Runtime.CompilerServices;
#if IL2CPP
using Game = Il2CppScheduleOne;
#else
using Game = ScheduleOne;
#endif
namespace UnityEngine {
 public class Application { public static bool isBatchMode=true; }
 public class Canvas { public bool enabled=true; }
 public class WaitForEndOfFrame
#if IL2CPP
 : Il2CppSystem.Object
#endif
 { }
}
namespace Il2CppSystem { public class Object { public T TryCast<T>() where T:class => this as T; } }
#if IL2CPP
namespace Il2CppFishNet
#else
namespace FishNet
#endif
{ public static class InstanceFinder { public static bool IsServer=true; } }
namespace DedicatedServerMod.Utils { internal static class DebugLog { internal static void Info(string s){} internal static void Warning(string s){} internal static void StartupDebug(string s){} } }
namespace DedicatedServerMod.Server.Game.Patches.Common { internal static class DedicatedServerPatchCommon { internal static bool IsDedicatedHeadlessServer()=>UnityEngine.Application.isBatchMode; } }
namespace DedicatedServerMod.Shared.Networking { internal static class CustomMessaging { internal static void DailySummaryAwakePostfix(object o){} } }
#if IL2CPP
namespace Il2CppScheduleOne.UI
#else
namespace ScheduleOne.UI
#endif
{
 public class DailySummary {
  public bool IsInProgress; public UnityEngine.Canvas Canvas=new(); public int UiOpens,StatsClears,Completions;
  [MethodImpl(MethodImplOptions.NoInlining)] public void Awake(){}
  [MethodImpl(MethodImplOptions.NoInlining)] public void StartEvent(){IsInProgress=true;Open();}
  [MethodImpl(MethodImplOptions.NoInlining)] public void Open(){UiOpens++;}
  public void Close(){IsInProgress=false;Completions++;}
  public void ClearStats(){StatsClears++;}
 }
 public class RankUpCanvas {
  public bool IsInProgress; public int UiOpens;
  [MethodImpl(MethodImplOptions.NoInlining)] public void StartEvent(){IsInProgress=true;UiOpens++;}
 }
}
#if IL2CPP
namespace Il2CppScheduleOne.GameTime
#else
namespace ScheduleOne.GameTime
#endif
{
 public class TimeManager {
  public int Minutes,Ticks; public float PlantGrowth; public bool Paused; public int Notifications;
  public class _TimeLoop_d__1 {
   public object __2__current {get;set;} private readonly TimeManager _owner; private bool _started;
   public _TimeLoop_d__1(TimeManager owner){_owner=owner;}
   [MethodImpl(MethodImplOptions.NoInlining)] public bool MoveNext(){
    if(_started&&!_owner.Paused){_owner.Minutes++;_owner.Notifications++;_owner.PlantGrowth+=.01f;}
    _started=true;__2__current=new UnityEngine.WaitForEndOfFrame();return true;
   }
  }
  public class _TickLoop_d__2 {
   public object __2__current {get;set;} private readonly TimeManager _owner;
   public _TickLoop_d__2(TimeManager owner){_owner=owner;}
   [MethodImpl(MethodImplOptions.NoInlining)] public bool MoveNext(){_owner.Ticks++;__2__current=new UnityEngine.WaitForEndOfFrame();return true;}
  }
 }
}
