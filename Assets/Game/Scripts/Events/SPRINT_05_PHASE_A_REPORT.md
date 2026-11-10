# Sprint 05 — Phase A Report (Updated)

**Project:** Tears of Mathra (TOM)  
**Scope:** Hybrid Dialogue System — Phase A + approved additions  
**Status:** Phase A **QA passed** (manual play + console logs, 2026-11-10)  
**Date of this report:** 2026-11-10 (updated after Triggered Narrative + Interrupt)

---

## 1. Summary

Phase A delivers a complete **ambient + interactive + triggered narrative** dialogue stack for TestLab:

| Capability | Status |
|------------|--------|
| Ambient NPC mutter (small bubble, no lock, no portrait) | Done |
| Interactive NPC talk (Interact `?`/`!`, large comic chatbox) | Done |
| Multi-speaker conversation-level layout (stable L/R slots) | Done |
| Active player (`active_player` → Jyn/Ax name, portrait, assets) | Done |
| Triggered narrative (volume → interactive-style, no Interact) | Done |
| Blocking / non-blocking lock policy | Done |
| Manual / automatic progression | Done |
| Conflict: Ignore / Queue / **Interrupt** | Done |
| OncePerScene / OncePersistent (`WorldState`) | Done |

**Not in Phase A:** choices / per-line text variants (B), EventRunner `StartDialogueAction` / conditions (C).

---

## 2. Conceptual model (approved)

Three independent axes:

1. **Trigger source** — Interact · Trigger volume · (EventRunner later)  
2. **Presentation** — Ambient-style · Interactive-style  
3. **Lock policy** — Blocking (`lockGameplay`) · Non-blocking  

Triggered narrative = **volume** + **interactive presentation** + **blocking or not**.

Camera: dialogue does **not** call Cinemachine. Blocking stops the motor via `GameplayLock` → follow cam idles because the target stopped.

---

## 3. What shipped

### 3.1 Core runtime

| Piece | Role |
|-------|------|
| `DialogueAsset` / `DialogueLineNode` / `DialogueEndNode` | Graph data |
| `CharacterPortraitRoster` | Names + optional portraits |
| `DialogueService` | Director; ambient list; `TryStartInteractive`; `CancelInteractive`; deferred-after-interactive |
| `DialogueSession` | Line→End; multi-bubble; lock; initiator freeze; progression Manual/Automatic |
| `DialogueInput` | Confirm / Cancel / Fast-forward (survives PlayerInput lock) |
| `DialogueUIController` / `DialogueChatboxView` | Overlay placeholders |
| `NpcDialogueHost` | Ambient / Interactive / Both + Sprint 04 prompts |
| `DialogueLayoutSolver` + `ConversationLayoutPlan` | Ambient place + interactive conversation slots |
| `DialogueActiveCharacter` | `active_player` aliases |
| `DialogueCharacterAssetResolver` | Per-character asset pick + fallback |
| `NarrativeDialogueTrigger` | Volume adapter for narrative lines |

### 3.2 Interactive layout (final)

- Speakers collected **before** first line  
- One shared plan (typically Player Left / NPC Right by screen X)  
- Per-frame **follow** only — no re-sort on turn change  
- Rebuild only on large framing flip / offscreen / unexpected speaker  
- Solo-NPC conversations use a closer lift (`conversationSoloLiftRef`)

### 3.3 Active player

- Authored `speakerId = active_player` (aliases: `player`, `party`, `hero`)  
- Session freezes **character id** + initiating **Transform** at start  
- Triggered narrative can pass the **entering** character as initiator  
- Host `interactiveByCharacter[]` for fully different assets per Jyn/Ax  

### 3.4 Triggered narrative

`NarrativeDialogueTrigger`:

| Setting | Options |
|---------|---------|
| Actor | `ActivePlayerOnly` (default), `AnyPartyMember` |
| Fire | `Repeat` + cooldown · `OncePerScene` · `OncePersistent` |
| Conflict | `Ignore` · `Queue` (1 pending) · `Interrupt` |
| Progression | `Manual` · `Automatic` (+ hold seconds) |
| Lock | Use asset · Force blocking · Force non-blocking |

**Once consumption:** only when the session **finishes cleanly** (`!WasCancelled`). Failed start or cancel → not consumed. Interrupt cancels the previous session → previous Once not consumed.

**Persistent flag:** `dialogue.narrative.{triggerId}.seen` via existing `WorldState` / save pipeline.

**Interrupt (QA confirmed in logs):**  
`Session cancelled (narrative-interrupt) asset='lab_narrative_auto'` → immediate `Interactive start 'lab_narrative_manual'`.

### 3.5 Presentation sizes (defaults)

| | Ambient | Interactive |
|--|---------|-------------|
| Size (ref units) | 380 × 140 | 560 × 220 |
| Speaker font | ~20 | ~24 |
| Body font | ~22 | ~30 |

---

## 4. TestLab fixtures

**Tools → ARPG → Events → Add Dialogue Test Fixtures (Phase A)**

| Object | Purpose |
|--------|---------|
| `NPC_Ambient_A` / `B` | Ambient only (z ≈ −20) |
| `NPC_Interactive` | Shared `active_player` talk |
| `NPC_Interactive_NoPortrait` | No roster portrait |
| `NPC_Both` | Ambient + interactive priority |
| `NPC_HiddenPrompt` | Interact without `?`/`!` |
| `NPC_ActivePlayer_Variants` | Jyn/Ax-specific assets |
| `Trigger_Narrative_Auto_NonBlocking` | Auto + move + Queue (z ≈ −24) |
| `Trigger_Narrative_Manual_Blocking` | Manual + lock + **Interrupt** |
| `Trigger_Narrative_OncePersistent` | Auto + OncePersistent + Queue |

---

## 5. QA status

| Area | Doc | Result |
|------|-----|--------|
| Ambient + Interact baseline | `SPRINT_05_PHASE_A_QA.md` | Pass (earlier session) |
| Active player | `SPRINT_05_ACTIVE_PLAYER_QA.md` | Pass |
| Conversation layout stability | `SPRINT_05_CONVERSATION_LAYOUT_QA.md` | Pass |
| Triggered narrative + Interrupt | `SPRINT_05_TRIGGERED_NARRATIVE_QA.md` | **Pass** (user logs 2026-11-10) |

Observed in console (abridged):

1. Start `lab_narrative_auto`  
2. Enter Interrupt pad → `cancelled (narrative-interrupt)` → start `lab_narrative_manual`  
3. Once / Auto / party swap Ax→Jyn still trigger correctly as ActivePlayer  

---

## 6. Files (high level)

### New (additions)
- `Dialogue/NPC/NarrativeDialogueTrigger.cs`
- `Dialogue/Runtime/DialogueProgressionMode.cs`
- `Dialogue/Runtime/DialogueActiveCharacter.cs`
- `Dialogue/Runtime/DialogueCharacterAssetOverride.cs`
- `Dialogue/Runtime/DialogueCharacterAssetResolver.cs`
- `Dialogue/Runtime/ConversationLayoutPlan.cs`
- `Dialogue/Runtime/DialogueLayoutSettings.cs`
- Plan/QA/Report markdown under `Events/SPRINT_05_*`

### Core updated
- `DialogueSession`, `DialogueService` (`CancelInteractive`, initiator + progression args)
- `DialogueLayoutSolver`, `AmbientDialogueHandle`, `NpcDialogueHost`
- `DialogueUIController` / chatbox presentation
- `EventSystemTestLabBuilder` fixtures

---

## 7. Inspector quick reference

### `NarrativeDialogueTrigger`
- `triggerId`, `dialogueAsset`, optional `dialogueByCharacter[]`  
- `lockGameplay`, `progressionMode`, `autoAdvanceHoldSeconds`  
- `actorFilter`, `fireMode`, `retriggerCooldownSeconds`  
- `conflictPolicy` = Ignore | Queue | **Interrupt**  
- `cancelPendingOnExit` (Queue)

### `NpcDialogueHost`
- Ambient / Interactive assets, `interactiveByCharacter[]`  
- Prompt mode, radii (360°), `npcSpeakerId`, `speechAnchor`

### Line authoring
- `speakerId = active_player` for controlling / entering character  

---

## 8. Controls

| Action | Binding |
|--------|---------|
| Confirm / advance | E, Enter, Gamepad South |
| Cancel | Esc, Gamepad East |
| Fast-forward typewriter | Left Shift / Right Shoulder |

Automatic progression needs **no** Confirm (hold then advance); Confirm can still skip hold.

---

## 9. Known limitations (Phase A)

- Placeholder UI / missing portrait sprites in TestLab OK  
- No choice UI / branch nodes (Phase B)  
- No per-line Ax/Jyn text variants (Phase B)  
- No `StartDialogueAction` in EventRunner yet (Phase C; resolver + initiator API ready)  
- Scene-serialized `DialogueService.layoutSettings` may retain old sizes until reset  
- Conversation speaker walk is Line/`nextNodeId`-oriented  

---

## 10. Next steps

1. Treat Phase A as **closed** pending any final art/font swap.  
2. **Phase B** only with explicit approval: choices + optional per-line character variants.  
3. **Phase C** after Phase B QA: EventRunner wait action, conditions/consequences, scripted starts reusing initiator + resolver.  

---

## 11. Related docs

| Doc | Topic |
|-----|--------|
| `SPRINT_05_PLAN.md` | Master plan + links |
| `SPRINT_05_ACTIVE_PLAYER_PLAN.md` / `_QA.md` | Active player |
| `SPRINT_05_CONVERSATION_LAYOUT_PLAN.md` / `_QA.md` | Stable slots |
| `SPRINT_05_TRIGGERED_NARRATIVE_PLAN.md` / `_QA.md` | Volume narrative + conflict policies |
| `SPRINT_05_PHASE_A_QA.md` | Baseline ambient/interact |
| `SPRINT_05_PHASE_A_REPORT.md` | This report |
