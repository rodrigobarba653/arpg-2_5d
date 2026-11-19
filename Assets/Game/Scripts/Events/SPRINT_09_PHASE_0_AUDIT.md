# Sprint 09 — Phase 0 Audit (Architecture & Editor ONLY)

Sprints 05–08 CLOSED. Sin código de implementación en esta fase. **AUDIT ONLY — esperando aprobación.**

---

## 1. Current architecture summary

| Layer | Role | Paths |
|--------|------|--------|
| **EventRunner** | Orquestador: condiciones → secuencia `SerializeReference` → complete/cancel | `Events/EventRunner.cs` |
| **EventAction / EventCondition** | Base + `Cancel` opcional | `Events/EventAction.cs`, `Events/EventCondition.cs` |
| **EventContext** | Token, `RequestStop`, log | `Events/EventContext.cs` |
| **EventScheduler** | ≤1 player-control; env ilimitados | `Events/EventScheduler.cs` |
| **GameplayLock** | Input/interact/menu/… | `Events/GameplayLock.cs` |
| **ParallelActions** | Wait-for-all; ≤1 camera pose; ≤1 move/body; no nest | `Events/Actions/ParallelActions.cs` |
| **CameraDirector** | Lease por runner; restore en `NotifyRunnerFinished` | `Camera/CameraDirector.cs` |
| **Dialogue** | `StartDialogueAction` → `DialogueService` / `DialogueSession` | `Events/Actions/StartDialogueAction.cs`, `Dialogue/Runtime/*` |
| **Movement / anim** | `PlayerMotor` scripted + `ScriptedNpcMotor` | `Player/PlayerMotor.cs`, `Characters/ScriptedNpcMotor.cs` |
| **Triggers** | `WorldInteractable`, `EventSourceTrigger`, `EventSourceInteract` | `Interaction/`, `Events/Sources/` |
| **Editor** | `EventRunnerEditor`, `ParallelActionsDrawer`, consequence drawer, TestLab builder | `Editor/Events/*`, `Editor/Dialogue/DialogueConsequenceListDrawer.cs` |

**Packages (ya instalados, transitivos):** Timeline **1.8.12**, Cinemachine **3.1.6** vía `com.unity.feature.gameplay-storytelling` / `characters-animation`.

**En `Assets/Game`:** cero `PlayableDirector` / Timeline assets / signals.

**Exists today:** sequential root list, parallel wait-all, dialogue await/cancel, camera lease tied to runner, scripted move/anim ownership, TestLab menus that **wire** scenes (no general authoring wizard).

**Does not exist:** Start Delay, categorized Add menu, conflict warnings in Inspector, Timeline bridge, dialogue-from-Timeline.

---

## 2. Existing authoring workflow & pain points

### How a developer creates an event today

1. Manual: GO + `EventRunner` + trigger (`WorldInteractable` / `EventSourceTrigger` / Interact).
2. Or TestLab menu that clones a whole fixture (markers, shots, actions).
3. Assign `eventId`, Once/Repeat, Persist Once, `seizesPlayerControl`.
4. Add conditions/actions via **+** dropdown (flat type names).
5. Drag Transform/asset refs; expand nested Parallel children; save scene.

### Actions: add / configure / reorder / nest / remove

- Root: `ReorderableList` + `SerializeReference` (`EventRunnerEditor`).
- Parallel: custom drawer; Add child / −; **no drag-reorder** of children.
- Nesting: only one Parallel level; Parallel-in-Parallel rejected at runtime.

### Sequential vs Parallel

- Sequential = root `actions` order in `RunSequence`.
- Parallel = child list, all start **t=0**, settle when all finish.

### Targets

- Characters: `ScriptedCharacterTarget` (ActivePlayer / Explicit Transform).
- Cameras: `CameraShotMarker` / focus transforms on camera actions.
- Dialogue: asset / character resolver on `StartDialogueAction`.
- Markers: plain scene Transforms (TestLab creates them).

### Pain points

1. Add menu is a flat list of ~20 type names (no categories).
2. Parallel children not reorderable; deep expand is noisy.
3. Staggered starts require inventing Wait siblings that **don’t** delay peers (Wait as sibling finishes early; peers already started).
4. Duplicate type registries: `EventRunnerEditor.ActionTypes` vs `DialogueConsequenceListDrawer` (camera/dialogue/parallel/chest diverge by design, but easy to forget when adding types).
5. No Inspector warnings for missing refs / dual camera / dual move (only runtime `RequestStop`).
6. Wiring triggers + markers + shots is repetitive outside TestLab.
7. `DisplayName` is good; foldouts are default Unity — OK but not “cutscene author” clear.

### KEEP / REFACTOR / REPLACE / REMOVE

| Item | Verdict |
|------|---------|
| `EventRunner` + `SerializeReference` lists | **KEEP** |
| `EventAction` / `Cancel` / `EventContext` | **KEEP** |
| `ParallelActions` wait-for-all + camera/move validation | **KEEP** (extend carefully) |
| `EventScheduler` / `GameplayLock` / Camera lease | **KEEP** |
| `EventRunnerEditor` + `ReorderableList` | **REFACTOR** (UX), not replace with node graph |
| `ParallelActionsDrawer` | **REFACTOR** (reorder, delay, fold) |
| Dual Add-type arrays | **REFACTOR** → shared categorized registry |
| `EventSystemTestLabBuilder` | **KEEP** as lab; not production authoring |
| Node graph / standalone tool | **REMOVE from scope** |
| Bidirectional Event↔Timeline conversion | **REMOVE from scope** |
| Timeline as EventRunner replacement | **REMOVE** |

---

## 3. Proposed Inspector UX (text mockup)

Unity CustomEditor + ReorderableList + PropertyDrawers is **sufficient**. No node graph.

```
┌ EventRunner ─────────────────────────────────────────┐
│ Event Id [ lab_pontiff_intro     ]  [⚠ Persist needs id]│
│ ☑ Seize Player Control   Mode [Once ▾]  ☐ Persist Once │
│ ☐ Debug Log                                              │
│                                                          │
│ Conditions (AND)  [+] [↕]                                │
│   0 Flag Is True  flagId=…                               │
│                                                          │
│ Actions                                          [+ ▾]   │
│ ┌────────────────────────────────────────────────────┐   │
│ │ 1  Parallel (4)                              [▾][↕]│   │
│ │   ├ 0  Camera Shot     delay 0.00s   marker ● Shot │   │
│ │   ├ 1  Move Character  delay 0.50s   → Pontiff ●   │   │
│ │   ├ 2  Move Character  delay 1.50s   → Jyn ●       │   │
│ │   └ 3  Play Anim       delay 2.00s   Idle          │   │
│ │   [+ Child ▾]   ⚠ two moves same body? (validate)  │   │
│ │ 2  Start Dialogue   asset ● …   ☑ wait             │   │
│ │ 3  Camera Restore   1.0s                           │   │
│ └────────────────────────────────────────────────────┘   │
│ Add Action ▾  Camera / Characters / Dialogue / World /… │
│ [Ping selected refs]   Play: Try Start | Cancel          │
└──────────────────────────────────────────────────────────┘
```

**Author flow:** set id/mode → Add Parallel → add children with Start Delay → assign markers via object fields + ping → add dialogue/restore → Play Mode Try Start / Cancel.

---

## 4. Recommended Start Delay

**Problem:** Parallel starts every child at once. A peer `WaitAction` does **not** stagger others.

**Recommendation (smallest safe):** new wrapper action, no change to existing action bodies:

```
DelayedStartAction : EventAction
  float startDelay
  [SerializeReference] EventAction action   // one child, not Parallel
```

- Execute: wait `startDelay` (honour cancel) → `action.Execute` → forward `Cancel` to child.
- Reject wrapping another `ParallelActions` / nested `DelayedStart` (keep rules simple).
- Parallel validation walks **through** the wrapper to see real Move/Camera types.

**Why not** put `startDelay` on every `EventAction`: touches all types/serialization for a Parallel-only need.

**Why not** replace `List<EventAction>` with `ParallelSlot` yet: breaks existing scenes; can be a later Inspector sugar that serializes as `DelayedStartAction`.

Group still advances only when **all** children (including delayed ones) finish — current Parallel semantics.

---

## 5. Minimum viable Timeline integration

**Exists:** Timeline 1.8.12, CM 3.1.6, EventRunner lifecycle, dialogue sessions, camera lease, scripted motors.

**New:** bridge types + thin Timeline markers/signals.

**Modify:** cancel/cleanup paths; register `PlayTimelineAction`; optional shared action registry.

### 5.1 PlayTimelineAction (EventRunner → Timeline)

- Fields: `PlayableDirector` (or TimelineAsset + director on GO), `waitUntilFinished`, optional `stopOnCancel`.
- Execute: `Play()`, await `time >= duration` / `stopped` / wrapped mode; on cancel → `Stop()` + extrapolate/hold policy documented.
- Runner already holds GameplayLock / scheduler; Timeline **must not** Acquire a second PlayerControl owner.
- Camera: **one owner** — either (A) Timeline drives CM **without** CameraDirector lease, or (B) PlayTimelineAction acquires/holds director lease for the runner and Timeline only animates non-director props. Prefer **B for event cameras** or “Timeline owns shot; CameraDirector idle” with explicit restore on end — avoid dual writers.
- Movement: **no** AnimationTrack on active `PlayerMotor` root that fights CharacterController. Use `ScriptedNpcMotor` bodies, cinematic doubles, or (later) custom clips that call scripted APIs. Not in Phase 0 implement.

### 5.2 DialogueCheckpoint / Pause & Wait / Continue

| Mode | Behavior |
|------|----------|
| **Pause & Wait** | Marker/signal → pause `PlayableDirector` → `DialogueService.TryStartInteractive` (same as today) → wait `session.IsFinished` → if Completed `Resume()`; if Cancelled/Failed → fail policy (stop Timeline + `RequestStop` on host action). |
| **Continue** | Fire dialogue, **do not** pause Timeline; policy: reject if interactive already running; on event cancel → `session.Cancel`; Timeline end does not wait for dialogue unless action awaits separately. |

No second dialogue engine. Consequences stay on dialogue graph.

### 5.3 Completed / Cancelled / Failed

- Timeline natural end + wait → action completes → runner continues.
- Event Cancel → `PlayTimelineAction.Cancel` stops director; abort dialogue if started by checkpoint; `NotifyRunnerFinished` still restores camera.
- Dialogue Failed/Cancelled under Pause&Wait → stop Timeline + `RequestStop` (no Once mark) — align with `StartDialogueAction`.
- **Deadlock avoid:** never pause Timeline waiting on dialogue while dialogue waits on Timeline signal that only fires after resume; checkpoint must be fire-then-pause on the same notification path.
- **Double ownership:** one GameplayLock owner (runner); one camera pose writer; one interactive dialogue session.

### What is necessary later (not now)

Custom `Marker`/`Signal` receiver for DialogueCheckpoint; optional Cinemachine track discipline doc.

**Not necessary:** converting actions↔clips; full custom Animation tracks for PlayerMotor.

---

## 6. Risks & compatibility

| Risk | Note |
|------|------|
| Parallel serialization migration | Prefer `DelayedStartAction` over redefining Parallel list |
| Camera + Timeline CM tracks | Double blend / lease fight |
| AnimationTrack vs PlayerMotor | Physics/desync; banned for active player |
| Dialogue without pause API | Pause the **Director**, not DialogueSession |
| Consequence vs runner action menus | Drift when adding `PlayTimelineAction` / `DelayedStartAction` |
| TestLab vs production | Builder remains optional; Inspector is the product path |
| Cancel mid-Timeline | Must Stop director or it keeps writing after lock release |

---

## 7. Exists / Modify / New

| | |
|--|--|
| **Exists** | EventRunner stack, Parallel wait-all, dialogue, CameraDirector, scripted move/anim, basic ReorderableList editors, Timeline+CM packages |
| **Modify** | `EventRunnerEditor` / `ParallelActionsDrawer` UX; shared action registry; Parallel validate unwrap delay wrapper; cancel docs for Timeline |
| **New** | `DelayedStartAction`; Inspector categories + warnings; `PlayTimelineAction`; DialogueCheckpoint receiver (Pause&Wait / Continue); thin TestLab Timeline fixture (later phase) |

---

## 8. Sprint 09 implementation plan (small, realistic)

1. **Phase 1 — Authoring UX + Start Delay**  
   Shared categorized action registry; Inspector polish (numbers, foldouts, ping, warnings); `DelayedStartAction` + Parallel unwrap validation; QA staggered Parallel fixture (no Timeline).

2. **Phase 2 — PlayTimelineAction**  
   Play/await/Stop on cancel; camera ownership rule locked; no dialogue-from-Timeline yet; one TestLab playable.

3. **Phase 3 — DialogueCheckpoint**  
   Pause&Wait + Continue policies; cancel/fail cleanup; QA deadlock + interrupt.

4. **Out of sprint**  
   Node graph; Event↔Timeline converter; AnimationTracks on PlayerMotor; nested Parallel; dormant-party narrative mode.

---

**Stop.** Esperando aprobación de arquitectura antes de implementar.
