# Sprint 05 — Hybrid Dialogue System (PLAN ONLY)

**Status:** Awaiting architecture approval. **Do not implement until approved.**  
**Depends on:** Sprints 01–04 (approved).  
**Do not start Sprint 06.**

Internal delivery: **Phase A → QA → Phase B → QA → Phase C → QA**.

---

## 1. Technical audit (current project)

| System | Role relevant to dialogue | Integration note |
|--------|---------------------------|------------------|
| `EventRunner` + `SerializeReference` `EventAction` | Ordered sequences; wait via coroutine `Execute` | Add `StartDialogueAction` that yields until session ends |
| `EventScheduler` | 1 player-control event; unlimited env | Ambient = never seize. Interactive wait action: prefer **parent runner does not seize** OR dialogue owns lock; avoid nested seize |
| `EventContext` + `Cancel` | Cancellation token per run | Dialogue session must cancel + release locks on runner cancel / destroy |
| `GameplayLock` + `PlayerInputGate` | Ref-count locks; never blind re-enable | Interactive may acquire `PlayerControl` (or subset). Dialogue Confirm must **not** depend on `PlayerInput` (it gets disabled) |
| `InteractInput` | E/Enter/South; blocked when `Interact` locked | Good: locks prevent InteractionDetector during dialogue |
| `WorldState` + Save pipeline | Flags + completed event ids | Choice consequences = existing `SetFlag` / `ClearFlag` / actions. Optional `dialogue.{id}.seen` flags |
| `InteractionDetector` + `IInteractable` / `WorldInteractable` | Central interact + FFIX `?`/`!` | Interactive NPC = `WorldInteractable` (or adapter) → starts dialogue. Prompt Mode independent of dialogue mode |
| `PlayerInteractionPrompt` | Prompt under `SpriteBody/Prompt` | **Do not** reuse for chatboxes |
| `Party.Active` | Active Jyn/Ax | Speaker “player” resolves to active member; swap mid-dialogue = policy (freeze swap via lock) |
| UI | uGUI + TMP; Menu Canvas; `ItemPickupPopup` is **World Space** | Dialogue uses **Screen Space** (Overlay or Camera) — not world meshes |
| Cameras | BattleRing: Main + UI Camera (Prompt layer 9) + Cinemachine | Dialogue canvas on Overlay (simplest) or UI camera stack; world-to-screen via `Camera.main` / brain output |
| Input conflicts | Jump + Interact share South (documented) | Dialogue Confirm should prefer **East/Cancel-safe** or explicit Confirm; audit before binding |

**No existing dialogue / NPC talk system found.** Build new module under `Assets/Game/Scripts/Dialogue/`.

### Risks from audit

1. **PlayerInput disabled under lock** → dialogue needs its own `InputAction`s (like `InteractInput`), not `PlayerInput` asset actions alone.
2. **Nested EventRunner + dialogue wait** → deadlock if choice starts a seizing runner while `StartDialogueAction` holds another seize. Mitigation in §8.
3. **South button** → Confirm vs Jump; keep Jump locked during dialogue; document Confirm binding.
4. **Prompt layer vs Overlay UI** → chatboxes are not Prompt sprites; Overlay is fine and avoids layer 9 coupling.
5. **Do not modify PlayerMotor / production prefabs** without approval; TestLab fixtures only for QA.

---

## 2. Proposed classes & responsibilities

```
Assets/Game/Scripts/Dialogue/
  Data/
    DialogueAsset.cs              // SO conversation graph
    DialogueNode.cs               // SerializeReference base
    DialogueLineNode.cs
    DialogueChoiceNode.cs
    DialogueBranchNode.cs         // conditional jump
    DialogueEndNode.cs
    DialogueChoiceOption.cs
    CharacterPortraitRoster.cs    // SO library
    CharacterPortraitEntry.cs
  Runtime/
    DialogueService.cs            // scene/bootstrap singleton: sessions + ambient slots
    DialogueSession.cs            // one interactive (or non-blocking) playthrough
    AmbientDialoguePlayer.cs      // per-NPC ambient lifecycle
    DialogueLayoutSolver.cs       // screen positions / overlap
    DialogueInput.cs              // Confirm / Next / FastForward / Navigate / Cancel
  Presentation/
    DialogueUIController.cs       // spawns/binds views; style-agnostic API
    AmbientChatboxView.cs         // small bubble, no portrait
    InteractiveChatboxView.cs     // large bubble, optional portrait + choices
    DialogueAnchor.cs             // world transform → screen point
  NPC/
    NpcDialogueHost.cs            // Ambient / Interactive / Both + refs to assets
  Actions/
    StartDialogueAction.cs        // EventAction
```

Editor: custom drawers for `DialogueAsset` nodes; `NpcDialogueHost` shows only relevant fields per mode.

---

## 3. Dialogue data & branching model

**`DialogueAsset` (ScriptableObject)**  
- `dialogueId` (stable, unique)  
- `entryNodeId`  
- `nodes`: `List<DialogueNode>` via `[SerializeReference]` (Inspector list, **no** visual graph editor)  
- Optional default typewriter speed / lock profile  

**Nodes**

| Type | Fields | Next |
|------|--------|------|
| **Line** | speakerId, displayName override, text, portraitExpression (interactive only), duration overrides, optional inline conditions | `nextNodeId` |
| **Choice** | prompt text optional; `options[]` | each option → `nextNodeId` |
| **Branch** | `List<EventCondition>` + target ids (first true wins; else default) | jump |
| **End** | — | ends session |

**Choice option:** text, availability `EventCondition`s, `nextNodeId`, `List<EventAction> consequences` (reuse SetFlag/ClearFlag/etc.).

**Validation (editor + runtime):** missing next ids, duplicate node ids, empty choices, dialogueId empty, portrait missing → warn, don't crash.

**NPC does not embed full graphs** — only references `DialogueAsset`s (ambient + interactive separately for Both).

---

## 4. Portrait roster

**`CharacterPortraitRoster` SO**  
- List of characters: `characterId`, display name, `expressions[]` (enum/string → Sprite)  
- Shared across scenes; lines reference `characterId` + `expressionId`  
- Missing sprite → hide portrait panel; layout collapses (no broken UI)  
- Ambient authoring **never** shows portrait fields  

Placeholder sprites OK until final art.

---

## 5. Ambient vs interactive lifecycle

### `NpcDialogueHost`
- `DialogueNpcMode`: AmbientOnly | InteractiveOnly | Both  
- Ambient: asset, proximity radius, Once/Repeat, cooldown, dismissOnExit, conditions  
- Interactive: asset, optional `WorldInteractable` / implements `IInteractable.TryInteract` → `DialogueService.StartInteractive`  
- **Prompt Mode stays on WorldInteractable** (Hidden / InteractionOnly / Discovery+Interaction) — independent  

### Ambient
- Proximity check (distance or trigger) → request slot from `DialogueService`  
- Auto typewriter + hold duration → dismiss  
- Cooldown / Once via runtime + optional `WorldState` flag `dialogue.{id}.ambientSeen`  
- No GameplayLock; no portraits  
- Max concurrent ambient (default **2**); layout solver may postpone/suppress  

### Interactive
- Start via Interact or `StartDialogueAction`  
- Optional `lockGameplay` (default true for story talks) → `GameplayLock.Acquire(session, PlayerControl)`  
- Typewriter → Confirm completes line XOR advances (one press = one action)  
- Choices → navigate → Confirm once  
- End → release lock, notify waiters  
- Interactive **suppresses new ambient** (and hides/pauses ambient that would overlap)  

### Both
- Independent assets; ambient never blocks interactive availability  
- If player starts interactive while ambient showing on same NPC → dismiss that ambient first  

---

## 6. Input & playback

**`DialogueInput`** (static, own `InputAction`s — survive `PlayerInput` disable):

| Action | Proposed default | Notes |
|--------|------------------|-------|
| Confirm / Advance | Enter, E, Gamepad **East** (Circle/B) | Avoid South if Jump still active; during lock Jump is off so South *can* be secondary Confirm — **decision** |
| Fast Forward | Hold Confirm or dedicated (R2 / Right Shoulder) | Accelerates typewriter only |
| Navigate choices | W/S or D-Pad / Stick | |
| Cancel dialogue | optional Escape / East if not Confirm | Only if conversation allows cancel |

Rules:
- Line typing + Confirm → fill line (do not advance same frame).  
- Line complete + Confirm → next node.  
- Choice: Confirm selects once; ignore hold spam (`WasPressedThisFrame`).  
- While interactive locks `Interact`, `InteractionDetector` cannot fire.  
- Ambient: no input.

Skip conversation: **opt-in per asset, default off**; skip stops before Choice nodes and before nodes tagged `mandatory` / with consequences; else blocked. Phase C.

---

## 7. Screen-space positioning & overlap

- Chatboxes: **Screen Space Overlay** (or Screen Space Camera) prefabs — **not** 3D world quads.  
- Each view: `DialogueAnchor` world point (NPC head bone/transform) → `RectTransform` screen position.  
- **Ambient layout solver (lightweight):**  
  1. Preferred side (Above / Left / Right / Below)  
  2. Try candidate offsets  
  3. Clamp to screen safe rect  
  4. AABB overlap vs other ambient + active interactive  
  5. If no slot → postpone (short retry) or suppress (do not queue forever)  
- Interactive: larger panel; preferred lower-third or beside speaker; still clamped; ambient yields.  
- Manual overrides on host/view: offset, scale, preferred side, width.  
- Off-screen speaker: clamp to edge + optional minimal indicator; do not spawn off-canvas.  
- Approximation for “gameplay obstruction”: avoid center-bottom combat HUD band via safe margins (configurable), not AI vision.

---

## 8. EventRunner / WorldState / GameplayLock integration

### StartDialogueAction
```text
Execute:
  start interactive session (asset + optional speaker overrides)
  while !session.IsFinished && !ctx.IsCancelled: yield
  if cancelled: session.Cancel()
```

### Lock ownership model (avoid deadlock)
1. **Recommended:** EventRunner that only runs `StartDialogueAction` has `seizesPlayerControl = false`; dialogue session acquires `GameplayLock` itself when `lockGameplay`.  
2. If designer enables seize on parent runner: dialogue **reuses** existing lock (does not Acquire again); session still releases only what it acquired.  
3. Choice consequences run **inline** via temporary `EventContext` calling `EventAction.Execute` **without** `EventScheduler.TryBegin` / without nested player-control runners.  
4. If a consequence must start a full `EventRunner`, it is deferred until dialogue session ends (queue on `DialogueService`) — never nested seize mid-wait.  

### Cancellation
- Runner cancel / OnDestroy / scene unload → `session.Cancel()` → hide UI → `GameplayLock.Release(session)` → clear waiters.  
- Consequences already applied are **not** rolled back (same as EventRunner policy).  
- Cancel mid-choice: no option selected; no consequences.

### Persistence
- Only WorldState mutations from consequences / optional seen flags.  
- Do **not** save “current line index” in Sprint 05.

---

## 9. Inspector authoring workflow

1. Create `DialogueAsset` → add Line/Choice/Branch/End nodes (Reorderable + type dropdown).  
2. Assign speakers / expressions from roster (interactive lines only).  
3. Wire conditions / consequence actions (existing EventCondition / EventAction drawers).  
4. Add `NpcDialogueHost` to NPC → mode Ambient / Interactive / Both → assign asset(s).  
5. For Interactive/Both: ensure `WorldInteractable` (Prompt Mode as desired, including Hidden).  
6. Tune proximity, cooldown, lock, layout overrides.  
7. TestLab Play.

Ambient inspector: **no** portrait/choice UI. Interactive: full. Both: two clear sections.

---

## 10. Phase A / B / C

### Phase A — Core (stop for QA)
- Data: DialogueAsset + Line + End  
- Roster SO (optional portraits)  
- NpcDialogueHost Ambient / Interactive / Both (line-only)  
- DialogueService + ambient slots + interactive session  
- Ambient + Interactive chatbox views (placeholder comic style)  
- Layout clamp + basic overlap  
- Typewriter, fast-forward, complete-line / next-line  
- GameplayLock on interactive  
- WorldInteractable start path  
- TestLab fixtures for ambient + interactive (no choices yet)

### Phase B — Choices (stop for QA)
- ChoiceNode + UI list  
- Keyboard + gamepad navigate/confirm  
- Conditional options  
- BranchNode  
- Safe confirm (one shot)

### Phase C — Consequences & integration (stop for QA)
- Option consequences (`EventAction` list)  
- `StartDialogueAction` + wait  
- Seen/cooldown WorldState helpers  
- Skip (safe)  
- Cancel / destroy / regression Sprint 01–04  
- Full QA matrix §15

---

## 11. Manual QA strategy (summary)

TestLab menu: **Add Dialogue Test Fixtures** (Phase A+).  
Beginner steps per case: object names, Inspector fields, walk/interact, expected UI/log, fail symptoms — expanded when implementing each phase.

Critical Phase A checks: ambient no lock/no portrait; interactive larger + optional portrait; Both independent; Hidden prompt; screen clamp; typewriter/confirm; Sprint 04 prompt intact.

Phase B: choice nav + branches.  
Phase C: flags persist F5/F9; EventRunner wait; cancel releases lock; doors/switches/interact regression.

---

## 12. Decisions requiring approval

| # | Decision | Proposal |
|---|----------|----------|
| D1 | Confirm binding | Enter + E + Gamepad **East**; South as optional extra only while locked |
| D2 | Max ambient bubbles | **2** default, Inspector on DialogueService |
| D3 | Canvas mode | **Screen Space Overlay** dedicated Dialogue canvas (DontDestroy or bootstrap) |
| D4 | Non-blocking interactive | Supported flag `lockGameplay=false`; story default true |
| D5 | Nested EventRunner from choice | **Defer until dialogue ends** (no nested seize) |
| D6 | Ambient “seen” persistence | Optional per-asset `persistSeen` → WorldState flag |
| D7 | Party swap mid-dialogue | Locked via PartySwap channel when `lockGameplay` |
| D8 | Production prefab edits | **None** in Sprint 05; TestLab + optional sample NPC prefab only |
| D9 | Skip | Phase C only; default off |
| D10 | Chatbox final art | Placeholder 9-slice / TMP; swappable prefabs later |

---

## Explicit non-goals (confirm)

No quest/reputation, no visual node editor, no VO/lipsync, no Camera Director, no Motor/combat/camera changes, no Sprint 04 prompt redesign, no new save architecture, no Sprint 06.
