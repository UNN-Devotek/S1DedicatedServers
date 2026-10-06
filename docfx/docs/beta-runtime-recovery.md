# Beta 0.4.7f9 runtime recovery

These changes target Steam beta build 25698382. Public and beta builds keep separate game references.

## Traced failures and fixes

- **[Confirmed] Loopback presentation:** Player.OnStartClient invokes onPlayerSpawned before assigning ThirdPersonMeshesVisibleToLocalPlayer and applying its mesh layer. The existing hide callback runs too early to protect the later layer assignment. The client now forces hidden presentation when that native method runs for a verified ghost, preserving real players and ordinary sessions.
- **[Confirmed] Pickup despawn:** BuildableItem.Destroy_Server is a RunLocally server RPC. Its local generated body calls Despawn, which FishNet rejects for a child NetworkObject. Beta dedicated clients skip that body; the writer still sends the request, inventory pickup still runs, and the server executes destruction and its observer notification.
- **[Confirmed] Flee authority:** FleeBehaviour.StartFlee invokes Flee before animation/speed setup. Flee selects a destination and calls NPCMovement.SetDestination even on remote clients. Beta dedicated clients skip only destination selection, retaining presentation and server pathfinding.
- **[Confirmed] Dead-drop quest identity:** CreateDeaddropCollectionQuest assigns/registers GUID before Quest.Start. Start initializes from StaticGUID, generating a new GUID when it is empty. Beta dedicated peers now copy an already assigned valid GUID into that empty/invalid initialization input while retaining native Start. Authored valid GUIDs remain authoritative.
- **[Confirmed] Loading screen:** Both game load completion and mod verification can call Close. Native Close pops its UI state even when already closed. Dedicated clients now make that duplicate call harmless while retaining the verification hold.
- **[Confirmed] Headless graphics:** The local server's Unity log showed InstancingManager.Start failing to find CSMain and repeated invalid-kernel errors. Fog material initialization also triggered camera capture and a graphics-buffer exception without compute-shader support. Server builds skip GPU instancing initialization/drawing and fog material/render updates; AI, weather simulation, and native resource cleanup remain intact. Optional targets are ignored when absent from the game version.

## Validation

BetaRuntimeTests exercises production Harmony patches against managed reproductions of the observed native call order, for public/beta and Mono/IL2CPP. HeadlessVisualTests checks optional target discovery, GPU avoidance and preservation of cleanup. Both complement actual builds against public 0.4.6f13 and beta 0.4.7f9 references.

Native beta RVA evidence: Player.OnStartClient 0x642E90, ApplyThirdPersonMeshVisibility 0x63B560, BuildableItem destruction body 0x6C3370, Flee 0x954C20, CreateDeaddropCollectionQuest 0xB2BFF0, Quest.Start initializer 0xB34FD0. These addresses are specific to this beta build.

## Remaining gameplay checks

- Rejoin with matching beta client/server mods; confirm only real players appear and customer markers remain correct.
- Pick up a placed item; confirm one inventory item, removal for another player, and removal after save/reload.
- Trigger fleeing; confirm movement/animation on two clients and no client destination warning.
- Create/complete a dead-drop quest and rejoin; confirm its GUID remains stable and progression persists.
- Confirm loading completes and closes cleanly after authentication.

**[Confirmed] Additional warnings remain in the original client log:** missing quest name The Deep End, missing tap input prompt bindings, seed echo warnings, loading-time NPC GUID/region mismatches and NavMesh placement failures. Their initiating game state has not been reproduced with enough evidence for a safe repair. No placeholder quests, fabricated bindings, inventory retries, or global AI disabling are included.

The original onPlayerSpawned-cleanup hypothesis was refuted by the beta native disassembly: LoadManager.CleanUp clears onLocalPlayerSpawned, while onPlayerSpawned resides in a separate static field.
