# Beta runtime regression checks

Run with `dotnet run --project tests/BetaRuntimeTests -p:RuntimeDefine=IL2CPP -p:BranchDefine=GAME_BETA`.
Repeat with `MONO` and `GAME_PUBLIC` to check compile guards and ordinary-session behavior.

The checks apply the production Harmony patches to managed doubles of the native methods. They reproduce the beta 0.4.7f9 call order observed in GameAssembly: spawn events precede third-person mesh visibility; a RunLocally destroy RPC body attempts client despawn; flee activation calls destination selection before presentation; and a runtime quest is assigned its GUID before Start reads StaticGUID. The RPC wrapper remains unpatched so transmission and server removal are exercised separately.

These checks validate patch registration, side/session guards and behavior. They do not replace a two-client game test of inventory, authoritative removal, movement animation or saved quest progression.
