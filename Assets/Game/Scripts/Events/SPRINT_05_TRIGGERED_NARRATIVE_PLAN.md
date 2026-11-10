# Sprint 05 — Phase A Addition: Triggered Narrative Dialogue

**Status:** Phase A **implemented** — see `SPRINT_05_TRIGGERED_NARRATIVE_QA.md`.  
**Scope:** Phase A addition only. Do **not** start Phase B/C.  
**Principle:** Separate **trigger source**, **presentation mode**, and **gameplay lock policy**.

---

## 1. Scenario

A playable character (Jyn / Ax) enters a **trigger volume** and automatically speaks a narrative line.

| Is | Is not |
|----|--------|
| Interactive-**style** presentation (large comic chatbox, speaker, optional portrait, typewriter, Confirm/Cancel/FF) | Ambient NPC muttering |
| Started by **proximity trigger** (no Interact) | A second dialogue engine |
| Optional `lockGameplay = false` (talk while moving) | Assumed blocking just because style is interactive |

---

## 2. Audit (current code)

### What already works

| Piece | Fit |
|-------|-----|
| `DialogueService.TryStartInteractive` | Starts `DialogueSession` with presentation we want |
| `DialogueAsset.lockGameplay` + override arg | Blocking vs non-blocking already supported |
| `DialogueSession` + conversation layout | Chatbox follows speaker Transform live |
| `DialogueInput` | Works even when PlayerInput is locked |
| `DialogueCharacterAssetResolver` | Reusable per-character assets |
| `active_player` resolution | Freezes id + Transform at session start |

### Gaps

| Gap | Detail |
|-----|--------|
| No trigger adapter | Only `NpcDialogueHost.TryInteract` / ambient proximity start dialogue today |
| Session identity = `Party.Active` | Trigger should freeze the **entering** character (usually Active, but must be explicit) |
| Retrigger policy | Hosts have Once/Repeat/cooldown for ambient; interactive Interact is press-based — need enter/exit latch for volumes |
| “While inside, fire every frame” | Must gate with “armed until OnTriggerExit” |

### Camera

- Dialogue does **not** call Cinemachine APIs.
- When `lockGameplay = true`, session acquires `GameplayLockChannels.PlayerControl` → PlayerInput disabled → motor stops → **follow camera stops because the target stopped**, not because dialogue mutes the camera.
- When `lockGameplay = false`, movement continues → camera should keep following.
- **Proposal:** no Cinemachine/camera code changes unless QA proves a separate issue.

---

## 3. Conceptual split (required)

```text
Trigger source     → Interact | Trigger volume | Proximity | EventRunner (later)
Presentation mode  → Ambient-style | Interactive-style
Lock policy        → Blocking (lockGameplay true) | Non-blocking (false)
```

Triggered narrative = **Trigger volume** + **Interactive-style** + **Blocking OR Non-blocking**.

---

## 4. Smallest compatible design

### 4.1 New thin adapter (only new “feature” type)

`NarrativeDialogueTrigger` : `MonoBehaviour`

Inspector (minimal):

```text
dialogueAsset                    // shared / fallback
dialogueByCharacter[]            // optional — reuse DialogueCharacterAssetOverride + Resolver
lockGameplayOverride             // optional override of asset.lockGameplay (Nullable-style or enum UseAsset/ForceOn/ForceOff)
allowCancel                      // default true
once / repeat                    // enum
retriggerCooldownSeconds
requirePartyMember               // true: only PlayerHealth / party colliders
useEnteringCharacterAsActivePlayer  // true (default): freeze session to who entered
manualOffset / preferred / scale // optional, same as host
```

Runtime:

1. `OnTriggerEnter` → resolve entering `PlayerHealth` (party).  
2. If already fired and Once → ignore.  
3. If inside latch active / cooldown → ignore.  
4. If `DialogueService.HasInteractive` → ignore (or queue — prefer ignore to avoid stealing).  
5. Resolve asset via `DialogueCharacterAssetResolver` with **entering** `character.id`.  
6. `TryStartInteractive(..., lockOverride, initiatorCharacterId, initiatorTransform)`.  
7. Set latch “consumed until exit”; start cooldown clock on finish or on enter (recommend: cooldown starts when session **ends**).  
8. `OnTriggerExit` → clear latch so Repeat can fire next enter.

**Do not** invent a scheduler, EventRunner, or second `DialogueService`.

### 4.2 Tiny API extension (reuse, don’t fork)

Extend `TryStartInteractive` / `DialogueSession` with optional initiator:

```text
string sessionCharacterIdOverride = null
Transform sessionTransformOverride = null
```

- If provided → `CaptureSessionActiveIdentity` uses override (for `active_player` + layout anchor).  
- If null → current `Party.Active` behavior (NPC Interact unchanged).

NpcAnchor for NPC lines in a narrative-only asset can be null / player — most narrative lines will be `active_player` only.

### 4.3 Presentation & layout

- Same interactive prefab path, conversation plan, follow slots.  
- Non-blocking: player moves → Transform moves → chatbox follows (already).  
- No change to ambient presentation path.

### 4.4 WorldState / Event System

- Phase A: local Once/Repeat/cooldown on the component is enough.  
- Optional later: `WorldState` flag `dialogue.{id}.narrativeSeen` for save-safe Once (Phase C / polish) — **not required** for first pass if TestLab-only Once is acceptable.  
- EventRunner `StartDialogueAction` remains Phase C; this trigger is the Phase A stopgap that proves the three-way split.

---

## 5. Files (when authorized)

| File | Change |
|------|--------|
| `Dialogue/NPC/NarrativeDialogueTrigger.cs` | **New** — trigger adapter |
| `Dialogue/Runtime/DialogueService.cs` | Optional initiator overrides on `TryStartInteractive` |
| `Dialogue/Runtime/DialogueSession.cs` | Honor initiator override in `CaptureSessionActiveIdentity` |
| `Editor/.../EventSystemTestLabBuilder.cs` | Fixture volume + narrative asset (Jyn/Ax) |
| `Events/SPRINT_05_TRIGGERED_NARRATIVE_QA.md` | Checklist |
| Plan/report links | Cross-link from `SPRINT_05_PLAN.md` |

**Out of scope:** Cinemachine edits, ambient redesign, Phase B choices/variants, Phase C EventRunner action.

---

## 6. QA (post-implementation)

| # | Case | Expect |
|---|------|--------|
| 1 | Jyn enters volume | Auto-speaks as Jyn, no Interact |
| 2 | Ax enters same volume | Resolves as Ax |
| 3 | No Interact | Session starts on enter only |
| 4 | Non-blocking | Move + camera follow while talking |
| 5 | Chatbox follow | Smooth follow, no slot jump |
| 6 | Blocking | Movement stops; restores after end/cancel |
| 7 | Once / Repeat / cooldown | Correct |
| 8 | Stay inside | No per-frame retrigger |
| 9 | Regression | Ambient NPC + Interact NPC still work |

---

## 7. Risks & mitigations

| Risk | Mitigation |
|------|------------|
| Trigger fires while another interactive runs | Reject start (`HasInteractive`) |
| Stay inside after Once | Latch until exit + Once flag |
| Entering character ≠ Party.Active | Initiator override |
| Camera “stops” in blocking mode | Document as lock→motor stop; no camera patch unless proven |
| Collider hits non-player | Filter `PlayerHealth` / party only |

---

## 8. Gate

Await explicit approval → implement this addition only → stop for manual QA.  
Do not start Phase B or C.
