# Sprint 05 — Addition: Active Player-Aware Dialogue

**Status:** Phase A **implemented** — awaiting manual QA  
(`SPRINT_05_ACTIVE_PLAYER_QA.md`).  
**Scope:** Part of Sprint 05 (not a future sprint).  
**Source of truth:** `Party.Active` → `PlayerHealth.character.id` (`CharacterDefinition.id`).  
**Do not** create a second active-player variable or duplicate party state.  
**Do not** start Phase B until Phase A QA is approved.

### Approved adjustments (applied)
1. Freeze **character id** at session start; keep initiating **Transform** and follow its live world position (do not freeze pose; do not rebind on `Party.Active` change; null-safe if destroyed).  
2. Per-character asset selection lives in reusable `DialogueCharacterAssetResolver` (NpcDialogueHost consumes it; Phase C EventRunner can reuse).

---

## Goals

Support dialogue differences depending on the currently active playable character (e.g. Ax or Jyn).

### Three scenarios

1. **Shared dialogue**  
   Same content regardless of who is active.  
   Lines using an ActivePlayer speaker resolve the correct display name and optional portrait.

2. **Shared dialogue with character variants** *(Phase B)*  
   Most of the conversation is shared.  
   Individual lines can have different text per active character.  
   Do not require duplicating entire conversations for small differences.

3. **Character-specific dialogue** *(Phase A asset selection)*  
   An NPC / interactable can select entirely different `DialogueAsset`s depending on the active character (e.g. Ax asset vs Jyn asset), with a sensible shared fallback.

---

## Audit (current code)

| Need | Current state |
|------|----------------|
| Speaker `ActivePlayer` → name / portrait of active member | **Missing.** Lines use literal `speakerId` (`jyn` / `ax`). Roster looks up that id as-is. |
| World anchor follows active member | **Partial.** `DialogueSession.IsPlayerSpeaker` treats `jyn` / `ax` / `player` / `party` / `hero` and anchors to `Party.Active`, but **display name / portrait do not resolve** to the active character. |
| Session-stable active identity | **Missing.** Layout/anchor re-reads `Party.Active`; no frozen session character id (GameplayLock usually blocks swap, but identity is not captured). |
| Per-character `DialogueAsset` on host | **Missing.** `NpcDialogueHost.interactiveDialogue` is a single asset. |
| Per-line text variants | Phase B — not on `DialogueLineNode`. |
| Character-specific conditions / consequences | Phase C. |

Existing pieces to reuse:

- `Party.Active`, `Party.FindByCharacterId`, `Party.SetActiveByCharacterId`
- `CharacterDefinition.id`
- `CharacterPortraitRoster` (`characterId` → displayName + expressions)
- `NpcDialogueHost` + `DialogueService.TryStartInteractive`
- Interactive `GameplayLock` (helps freeze control / swap during talk)

---

## Architecture (approved direction)

### Principles

- Resolve active character identity **when the dialogue session begins**; keep it **stable for the session**.
- Compare with **stable character IDs**, not display names.
- Fallback when no character-specific variant is configured.
- Avoid hardcoding Ax/Jyn in runtime beyond optional authoring aliases; prefer `CharacterDefinition.id`.
- Keep authoring Inspector-friendly.
- Remain compatible with conditions, branching, portraits, and WorldState.
- Do not modify `Party` / party systems unnecessarily.

### Phase A — implement first

#### A1. ActivePlayer speaker resolution (scenario 1)

- Authoring token: `active_player`  
  Legacy aliases (optional): `player`, `party`
- On `DialogueSession.Start`:
  - Capture `sessionActiveCharacterId` from `Party.Active.character.id`
  - Capture active transform for anchoring
- When a line uses an ActivePlayer alias:
  - Resolve to `sessionActiveCharacterId` for bubble key, roster display name, portrait, and world anchor
- Lines with fixed `jyn` / `ax` remain **literal** (NPC talking *about* a specific character)
- Helper (small, shared): e.g. `DialogueActiveCharacter.cs`
  - `TryGetActiveCharacterId(out string id)`
  - `IsActivePlayerAlias(string speakerId)`
  - `ResolveSpeakerId(string authoredId, string sessionActiveId)`

#### A2. Per-character DialogueAsset selection (scenario 3)

On `NpcDialogueHost` (Inspector):

```text
interactiveDialogue                 → shared / fallback asset
interactiveByCharacter[]            → { characterId, asset }
```

On `TryInteract`:

1. Read `Party.Active.character.id`
2. If list has matching `characterId` with non-null asset → use it
3. Else → `interactiveDialogue`
4. If still null → fail gracefully (existing warning path)

Same helper can later serve `StartDialogueAction` / other interactables.  
Ambient overrides are **optional** in Phase A (interactive is required).

#### A3. Session stability

- Freeze character id (+ anchor) at session start
- Do not re-bind ActivePlayer identity mid-session if `Party.Active` changes
- Do not change Party swap APIs; rely on existing gameplay lock during interactive dialogue

### Phase B — after Phase A QA

- Per-line text variants on shared `DialogueAsset` (e.g. default text + optional overrides keyed by `characterId`)
- NPC / shared nodes stay single-graph; only variant fields differ
- No full conversation duplication for small wording changes

### Phase C — after Phase B QA

- Character-specific conditions / consequences
- Broader integration with EventRunner / WorldState
- Full QA matrix across prompts, ambient, interactive, save flags

---

## Files affected (Phase A only)

| File | Change |
|------|--------|
| `Dialogue/Runtime/DialogueActiveCharacter.cs` | **New** — resolve id / aliases from `Party.Active` |
| `Dialogue/Runtime/DialogueSession.cs` | Freeze session id; resolve ActivePlayer for name / portrait / anchor / bubble key |
| `Dialogue/NPC/NpcDialogueHost.cs` | `interactiveByCharacter[]` + select asset with fallback |
| `Dialogue/Runtime/DialogueService.cs` | Pass session character id into session (if not resolved only inside session) |
| `Editor/Events/EventSystemTestLabBuilder.cs` | Fixture: ActivePlayer line + optional Ax/Jyn asset overrides |
| `Events/SPRINT_05_PHASE_A_QA.md` or sibling QA note | Active-player cases |
| `Events/SPRINT_05_PLAN.md` | Cross-link this addition |

**Out of scope for Phase A:** `Party.cs`, `PlayerMotor`, layout/chatbox solver (except bubble key change from resolved id), Phase B line variants, Phase C conditions.

---

## Compatibility & fallbacks

- Existing assets with `speakerId = "jyn"` / `"ax"` keep working as fixed speakers.
- Hosts with only `interactiveDialogue` set behave as today (shared).
- Missing Active / missing `character.id` → alias cannot resolve; warn once; show safe label (e.g. `"Player"`) and hide portrait if roster miss.
- Roster / typewriter / branching / WorldState contracts unchanged.

---

## QA — Phase A

| # | Steps | Expect |
|---|--------|--------|
| 1 | Shared NPC; line `active_player`; talk as **Jyn** | Display name + portrait = Jyn |
| 2 | Same asset; swap to **Ax**; talk again | Display name + portrait = Ax |
| 3 | Host with Ax-only + Jyn-only interactive assets | Entirely different conversations per active character |
| 4 | Host with only fallback asset | Both characters use shared dialogue |
| 5 | Missing override for one character | Falls back to `interactiveDialogue` |
| 6 | Mid-dialogue | Session identity stable (no mid-talk rename/portrait flip) |
| 7 | Regression | Multi-bubble layout, cancel, ambient yield still OK |

---

## Phasing gate

```text
Phase A (ActivePlayer resolve + asset selection)
  → manual QA
  → approval
Phase B (per-line variants)
  → QA
  → approval
Phase C (conditions / consequences / integration)
  → QA
```

**Do not implement Phase B or C without explicit approval after the previous phase passes QA.**
