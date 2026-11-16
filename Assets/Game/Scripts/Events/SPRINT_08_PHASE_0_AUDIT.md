# TOM — Sprint 08 Phase 0: Scripted Movement & Animation Audit

Audit only. No code, prefabs, scenes, or assets were changed. Sprint 06 and Sprint 07 stay closed. No Timeline. No Sprint 09.

Sprint 08 can direct characters through EventRunner if it reuses `PlayerMotor` and `TopDownAnimDriver` instead of building a second controller. The missing piece is a scripted locomotion channel on the existing motor, plus thin EventActions. There is no general NPC mover today. Inactive party members are disabled wholesale, so “Ax turns while Jyn is active” is not free.

## 1. Existing movement architecture

### Playable characters (Jyn / Ax)

| Piece | Path | Role |
|---|---|---|
| Physics root | `CharacterController` on the party-member prefab | Collision, slopes, grounding |
| Locomotion | `Assets/Game/Scripts/Player/PlayerMotor.cs` | Camera-relative XZ move in `Update`, gravity, slopes, roll, attack lunge, knockback |
| Input | `PlayerInput` → `PlayerMotor.OnMove` | Stick → `rawInput` → `moveInput` |
| Jump / swim / climb | `PlayerJump`, `PlayerSwimming`, `PlayerClimbing` | Own temporary animator and facing locks |
| Party control | `Assets/Game/Scripts/Party/PartyMemberControl.cs` | Active member: motor, CC, combat, animators, renderers on. Dormant: all off |

`PlayerMotor` applies movement only through `controller.Move` in `Update`. Rigidbody, if present, is forced kinematic. Facing is a 2D stick vector (`lastNonZeroFacing` / `LockFacing`), not root yaw. The sprite faces the camera via `BillboardToCamera` on `SpriteBody`; the motor does not rotate the root toward the move direction.

Existing override hooks on `PlayerMotor`:

- `LockMovement(bool)` / `movementLocked` — zeros stick and skips normal horizontal move. Used by combat (attack / roll).
- `LockFacing` / `UnlockFacing` / `SetFacing` — combat, climb, party swap, hit reaction.
- `BeginRoll` / `BeginAttackLunge` / knockback — temporary world-direction movers that still share gravity and slopes.

There is **no** move-to-point, arrive, or scripted-speed API.

When an event sets `seizesPlayerControl`, `GameplayLock` disables `PlayerInput` through `PlayerInputGate`. The motor keeps running. Stick stops updating. The motor does **not** clear a stuck stick by itself. Events today never call `LockMovement`.

### Party / active player

- `Party.Active` / `Party.FindByCharacterId` / `Party.SetActiveByCharacterId` — `Assets/Game/Scripts/Party/Party.cs`
- Identity asset — `Assets/Game/Scripts/Party/CharacterDefinition.cs` (`id`, `displayName`)
- Runtime link — `PlayerHealth.character`

Dormant members are snapped onto the active character and rendered off. Their `CharacterController` and `PlayerMotor` are disabled. A scripted walk of a dormant Ax cannot use the motor until control (or a new narrative-visible mode) turns those components back on.

### NPCs

`NpcDialogueHost` (`Assets/Game/Scripts/Dialogue/NPC/NpcDialogueHost.cs`) is dialogue and interaction only. No NPC motor, no pathing, no facing API. TestLab NPC capsules are static.

### Enemies (adjacent only)

`EnemyMotor` (`Assets/Game/Scripts/Enemy/EnemyMotor.cs`) uses `CharacterController` plus an optional `NavMeshAgent` with `updatePosition/Rotation = false`. AI calls `SetMoveDirection` / `SetMoveAndFacing`. That API is combat-shaped and tied to `EnemyAI`. **Do not reuse it for narrative player/NPC walks in Sprint 08.** Leave combat NavMesh alone.

### Implications for Sprint 08

- Reuse `PlayerMotor.Move` gravity and slope path. Do not write a parallel `CharacterController.Move` from an EventAction.
- Prefer world-space markers and straight-line XZ steering. Do not add NavMesh for playable characters without a later, concrete reason.
- Arrival = planar distance ≤ stop radius, with a timeout so a blocked destination cannot run forever.
- Cancel must end the scripted channel and leave gravity/Idle correct.

## 2. Existing animation architecture

| Piece | Path | Role |
|---|---|---|
| Body sprite | child `SpriteBody` + `BillboardToCamera` | Visual facing toward camera |
| Weapon overlay | `WeaponBody` under SpriteBody + `WeaponPerspectiveAlignment` | Combat weapon draw |
| Feet overlay | `FeetBody` / `FeetBodyMirror` / `FeetBodyBootstrap` | Bottom slice of body sprite |
| Locomotion params | `TopDownAnimDriver` on SpriteBody | Every `Update`: `IsMoving`, `MoveX`, `MoveY` from `PlayerMotor` |
| Combat | `PlayerCombatController` | Own triggers/bools; temporarily `LockMovement` / `LockFacing` |
| Equipment | `PlayerEquipment` | May swap `runtimeAnimatorController` when the weapon changes |

`TopDownAnimDriver` (`Assets/Game/Scripts/Player/TopDownAnimDriver.cs`) owns locomotion presentation. Climbing yields to `PlayerClimbing`. Air and roll keep a locked facing. `IsMoving` is true only when stick input exists and the character is not rolling — **not** when the body is moved by lunge/knockback alone.

Eight-direction presentation comes from `MoveX` / `MoveY` blend trees, fed by the motor’s 2D facing (optionally snapped in the motor). Root transform yaw is not the facing source.

There is no shared “play clip then restore” service. One-shot combat uses Animator triggers. Driving arbitrary `Animator.Play` from an event will fight combat, climb, swim, and `TopDownAnimDriver` unless those owners are paused or the state is authored to coexist.

### Implications for Sprint 08

- Walk/Run during a scripted move must flow through the same values `TopDownAnimDriver` already reads (scripted move input + facing). Do not set `IsMoving` from the EventAction while the driver is enabled.
- Face-target = `SetFacing` / `LockFacing` with camera-relative 2D from `PlayerMotor.WorldToFacing`.
- One-shot animation = optional, narrow API (state name + timeout). Prefer Idle/Walk via the driver for the first fixture.
- On cancel: end scripted move, `UnlockFacing` if the action locked it, leave the driver running so Idle returns from zero move input.

## 3. EventRunner integration points

Existing, keep as-is:

- `EventRunner` / `EventContext` / `EventAction` / `EventScheduler`
- `ParallelActions` — wait-for-all; one camera-pose writer per group
- `GameplayLock` + `PlayerInputGate`
- Camera actions and `StartDialogueAction`
- Dialogue consequences with `EventContext(null)` — camera actions already `RequestStop` there; character moves that need a live body must tolerate null runner or reject cleanly

Proposed action shapes (names conceptual until implementation):

| Action | Behavior | Cancel |
|---|---|---|
| Move character | Steer target to point/marker at speed; wait until arrive or timeout | Stop scripted channel; leave character where it is |
| Face character / direction | Set/lock facing toward target or world vector; optional short settle | Unlock facing if this action locked it |
| Play animation | Optional CrossFade/Play by state name; wait or fire-and-forget | Clear one-shot ownership; do not leave a stuck trigger |

Parallel:

- Move + camera blend: allowed (move is not a camera-pose writer).
- Move + dialogue: allowed if the mover does not require `ctx.Runner` beyond logging; player stays locked by the runner’s `seizesPlayerControl`.
- Two moves of the **same** character in one parallel group: reject (one locomotion owner).
- Two moves of **different** characters: allowed if both targets can actually move (see dormant party risk).

Cleanup: EventRunner already releases GameplayLock and the camera lease in `CleanupAfterRun`. Scripted locomotion must also end in action `Cancel` and in a `finally` if the action owns a channel, matching camera-move cancel patterns.

## 4. Character reference strategy

Do **not** add a global character registry. Existing identity is enough for party members:

1. **Active player** — `Party.Active` / `Party.ActiveTransform`.
2. **Explicit scene reference** — `PlayerHealth` or `Transform` field on the action.
3. **Character id** — `Party.FindByCharacterId(id)` using `CharacterDefinition.id` (`jyn`, `ax`, …).

For NPCs without `PlayerHealth`, use an explicit `Transform` (and later a small mover component on that object). Speaker ids on `NpcDialogueHost` are dialogue-only and are not motor handles.

Resolution order for an action: explicit reference → character id → active player (if marked). Fail with `RequestStop` when nothing resolves.

## 5. Proposed Sprint 08 architecture

### A. Already reusable

- `PlayerMotor` gravity, slopes, facing helpers (`FacingToWorld`, `WorldToFacing`, `SetFacing`, `LockFacing`).
- `TopDownAnimDriver` for Idle / Walk presentation.
- `Party` active + id lookup.
- `EventRunner`, `ParallelActions`, `GameplayLock`, Camera Director, Dialogue.
- Scene markers as empty Transforms (same authoring idea as `CameraShotMarker`, without needing a new marker type for v1).

### B. Small adaptations

- **`PlayerMotor` — add a scripted locomotion channel** (REFACTOR, minimal):
  - Begin with world XZ direction or destination, speed, and optional facing mode (face move / locked facing).
  - While active: ignore stick, move through the same `controller.Move` gravity/slope path, expose scripted 2D input so `TopDownAnimDriver` sets `IsMoving`.
  - End on arrive, timeout, cancel, or `OnDisable` (party swap must not leave a dangling channel; `OnEnable` already clears roll/facing locks).
- **`TopDownAnimDriver` — only if needed**: treat scripted move input as “has input” the same way stick does. Prefer implementing that via motor getters so the driver stays unchanged.

### C. New code

- `MoveCharacterAction` — resolve target, begin scripted move to Transform or world point, wait, timeout, cancel.
- `FaceCharacterAction` — resolve target, face another Transform or world direction.
- `PlayCharacterAnimationAction` — narrow; state name + optional wait/timeout; document combat conflict.
- Optional `ScriptedNpcMotor` (or equivalent) on narrative NPCs that are **not** party prefabs: CharacterController + straight-line move + simple MoveX/MoveY driver. Only if the fixture includes a non-player walker.

### D. Defer

- NavMesh for players/NPCs.
- Full dormant-party narrative mode (Ax visible and animated while Jyn is controlled) — needs a `PartyMemberControl` change beyond Sprint 08’s minimum unless the fixture only uses the active player.
- Enemy scripted choreography via `EnemyMotor`.
- Timeline.
- Generic animation timeline scrubbing / root-motion.
- Rewriting `PlayerMotor` slope logic.
- Auto path around dynamic blockers beyond timeout + stop.

### Simplest movement behavior (approved direction)

- Destination: world point or scene Transform.
- Speed: action override or motor `moveSpeed`.
- Stop distance: configurable planar radius.
- Facing: face velocity while moving; optional final face-target.
- Blocked destination: timeout fails or stops with a log; no infinite wait.
- No NavMesh in Sprint 08.

## 6. KEEP / REFACTOR / REPLACE / REMOVE

| Component | Verdict | Reason | Risk |
|---|---|---|---|
| `PlayerMotor` | REFACTOR (minimal) | Only place that safely owns CC move + slopes. Needs a scripted channel. | Wrong priority vs roll/lunge/knockback; party-swap mid-move |
| `TopDownAnimDriver` | KEEP (prefer) / tiny REFACTOR if motor getters are insufficient | Already maps motor → `IsMoving` / `MoveX` / `MoveY` | Fighting climb/combat if bypassed |
| `Party` / `CharacterDefinition` / `PlayerHealth` | KEEP | Identity and active lookup already exist | Id typos |
| `PartyMemberControl` | KEEP for S08 fixture; REFACTOR later for dormant narrative | All-or-nothing control blocks inactive Ax walks | Scope creep if forced now |
| `NpcDialogueHost` | KEEP | Dialogue only; not a mover | Expecting it to walk |
| `EnemyMotor` / `EnemyAI` / NavMesh usage | KEEP / leave untouched | Combat pathing; wrong owner for narrative | Coupling story to AI |
| `GameplayLock` / `PlayerInputGate` / `EventRunner` / `ParallelActions` | KEEP | Correct lock and sequencing model | Assuming lock also freezes the motor (it does not) |
| `CameraDirector` + camera actions | KEEP | Parallel with move is already legal | Second pose writer in the same parallel group |
| `BillboardToCamera` / `WeaponBody` / `FeetBody*` | KEEP | Presentation; do not drive from events | — |
| New second character controller | REMOVE from plan | Would duplicate slopes/gravity and desync anim | High |
| Timeline integration | REMOVE from Sprint 08 | Out of scope | — |

## 7. Files likely created or modified

**Create (expected):**

- `Assets/Game/Scripts/Events/Actions/MoveCharacterAction.cs`
- `Assets/Game/Scripts/Events/Actions/FaceCharacterAction.cs`
- `Assets/Game/Scripts/Events/Actions/PlayCharacterAnimationAction.cs`
- Optional: `Assets/Game/Scripts/Characters/ScriptedNpcMotor.cs` (only if non-player walkers are in scope)
- TestLab menu addition in `EventSystemTestLabBuilder.cs` (implementation phase, not this audit)
- `Assets/Game/Scripts/Events/SPRINT_08_PHASE_*` reports when implementing

**Modify (expected, minimal):**

- `Assets/Game/Scripts/Player/PlayerMotor.cs` — scripted locomotion channel
- Possibly `Assets/Game/Scripts/Player/TopDownAnimDriver.cs` — only if motor surface is not enough
- `Assets/Game/Scripts/Editor/Events/EventRunnerEditor.cs` — register new actions
- Possibly `DialogueConsequenceListDrawer.cs` — only if face/grant-style actions are allowed as consequences

**Do not modify for Sprint 08 unless a fixture proves it is required:**

- `PartyMemberControl.cs`, `EnemyMotor.cs`, `CameraDirector.cs`, `DialogueService.cs`, door scripts, inventory/save from Sprint 07

## 8. Risks and limitations

1. **Dormant party members cannot walk or turn** while `PartyMemberControl` has them uncontrolled. Fixture should use the active player unless a narrative-visible mode is explicitly approved.
2. **`TopDownAnimDriver` ignores non-input motion.** Lunges do not set `IsMoving`. Scripted move must present as input to the driver.
3. **GameplayLock disables input, not the motor.** Scripted move must clear or override stick so a held direction cannot leak after unlock.
4. **Combat and climb own the Animator.** A generic Play-animation action can stick or interrupt attacks. First fixture should rely on Idle/Walk via the driver.
5. **Directed camera changes the camera basis** used by `FacingToWorld` / `WorldToFacing`. Facing during a directed shot stays consistent with gameplay’s camera-relative model, but designers must place markers in world space, not “on screen.”
6. **No NPC motor.** A second walking character that is not a party prefab needs a new thin mover or a temporary player-prefab stand-in.
7. **Cancel does not roll back position.** Same rule as chest rewards: finished motion stays.
8. **Parallel group rules stay.** One camera pose writer. One locomotion owner per character.

## 9. One TestLab QA fixture (proposal only — do not build in this phase)

One pad past `Chest_S7`, interact via `WorldInteractable`, `seizesPlayerControl = true`.

Sequence:

1. Optional short camera blend to a shot of a walk marker.
2. `ParallelActions`: camera hold or second light blend **and** active player `MoveCharacter` to `Marker_S8_Walk`.
3. `FaceCharacter` toward a prop or second marker.
4. Short wait (stand-in for a safe Idle-facing beat; optional one-shot only if a known state exists).
5. `CameraRestore`.
6. Debug log + `Completed`.

Cancel cases:

- Cancel during the move: motion stops, lock releases, no `Completed`, player can walk with input again, Idle correct.
- Full run: arrive, face, restore, `Completed`.

No second pad. No enemy walker. No dormant-Ax requirement in the first fixture.

## 10. Summary recommendation

| Layer | Decision |
|---|---|
| Movement | Extend `PlayerMotor` with scripted locomotion; straight-line to markers; timeout; no NavMesh |
| Animation | Drive Walk/Idle through `TopDownAnimDriver` via the motor; face via existing facing APIs; defer aggressive one-shots |
| Events | New Move / Face / (optional) PlayAnimation actions on the existing runner; use `ParallelActions` with the camera |
| References | Active player + explicit ref + `CharacterDefinition.id`; no new registry |
| NPCs | New thin mover only when a non-player must walk; otherwise markers + active player |
| Out of scope | Timeline, enemy choreography, dormant dual-character staging, inventory/camera rewrites |

Stop. No implementation until this architecture is approved.
