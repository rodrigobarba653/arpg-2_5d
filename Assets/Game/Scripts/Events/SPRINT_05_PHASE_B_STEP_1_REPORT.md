# Sprint 05 — Phase B Step 1 Report

**Date:** 2026-11-10  
**Status:** Implemented. User confirmed the variant fixture works in TestLab.  
**Next:** Step 2 only, after reading this file and `SPRINT_05_PHASE_B_PLAN.md`.  
**Do not start Step 3.**

Architecture baseline remains `SPRINT_05_PHASE_B_PLAN.md`. This file records what Step 1 actually shipped. Where code and an older chat disagree, read the code.

---

## What shipped

Per-character body text on an existing linear `DialogueLineNode`. No choices, no layout changes, no `DialogueSessionOutcome`.

| File | What it does |
|------|----------------|
| `Dialogue/Data/DialogueTextVariant.cs` | `characterId` + `text` |
| `Dialogue/Data/DialogueLineNode.cs` | Optional `textVariants`. `text` stays the default |
| `Dialogue/Runtime/DialogueTextResolver.cs` | Picks the body |
| `Dialogue/Runtime/DialogueSession.cs` | `PlayLine` calls the resolver with `SessionActiveCharacterId` |
| `Editor/Events/EventSystemTestLabBuilder.cs` | Menu **Add Dialogue Line Variant Fixture (Phase B Step 1)** |

`PlayLine` still resolves the speaker, portrait, typewriter, and layout the same way as Phase A. Only the string passed to the typewriter changed.

---

## Resolver (as implemented)

`DialogueTextResolver.Resolve(defaultText, variants, sessionCharacterId)`:

- Does not read `Party.Active`.
- Null variants, empty session id, or no match → `defaultText` (null becomes `""`).
- Walks the list in order. Skips null entries and entries with empty `text`.
- `active_player`, `player`, `party`, and `hero` go through `DialogueActiveCharacter.ResolveSpeakerId` using the frozen session id.
- Other ids are compared to that frozen id with `OrdinalIgnoreCase`. Project ids are `Ax` and `Jyn`; the fixture authors `ax` and `jyn`.
- First non-empty match wins.

Ambient still prints `line.text` and ignores variants. Ambient has no session and no frozen id. Leave that alone in Step 2.

---

## Test fixture

Menu creates only:

- Scene object `NPC_LineVariants` at `(0, 1, -28)`
- Asset `Assets/Game/Dialogue/TestLab/Lab_Line_Variants.asset` (`dialogueId` `lab_line_variants`)

It does not destroy or rewrite Phase A objects or assets.

`interactiveLockGameplay` and `asset.lockGameplay` are **false on this NPC only**. That is a test hook so Q / Left Trigger can swap during the talk and the later line still uses the character who started it. It is not the rule for interactive chat.

Normal interactive NPCs (`NPC_Interactive` and the rest of Phase A) keep `interactiveLockGameplay = true`. That acquires `GameplayLockChannels.PlayerControl`, which includes party swap, so Q does nothing while those chats are open. Do not turn the lock off on those NPCs. Do not "fix" the green NPC by turning the lock on unless a later QA task asks for it.

---

## Leave this alone in Step 2

- `DialogueTextResolver` and `textVariants`.
- Speaker, portrait, typewriter, progression, and `BuildConversationPlan`.
- `GameplayLock`, `WorldState` storage, save/load, `EventRunner`, cameras, motor.
- Phase A fixture menu. `EnsureDialogueAsset` rewrites `nodes` every time it runs. Do not point it at `Lab_Interactive` or the other Phase A assets.
- The green NPC's lock flag.

---

## Step 2 scope

From the approved plan, sequence item 2 only:

Add `DialogueSessionOutcome`: `None`, `Completed`, `Cancelled`, `Failed`.

`WasCancelled` stays the cancel bit. It is true only for `Cancelled`. `Cancel()` is the only writer of `Cancelled`. `Finish()` must not overwrite an outcome that was already set. `Finish()` still releases bubbles and the lock on every outcome.

Today the only reader outside the session is `NarrativeDialogueTrigger.OnInteractiveSlotFree`:

```text
success = !startedSession.WasCancelled
if success: ConsumeOnce()
cooldown is applied either way
```

Change that success test to `Outcome == Completed`. If it stays `!WasCancelled`, a `Failed` session still consumes Once. That is the one logic change in `NarrativeDialogueTrigger`. Cooldown still runs after cancel and after failure.

`DialogueService.NotifyInteractiveFinished` still runs the deferred queue for every outcome. Update the comment so Phase C knows to require `Completed`. Do not stop calling the queue.

Endings the current line/end runner can already hit:

| Ending | Outcome | Once |
|--------|---------|------|
| `DialogueEndNode` | Completed | Consumed |
| Empty `nextNodeId` | Completed | Consumed |
| User cancel, Interrupt (`CancelInteractive`), service destroy | Cancelled | Not consumed |
| `TryStartInteractive` returns false | No session | Not consumed |
| Non-empty id that `FindNode` misses | Failed | Not consumed |
| Null entry | Failed | Not consumed |
| Unsupported node type (the existing Phase A warning path) | Failed | Not consumed |

Empty next and `DialogueEndNode` must keep consuming Once. The behavior change versus Phase A is only a bad id and a null entry: those used to fall through `Finish()` with `WasCancelled == false` and counted as seen.

Do not implement in Step 2: choice nodes, choice UI, navigation, branching, layout, the automatic-cycle guard, the 32-per-frame fuse, conditions, or `StartDialogueAction`. Those Failed rows exist in the plan for later steps. The enum can represent them later. Step 2 does not need choice or cycle code to set them.

Prove it with a **new** OncePersistent pad. Do not rewrite `Trigger_Narrative_OncePersistent`. Put the new pad away from z ≈ −20 and z ≈ −24. One asset that ends cleanly must set `dialogue.narrative.{triggerId}.seen`. One asset whose `nextNodeId` points at a missing id must not set the flag, must release the lock, and must be allowed to fire again after the existing cooldown.

---

## QA the user already accepted

Variant fixture, in play: Ax and Jyn see their own line 1, missing variants use the default, and an empty Ax variant does not blank the line. User confirmed this works.

They also confirmed they do not want party swap during a normal interactive chat. That remains true wherever `lockGameplay` is on. The green NPC is the exception used to observe the frozen id.
