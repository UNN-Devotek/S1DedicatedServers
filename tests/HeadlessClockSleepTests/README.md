# Headless clock and beta sleep regressions

Run with `dotnet run --project tests/HeadlessClockSleepTests -p:RuntimeDefine=MONO -p:BranchDefine=GAME_BETA`; repeat for IL2CPP and GAME_PUBLIC.

The managed reproduction models the native beta DailySummary.StartEvent transition: set IsInProgress, then Open. The existing server Open prefix suppresses presentation without completing the event. It also models a clock suspended by a render-end yield in a batch player, including native minute notifications, plant growth and pause behavior. Actual Harmony patches are applied; a native runtime check remains necessary for IL2CPP detours.

Before the repair: 5/11 passed for Mono beta. Afterward: 10/10 public and 11/11 beta for each runtime. Native client panels, summary clearing, rendered-server yields and non-server behavior are checked.
