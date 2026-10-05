Regression checks apply the production building-entry patch with real Harmony.
MONO supplies an inherited non-public npc field; IL2CPP supplies the generated
npc property and a throwing getter. Dedicated guards skip invalid object graphs
and let healthy NPCs enter. Ordinary sessions preserve native behavior.

dotnet run --project tests/NpcBuildingEntryTests -p:RuntimeDefine=MONO
dotnet run --project tests/NpcBuildingEntryTests -p:RuntimeDefine=IL2CPP

Use .NET 8 for the Harmony detour checks. These managed doubles verify patch
registration and validation, not the game's asynchronous animation coroutine.
