Run with .NET 8 (or a compatible runtime):

  dotnet run --project tests/PoliceRecoveryTests -p:RuntimeDefine=MONO
  dotnet run --project tests/PoliceRecoveryTests -p:RuntimeDefine=IL2CPP

If only a newer .NET runtime is installed on Linux, prefix these commands with
DOTNET_ROLL_FORWARD=Major.

The harness compiles the actual three production patch files against small
managed doubles. It verifies target cleanup, transition failures, normal update
behaviour, exception propagation, eight-second eligibility, and finite-pose
validation. Before the police changes, 18 of the original 27 cases failed.
The additional null-player sweep case checks all three concrete police types
and preserves valid pursuits.

These checks do not emulate native Unity physics, the IL2CPP runtime, or FishNet
RPC delivery. Full builds against real game assemblies and a multiplayer
knockdown/recovery test are also needed before claiming sliding is resolved.

In-game checks on an isolated saved world:
- With two clients, knock down the same officer, allow recovery, and compare
  movement/animation and finite collider state across both clients.
- Disconnect a pursued player and verify enabled pursuit/body-search/vehicle
  behaviours clear without repeated activation exceptions.
- Confirm normal pursuit of a valid player still works.
- Confirm dead and knocked-out NPCs are not forced to stand up.
- Verify save/reload and reconnect preserve progress.

Only server code changes in this patch set. Existing stand-up animation caching
is unchanged because its role in the observed sliding remains unproven.

The IL2CPP alias run also checks the native Il2CppException wrapper used in the
captured server errors: a formatted System.NullReferenceException during police
activation is handled, while other native exception types still propagate.
Current case counts: MONO=28; IL2CPP=30.
