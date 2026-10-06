using System.Reflection;
using HarmonyLib;
#if IL2CPP
using Game = Il2CppScheduleOne;
using Finder = Il2CppFishNet.InstanceFinder;
#else
using Game = ScheduleOne;
using Finder = FishNet.InstanceFinder;
#endif
var harmony=new Harmony("s1ds.tests.clock-sleep");
foreach(var t in Assembly.GetExecutingAssembly().GetTypes().Where(t=>t.GetCustomAttributes(typeof(HarmonyPatch),false).Length>0))harmony.CreateClassProcessor(t).Patch();
var failures=new List<string>();int checks=0;
void Check(bool c,string s){checks++;if(!c)failures.Add(s);}
var clock=new Game.GameTime.TimeManager();var loop=new Game.GameTime.TimeManager._TimeLoop_d__1(clock);
for(int i=0;i<4;i++){if(i>0&&loop.__2__current is UnityEngine.WaitForEndOfFrame)break;loop.MoveNext();}
Check(clock.Minutes==3,"Headless clock must resume without a rendered frame.");
Check(clock.Notifications==3&&clock.PlantGrowth>.029f,"Native minute notifications and growth must remain active.");
var tick=new Game.GameTime.TimeManager._TickLoop_d__2(clock);tick.MoveNext();Check(tick.__2__current==null,"Tick loop must avoid a render-only yield.");
clock.Paused=true;loop.MoveNext();Check(clock.Minutes==3,"Native pause must remain effective.");
var summary=new Game.UI.DailySummary();var rank=new Game.UI.RankUpCanvas();summary.StartEvent();rank.StartEvent();
#if GAME_BETA
Check(!summary.IsInProgress&&!rank.IsInProgress,"Headless beta summary events must complete without UI input.");
Check(summary.UiOpens==0&&rank.UiOpens==0,"Headless sleep must not open server-local panels.");
#else
Check(summary.IsInProgress&&rank.IsInProgress,"Public sleep behavior must remain native.");
#endif
summary.ClearStats();Check(summary.StatsClears==1,"Native summary-stat clearing must remain available.");
UnityEngine.Application.isBatchMode=false;
summary=new();rank=new();summary.StartEvent();rank.StartEvent();Check(summary.IsInProgress&&rank.IsInProgress&&summary.UiOpens==0,"Existing server summary Open policy remains unchanged outside batch mode.");
loop=new(clock);loop.MoveNext();Check(loop.__2__current is UnityEngine.WaitForEndOfFrame,"Rendered server clocks must preserve native yields.");
Finder.IsServer=false;UnityEngine.Application.isBatchMode=true;
summary=new();rank=new();summary.StartEvent();rank.StartEvent();Check(summary.IsInProgress&&rank.IsInProgress,"Non-server events must remain native.");
loop=new(clock);loop.MoveNext();Check(loop.__2__current is UnityEngine.WaitForEndOfFrame,"Non-server clock must remain native.");
foreach(string s in failures)Console.WriteLine("FAIL|"+s);
Console.WriteLine($"HeadlessClockSleepTests: {checks-failures.Count}/{checks} passed");return failures.Count==0?0:1;
