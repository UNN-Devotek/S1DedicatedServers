# Beta 0.4.7f9 runtime recovery

These changes target Steam beta build 25698382. Public and beta builds keep separate game references.

## Traced failures and fixes

- **[Confirmed] Loopback presentation:** Player.OnStartClient invokes onPlayerSpawned before assigning ThirdPersonMeshesVisibleToLocalPlayer and applying its mesh layer. The existing hide callback runs too early to protect the later layer assignment. The client now forces hidden presentation when that native method runs for a verified ghost, preserving real players and ordinary sessions.
- **[Confirmed] Pickup despawn:** BuildableItem.Destroy_Server is a RunLocally server RPC. Its local generated body calls Despawn, which FishNet rejects for a child NetworkObject. Beta dedicated clients skip that body; the writer still sends the request, inventory pickup still runs, and the server executes destruction and its observer notification.
- **[Confirmed] Flee authority:** FleeBehaviour.StartFlee invokes Flee before animation/speed setup. Flee selects a destination and calls NPCMovement.SetDestination even on remote clients. Beta dedicated clients skip only destination selection, retaining presentation and server pathfinding.
- **[Confirmed] Dead-drop quest identity:** CreateDeaddropCollectionQuest assigns/registers GUID before Quest.Start. Start initializes from StaticGUID, generating a new GUID when it is empty. Beta dedicated peers now copy an already assigned valid GUID into that empty/invalid initialization input while retaining native Start. Authored valid GUIDs remain authoritative.
- **[Confirmed] Loading screen:** Both game load completion and mod verification can call Close. Native Close pops its UI state even when already closed. Dedicated clients now make that duplicate call harmless while retaining the verification hold.
- **[Confirmed] Headless graphics:** The local server's Unity log showed InstancingManager.Start failing to find CSMain and invalid-kernel errors. A follow-up runtime log identified the continuing per-frame failure specifically in ReflectionProbeManager.UpdateProbes and a separate null texture failure in MaskController.UpdateMaskMap. The next log exposed OptimizedLight.UpdateCull dereferencing a stale transform from PlayerCamera movement callbacks. Fog material initialization also triggered camera capture and a graphics-buffer exception without compute-shader support. Server builds skip GPU instancing initialization/drawing, fog material/render updates, reflection probe kernels/cubemap assignment, weather mask texture updates god-ray render feature creation/passes and camera-driven light culling; AI, weather simulation, weather initialization callbacks and native resource cleanup remain intact. Optional targets are ignored when absent from the game version. Native Unity graphics startup can still report a buffer exception before MelonLoader has applied the patches.

## Validation

BetaRuntimeTests exercises production Harmony patches against managed reproductions of the observed native call order, for public/beta and Mono/IL2CPP. HeadlessVisualTests checks optional target discovery, GPU avoidance and preservation of cleanup. Both complement actual builds against public 0.4.6f13 and beta 0.4.7f9 references.

Native beta RVA evidence: Player.OnStartClient 0x642E90, ApplyThirdPersonMeshVisibility 0x63B560, BuildableItem destruction body 0x6C3370, Flee 0x954C20, CreateDeaddropCollectionQuest 0xB2BFF0, Quest.Start initializer 0xB34FD0. These addresses are specific to this beta build.

## Remaining gameplay checks

- Rejoin with matching beta client/server mods; confirm only real players appear and customer markers remain correct.
- Pick up a placed item; confirm one inventory item, removal for another player, and removal after save/reload.
- Trigger fleeing; confirm movement/animation on two clients and no client destination warning.
- Create/complete a dead-drop quest and rejoin; confirm its GUID remains stable and progression persists.
- Confirm loading completes and closes cleanly after authentication.

## Follow-up presentation repairs (1.1.0-unn.6)

**[Confirmed] Tap hint data:** beta 0.4.7f9's sharedassets0 `Descriptor_FillContainer` references `Generic/FillContainer`, whose only serialized binding is `<Gamepad>/rightStick/x`. The native tap remains usable with a mouse, but its prompt cannot find a mouse binding. Dedicated beta clients temporarily substitute the existing `Generic/PrimaryClick` reference while the native prompt resolver runs. This shows the click used to hold the handle, respects rebinding, leaves gamepad behavior and input controls intact, and releases the temporary objects even when rendering fails. Public builds and ordinary sessions retain native behavior.

**[Deduced] Customer marker reconciliation:** `Customer.SetupPoI` evaluates potential-customer visibility at creation; subsequent visibility relies on connection-unlock callbacks. A relationship already unlocked before subscription produces no new unlock callback. The dedicated client's map now re-evaluates markers with `Customer.UpdatePotentialCustomerPoI` each time it opens. This repairs the stale-marker case without granting customers or regions. ProgressionPresentationTests reproduces that ordering, but the user's original missing-marker symptom still requires an actual join/sample test.

**[Confirmed] Obsolete quest reference:** both beta scenes contain a SystemTrigger referring to `The Deep End`, but Main's quest definitions and the current saved quest list do not contain that quest. It is not a missing GUID for an existing quest. No replacement quest, title alias, or forced completion is introduced.

**[Confirmed] Region requirements:** `MapRegionData.RankRequirement` gates regions; `Customer.SampleConsumed` awards XP for a qualifying sample. Receiving XP or unlocking one customer does not necessarily meet the next region's requirement. The local test world currently has rank 0, tier 1, total XP 100 and Northtown unlocked. A sample/region failure must be checked against the actual rank and NPC state.

Use `worlddiagnostics`, `worlddiagnostics <npc name>`, and `worlddiagnostics quests` from the server console to capture rank/XP, relationships, mutual connections, conscious active agents off the NavMesh, ragdoll state and quest GUIDs. The command is read-only, uses normal console-command permissions, and refuses to inspect a loading world. Inactive template NavMesh warnings can be distinguished from a stranded active cop at runtime.

ProgressionPresentationTests applies the production Harmony patches for Mono/IL2CPP and public/beta, covering late relationship replication, unlocked customers, close/ordinary/server guards, gamepad hints, rebinding, genuinely unbound controls, unrelated descriptors and exception cleanup.

**[Confirmed] Additional warnings remain in the original client log:** seed echo warnings, loading-time NPC GUID/region mismatches and NavMesh placement failures. Their initiating game state has not been reproduced with enough evidence for a safe repair. No inventory retries or global AI disabling are included. Bare coroutine failures and quest audio warnings also need a gameplay reproduction with a useful native stack.

The original onPlayerSpawned-cleanup hypothesis was refuted by the beta native disassembly: LoadManager.CleanUp clears onLocalPlayerSpawned, while onPlayerSpawned resides in a separate static field.
