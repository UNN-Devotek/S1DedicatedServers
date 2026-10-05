# Gameplay console command checks

Run `dotnet run --project tests/GameplayCommandTests -p:RuntimeDefine=MONO` and repeat with `IL2CPP`.
The harness compiles the production commands, target resolver, parser, and permission defaults against game/transport stubs.
It exercises target identity and ambiguity, console versus in-game syntax, unavailable players/managers/transports,
vehicle ownership and placement, invalid transforms, quantities, and item relay payloads.

Full game builds must also compile against the actual public/beta assemblies. The isolated public server test
reached READY and passed ten TCP command registration/validation checks with no human clients.
Actual vehicle visibility, client inventory capacity, delivery, and persistence still need multiplayer verification.
