# Sprint 05 — Phase B Plan

**Project:** Tears of Mathra (TOM)  
**Scope:** Dialogue choices, branching, per-character line variants, branch-aware layout  
**Status:** First audit largely approved. Four decisions revised below. **Do not implement until these revisions are approved.**  
**Date:** 2026-11-10  
**Depends on:** Phase A QA passed (report of 2026-11-10).

This file is the Phase B source of truth. Where it disagrees with the first audit, this file wins. It does not reopen Phase A and it does not start Phase C.

---

## 0. Already approved

These points from the first audit stay. They are not open.

- One dialogue runtime. No second engine, no visual node editor (Sprint 09).
- `DialogueChoiceNode` plus `DialogueChoiceOption`. Choices are not fields on `DialogueLineNode`.
- No `DialogueBranchNode` in Phase B. Silent conditional jumps belong to Phase C. `DialogueGraph` is the only edge list, so a later branch node is additive.
- `DialogueTextVariant` on line body and on choice labels. One resolver. Match `CharacterDefinition.id` with `OrdinalIgnoreCase` against `DialogueSession.SessionActiveCharacterId`. Aliases go through `DialogueActiveCharacter.ResolveSpeakerId`. Empty variant text falls through to the default. Do not re-read `Party.Active` mid-session.
- Option visibility: existing `EventCondition` list (AND, null entries skipped) plus optional `requiredCharacterId` compared to the frozen session id. Unavailable options are omitted, not shown disabled. Empty result uses `fallbackNodeId`, or ends if that id is empty.
- `optionId` is stored and not executed. No `EventAction` list on options. No `StartDialogueAction`.
- Navigation extends `DialogueInput` (W/S, arrows, d-pad, digitalized left stick). Confirm and Cancel stay on their Phase A bindings.
- A choice never confirms itself. Automatic progression pauses on a choice. The advance press cannot select, and the select press cannot fill the next typewriter (`ConfirmReleased` plus `SuppressConfirmUntilReleased`, same contract as `PlayLine`).
- Cancel on a choice, when `allowCancel`, cancels the session. No “back up one node”.
- `DialogueChoiceView` is not a speaker bubble and is not an entry in `ConversationLayoutPlan`.
- Ambient keeps walking `DialogueLineNode` only. A choice in an ambient asset ends that mutter chain, which is already what `NpcDialogueHost` does when the next node is not a line.
- Phase C stays out: consequences, quests, reputation, Camera Director, Cinemachine, scripted movement, animation sequencing.

---

## 1. Revision — conversation layout

### Previous proposal

Reserve every reachable speaker. For one or two speakers, keep Phase A. For three or more, lock Left/Right to the first two speakers in BFS order and stack everyone else on Above, with a hard cap of four regions.

### Why that does not hold

BFS order is not “who this conversation is with”. In `NPC A → choice → B or C → player`, the first two unique speakers can be A and B. B is exclusive to one branch. The player, who speaks on every path, would lose the side slot.

A cap of four then piles every extra speaker on one offset. Exclusive branch speakers never appear together, so they were being overlapped by a limit that counts the wrong set. A linear scene with five speakers would also change the Phase A fan, which sorts every speaker by screen X.

`BuildConversationPlan` already implements the approved one-speaker, two-speaker, and three-or-more fan. Phase B should call that function for the people who actually share the conversation, and keep its math intact.

`ShouldRebuildConversationPlan` rebuilds when the speaker count changes or when screen-X order flips. A branch speaker discovered late, or an off-screen exclusive NPC crossing the player in X, would reshuffle the core boxes. Reservation has to include branch speakers up front, and the rebuild test has to ignore them.

### Revised recommendation

Separate three sets. Do not go back to `PlaceInteractiveSticky` for interactive conversations.

| Set | Meaning | Has a view? |
|-----|---------|-------------|
| Reachable | Resolved speaker ids on nodes `DialogueGraph` can reach from the entry, after the Phase B availability filter | Not yet |
| Core | Speakers that appear on every simple path from the entry to a terminal | Slot from the existing Phase A builder |
| Displayed | Speakers who have actually said a line in this playthrough | Yes, spawned on first line, then left visible (Phase A) |

A simple path does not repeat a `nodeId`. A terminal is an `DialogueEndNode`, a node with no outgoing ids, or an intentional empty `nextNodeId`. Availability uses the same rule as the choice menu: `requiredCharacterId` and `EventCondition`, evaluated once at session start against the frozen character id and current `WorldState`.

**Core is the primary composition, and it is a pair only when two speakers are unavoidable.**

1. Intersect the speaker sets of every simple path.
2. Pass only that core list to the existing `BuildConversationPlan`.
   - 1 core speaker: Above and `conversationSoloLiftRef`.
   - 2 core speakers: sort those two by screen X, Left and Right. This is the Phase A pair.
   - 3 or more core speakers: the existing X-sorted fan. A linear asset has one path, so every speaker is core, and a linear 3-speaker scene stays on today’s fan.
3. Freeze each core `speakerId` to its side and `localOffsetScreen`.

If the entry is a choice and the intersection is empty, the core is the speaker set on the trunk before the first choice. If that trunk is also empty, there is no core pair: the largest set of speakers that can be displayed together is laid out once with `BuildConversationPlan`, and those slots are then frozen.

**Branch speakers are reachable speakers outside the core.** They include people who exist on only some choices.

Two branch speakers need different regions only if some simple path contains both. The number of extra regions is the largest number of branch speakers on any one simple path, not the number of reachable speakers.

Assign region indexes in BFS discovery order. Each branch speaker gets the smallest index that no co-occurring branch speaker already holds. Speakers who never share a path share an index. Ten villagers on ten exclusive branches add one extra region, not ten.

The stored result is still per `speakerId`, not one shared world position:

- The index supplies the starting recipe: Above, lift `(index + 1) * (conversationLiftRef + conversationSideGapRef)`, horizontal bias alternating `0, +band, -band, +2 bands…` where `band` is `conversationSideGapRef + width * 0.35`.
- A single plan-time pass then looks at each simple path, using that path’s anchors, and may increase only that branch speaker’s own lift or bias until their rect clears the other boxes on that path, or until the safe rect cannot fit them.
- Core side and offset are not written in that pass.
- Exclusive speakers are never in the same path check, so one villager’s correction does not move another villager.
- If a path still cannot fit, log once and keep the clamped offsets. The screen is finite. Playback does not search for a better hole later.

**Branch changes do not assign positions.** Every reachable id is already in the map before the first line. Choosing B shows B’s view on B’s frozen offset. C’s view is never spawned on that playthrough. A and the player keep the sides `BuildConversationPlan` gave the core. `GetOrCreateBubble` remains lazy.

**Displayed is a subset of the path actually taken, which is a subset of reachable.** Reservation is data. A bubble exists only after that speaker talks, and then stays, as in Phase A. Mutually exclusive speakers therefore do not overlap: they are not on screen together. Overlap solving runs against path sets, which are exactly the sets that can be displayed together.

**Rebuild**

- A choice resolving is not a rebuild.
- `ShouldRebuildConversationPlan` for this session considers the core snapshots only. A branch speaker walking off screen, or crossing someone in X, does not flip the core.
- The existing core invalidation remains: a core anchor leaves the safe area by `planRebuildDeltaRef`, or the core pair’s own X order flips by more than that distance. That rebuild runs the same algorithm again from the new screen positions. It is conversation-level, once, not a per-line sticky search.
- A speaker who was not reachable at start (a flag changed by something outside dialogue) gets a new branch index, `max + 1`, solved against the frozen core. Core slots stay.

**Phase A assets**

No choice nodes means one path, core equals every speaker, branch list empty, and `BuildConversationPlan` receives the same list it receives today. TestLab’s one- and two-speaker conversations stay on that function. Linear scenes with more speakers stay on it too. There is no slot cap to change them.

**Path enumeration**

The layout walk cuts cycles with the path-local visited set. If a malformed graph produces more than 1024 simple paths, stop enumerating, log a warning, and do not trust a partial intersection. Fallback, still computed once: core equals the trunk before the first choice; every other reachable speaker gets its own region index. Playback continues. This budget is not a limit on how long the player may talk.

`PlaceInteractiveSticky` and `TryPlaceAmbient` stay as they are. Ambient layout is unchanged.

---

## 2. Revision — completion outcome and OncePersistent

### Previous proposal

A missing destination warned and then `Finish()` with `WasCancelled == false`. That is a clean end. `NarrativeDialogueTrigger` would consume OncePerScene and OncePersistent.

### What the code does today

`WasCancelled` is read in one place outside the session.

| Consumer | What it does with the flag |
|----------|----------------------------|
| `DialogueSession.Cancel` | Sets `WasCancelled` and calls `Finish`. Used for user cancel, `CancelInteractive` (Interrupt), and service destroy. |
| `DialogueSession.Run` / `PlayLine` | Stops loops after cancel. |
| `NarrativeDialogueTrigger.OnInteractiveSlotFree` | `success = !startedSession.WasCancelled`. On success, `ConsumeOnce()` for OncePerScene and OncePersistent. Cooldown is applied either way. |
| `DialogueService.NotifyInteractiveFinished` | Clears the interactive slot and runs `EnqueueAfterInteractive` on every finish. The comment says callers who must not run after cancel should look at `WasCancelled`. No current caller inspects the flag before enqueue. The trigger’s own callback is the only enqueued policy, and it checks the flag inside the callback. |
| `DialogueService.HasInteractive` / `CancelInteractive` | Use `IsFinished` only. |
| `NpcDialogueHost` | Does not read `WasCancelled`. |

Phase A ends that are not cancels:

- `DialogueEndNode`, or a line whose `nextNodeId` is empty: loop exits, `Finish()`, flag stays false, Once is consumed.
- Non-empty `nextNodeId` that `FindNode` misses: `node` becomes null, same `Finish()`. Once is consumed. No warning.
- Unsupported node type: warning, then the same `Finish()`. Once is consumed.
- Null entry: warning, `Finish()` directly. Once would be consumed if a trigger had started that asset.

Failed `TryStartInteractive` never sets `startedSession`, so Once is not consumed. That path stays.

### Revised recommendation

Add `DialogueSessionOutcome` and keep `WasCancelled` as the cancel bit existing code already understands.

```text
None       session still running
Completed  author-intended end
Cancelled  user cancel, interrupt, service destroy
Failed     graph or traversal failure
```

`WasCancelled` becomes true only for `Cancelled`. It stays false for `Completed` and for `Failed`. `Cancel()` is the only writer of `Cancelled`. `Finish()` does not change an outcome that was already set.

`NarrativeDialogueTrigger.OnInteractiveSlotFree` consumes Once only when `Outcome == Completed`. Leaving it on `!WasCancelled` would treat `Failed` as success. This is the one logic change in that file. Cooldown still runs after `Cancelled` and after `Failed`, same as today’s cancel path, so a broken volume does not restart every frame. After the cooldown the player can enter again.

`NotifyInteractiveFinished` still runs deferred work for every outcome. Phase B has nothing that grants flags from that queue. Update the comment: Phase C applies consequences only for `Completed`. Do not drop the queue on `Failed` in this phase.

| Ending | Outcome | OncePersistent / OncePerScene |
|--------|---------|-------------------------------|
| `DialogueEndNode` | Completed | Consumed |
| Empty `nextNodeId` on a line or a choice option | Completed | Consumed |
| Choice whose options were all filtered out, empty `fallbackNodeId` | Completed | Consumed |
| User cancel, Interrupt, service destroy | Cancelled | Not consumed |
| `TryStartInteractive` returns false | Session never starts | Not consumed |
| Non-empty id that `FindNode` misses, including a bad `fallbackNodeId` | Failed | Not consumed |
| Null entry | Failed | Not consumed |
| Choice with no authored options and no fallback | Failed | Not consumed |
| Unsupported node type | Failed | Not consumed |
| Automatic cycle guard or the frame fuse (section 3) | Failed | Not consumed |

The two rows that differ from Phase A are the missing id and the null entry. Valid Phase A assets end on an empty `nextNodeId` or an `DialogueEndNode`, so their Once behavior does not change. A broken id stops counting as “the player finished the scene”.

`Finish()` still releases bubbles, the choice view, and the `GameplayLock` for every outcome. `Failed` is not a stuck session.

---

## 3. Revision — traversal guard

### Previous proposal

Stop the session after 256 node steps, including steps the player confirmed.

### Why that does not hold

A long authored scene can pass 256 lines. A shop loop (`choice → line → choice`) is a legal conversation and can run for as long as the player keeps confirming. The failure mode we actually have is Automatic mode walking a cycle with nobody present to stop it.

The layout walk is already protected by its visited set and by the 1024-path fallback in section 1. It does not need a playback cap.

### Revised recommendation

No session-wide step cap.

**Manual.** Each advance waits for Confirm. A repeated `nodeId` is allowed. A manual cycle ends when the player hits an end, cancels, or the asset reaches an empty next. It does not become `Failed` because it got long.

**Choice cycles.** Opening a choice waits for Confirm. When the player confirms an option, clear the automatic-visit set. Lines after that choice may repeat ids that played before the choice. The player can loop a menu indefinitely. That is not a failure. Automatic mode does not select the option (already approved).

**Automatic.** Keep a set of node ids entered by automatic advancement since session start or since the last choice confirmation. Confirm used only to skip a typewriter or the hold is still an automatic advance: it does not clear the set. Before entering a node on that path, if the id is already in the set, `Fail("auto-cycle")`: outcome `Failed`, warning, lock released, choice view destroyed, Once not consumed. A 500-line automatic scene with no repeated id plays through. `Line A → Line B → Line A` with no choice fails on the second visit to A. `Line A → choice → Line A` is allowed, because the confirmation cleared the set.

**Frame fuse.** This is not a length limit. If the runner enters more than 32 nodes in one frame, `Fail("traversal-fuse")`. Every current line yields at least once, and a choice yields while it waits, so normal manual, automatic, and choice-loop playback stay at one transition per frame. The fuse catches a bug that stops yielding. Outcome is `Failed`, so Once is not consumed.

---

## 4. Revision — choice menu presentation

### Previous proposal

`DialogueChoiceView` independent of the bubbles, hardcoded to the lower-center of the safe area.

### What stays

The view is still not a speaker and still not a `ConversationLayoutPlan` entry. Opening it does not rebuild speaker slots and does not write speaker `RectTransform`s. The session tells the view which labels exist and which index is selected. The view does not know node types.

### Revised recommendation

Placement is data, resolved by a small `DialogueChoiceLayout` that only returns a screen position for the panel.

```text
DialogueChoicePlacement
  LowerCenter          // Phase B default
  NearPlayerChatbox

DialogueChoicePresentation
  placement
  marginRef            // extra inset inside the dialogue safe rect, reference units
  offsetRef            // designer nudge, reference units
  maxWidthRef
  maxHeightRef
  gapFromBubbleRef     // used by NearPlayerChatbox
```

Defaults live on `DialogueLayoutSettings`, beside the other reference-unit layout values. A `DialogueAsset` may set `overrideChoicePresentation` and supply its own struct. The session uses the asset override when the flag is set, otherwise the service default. TestLab stays LowerCenter until someone changes the enum.

`DialogueChoiceLayout.Resolve` takes the presentation, the safe rect from `DialogueLayoutSolver.GetEffectiveSafeScreenRect`, the panel size, and the player bubble’s screen rect when one exists.

- **LowerCenter.** Put the panel on the bottom center of the safe rect, apply `marginRef` and `offsetRef`, then clamp the whole panel inside the safe rect. The safe rect already uses `DialogueUiSafeArea` (bottom inset included). This is not a raw `Screen.height / 2` constant in the session.
- **NearPlayerChatbox.** If the player bubble rect is missing, use LowerCenter for that open. Otherwise try below the bubble, then above, then the side with more room inside the safe rect, separated by `gapFromBubbleRef`. Then clamp the panel. Read the bubble rect. Do not write it.
- Both modes may run again each frame while the menu is open so a resolution change or a moving player (non-blocking session) keeps the panel in the safe area. That follow writes the choice view only.
- If the label list is taller than `maxHeightRef` or the safe rect, the view scrolls. Scrolling is internal to the view. Layout only provides the rect.

`DialogueUIController` grows an optional `choicePrefab`, same pattern as `ambientPrefab` and `interactivePrefab`. Null builds the placeholder list. A later art pass swaps the prefab. The session API stays `Show(labels, selectedIndex)` / `SetSelected` / `Hide`.

The choice rect is omitted from the speaker draft and from `BuildConversationPlan`. It may be included in `CollectScreenRects` so other overlays see it as occupied. That inclusion must not change speaker count or call a conversation rebuild.

---

## 5. Runtime shape (unchanged except where a revision says so)

### Data

- `DialogueLineNode` gains `textVariants`. `text` remains the default. Old assets deserialize an empty list.
- `DialogueChoiceOption`: `optionId`, `text`, `textVariants`, `nextNodeId`, `requiredCharacterId`, `[SerializeReference] List<EventCondition> availability`.
- `DialogueChoiceNode`: optional `promptText`, `options`, `fallbackNodeId`.
- New types stay in the global namespace, like the rest of `Dialogue/`.

### Playback

`DialogueGraph` lists outgoing ids for the session, the layout, and the editor.

- Line: `nextNodeId` if non-empty.
- End: none.
- Choice: each option that passes the filter, or `fallbackNodeId` when none do.
- Unknown type: none.

Empty id means an intended end (`Completed`). A non-empty id that `FindNode` misses is `Failed`.

The choice UI snapshots the available options when the node opens and does not re-filter every frame. Previous speaker bubbles stay visible. Fast-forward does nothing on the menu.

### Input bindings

| Action | Keyboard | Gamepad |
|--------|----------|---------|
| Previous option | W, Up | D-pad up, left stick up (threshold 0.5, re-center before the next step) |
| Next option | S, Down | D-pad down, left stick down |
| Confirm | E, Enter, South | unchanged |
| Cancel session | Esc, East | unchanged, only if `allowCancel` |

Hold-repeat for keys and d-pad: 0.28 s, then every 0.18 s.

---

## 6. File impact

### Create

| File | Role |
|------|------|
| `Dialogue/Data/DialogueTextVariant.cs` | Per-character text |
| `Dialogue/Data/DialogueChoiceOption.cs` | One response |
| `Dialogue/Data/DialogueChoiceNode.cs` | The menu node |
| `Dialogue/Runtime/DialogueTextResolver.cs` | Default plus variants |
| `Dialogue/Runtime/DialogueGraph.cs` | Edges, simple paths, core, co-occurrence |
| `Dialogue/Runtime/DialogueSessionOutcome.cs` | Completed / Cancelled / Failed |
| `Dialogue/Runtime/DialogueChoicePresentation.cs` | Placement enum and settings |
| `Dialogue/Runtime/DialogueChoiceLayout.cs` | Panel rect only |
| `Dialogue/Presentation/DialogueChoiceView.cs` | Labels and selection |

### Modify

| File | Change |
|------|--------|
| `Dialogue/Data/DialogueLineNode.cs` | `textVariants` |
| `Dialogue/Data/DialogueAsset.cs` | Optional presentation override. Validation warnings. |
| `Dialogue/Runtime/DialogueLayoutSettings.cs` | Default `DialogueChoicePresentation` |
| `Dialogue/Runtime/DialogueSession.cs` | Variants, choice state, outcome, auto-cycle guard, frame fuse. Speaker plan uses core plus branch indexes. |
| `Dialogue/Runtime/DialogueInput.cs` | Previous / next |
| `Dialogue/Runtime/DialogueLayoutSolver.cs` | New branch-offset helper. **Do not change** `BuildConversationPlan`’s 1 / 2 / 3+ math. Session passes core-only snapshots to the existing rebuild test. |
| `Dialogue/Presentation/DialogueUIController.cs` | `choicePrefab`, spawn and release |
| `Dialogue/Runtime/DialogueService.cs` | Comment on the deferred queue: Phase C checks `Completed`. |
| `Dialogue/NPC/NarrativeDialogueTrigger.cs` | `ConsumeOnce` only when `Outcome == Completed`. |
| `Editor/Dialogue/DialogueAssetEditor.cs` | Add Choice. Id popup. Structure summary. Condition add-menu for the four existing condition types. |
| `Editor/Events/EventSystemTestLabBuilder.cs` | Separate Phase B menu. Do not call `EnsureDialogueAsset` on Phase A assets. |

### Leave alone

- `BuildConversationPlan` body, `PlaceInteractiveSticky`, `TryPlaceAmbient`.
- `GameplayLock`, `WorldState`, save, `EventRunner`, `EventAction`, existing condition classes, `EventRunnerEditor`.
- `NpcDialogueHost` control flow. Optional later warning if its ambient asset contains a choice.
- `DialogueCharacterAssetResolver`, `DialogueActiveCharacter`.
- Sprint 04 prompts, `PlayerMotor`, cameras, Cinemachine, production prefabs.

---

## 7. Implementation sequence

Each step is playable before the next. None of them add `StartDialogueAction` or consequences.

1. **Line variants.** Resolver plus `PlayLine`. One shared graph, Ax and Jyn text, fallback when the id has no variant. Layout untouched.
2. **Outcome.** `DialogueSessionOutcome`. Missing id and null entry return `Failed`. Trigger consumes Once only on `Completed`. Prove it with a OncePersistent pad before any choice UI exists.
3. **Choice menu, default LowerCenter.** Node, view, `DialogueChoiceLayout`, input, cancel, confirm suppression. Options converge on a shared line. Presentation comes from `DialogueLayoutSettings`.
4. **Core and branch slots.** `DialogueGraph` paths, core passed through the untouched Phase A builder, branch indexes, plan-time separation, no rebuild on select. Fixture with NPC B versus NPC C.
5. **Filters.** Label variants, `requiredCharacterId`, one `FlagIsTrueCondition`. Filtered speakers are not reachable, so they get no slot. Empty filtered list follows `fallbackNodeId` as `Completed`.
6. **Guards.** Automatic re-entry fails the session. A choice confirmation clears the set and the loop continues. A long manual chain and a long choice loop do not fail. Frame fuse only on a same-frame runaway.
7. **NearPlayerChatbox.** Asset override, safe-area clamp, panel follows the player bubble in a non-blocking session. Speaker boxes do not move when the menu opens or when the player moves.
8. **Inspector and fixtures.** Id popups, warnings, Phase B menu.
9. **QA** in section 9. Stop for review.

---

## 8. TestLab

**Tools → ARPG → Events → Add Dialogue Test Fixtures (Phase B)** creates new objects and new assets only. `EnsureDialogueAsset` replaces `nodes` every time it runs; the Phase B menu must not point it at `Lab_Interactive` or the other Phase A assets.

Place the new row away from z ≈ −20 and z ≈ −24.

| Object | Purpose |
|--------|---------|
| `NPC_Choice_Converge` | Three options, three short lines, one shared line, End |
| `NPC_Choice_BranchSpeakers` | Core pair plus exclusive NPC B or NPC C, then the player line |
| `NPC_Choice_Variants` | Same graph, `jyn` and `ax` line text and labels, plus a default |
| `NPC_Choice_Gated` | One option by `requiredCharacterId`, one by `FlagIsTrue` |
| `NPC_Choice_BadTarget` | One option id does not exist |
| `NPC_Choice_Auto` | Automatic trigger that reaches a choice and waits |
| `NPC_Choice_AutoLoop` | Automatic lines that cycle with no choice |
| `NPC_Choice_MenuLoop` | Choice returns to itself after a line |
| `Trigger_Choice_OnceCancel` | OncePersistent, cancel on the menu |
| `Trigger_Choice_OnceFail` | OncePersistent, bad destination |
| `Trigger_Choice_Interrupt` | Interrupt while the menu is open |
| `NPC_Choice_NearPlayer` | Asset override `NearPlayerChatbox`, non-blocking |

Phase A fixtures stay where they are and are the regression set. Do not regenerate them.

---

## 9. QA

### Choices and variants

- Keyboard and gamepad move one row per press. Stick and key repeat match section 5. The selected row is visibly distinct.
- The Confirm that finishes the question does not select. The Confirm that selects does not fill the next typewriter.
- All three converge options reach the shared line.
- Cancel closes the view, returns movement, and does not write the Once flag.
- Interrupt destroys the view, releases the lock, does not consume the interrupted Once, and starts the new session.
- Jyn sees Jyn text and labels. Ax sees Ax. A missing variant shows the default. The frozen id does not follow a party swap.
- A gated option is absent. Its speaker has no bubble and did not take a core slot. A fully filtered choice with an empty fallback completes and does consume Once.
- Automatic lines advance on their own and stop on the choice until Confirm.
- The menu loop can be confirmed well past a few dozen laps. It does not fail and does not move the core boxes.

### Layout

- `NPC_Interactive` and the other Phase A two-speaker talks keep Left/Right.
- A one-speaker talk keeps the solo Above lift.
- A linear three-speaker asset, if authored for this check, still uses the Phase A X-sort fan.
- On `NPC_Choice_BranchSpeakers`, A and the player do not change side when B is chosen. B appears on the reserved branch offset. C is not on screen. Talking again and choosing C uses C’s offset and still does not swap A and the player.
- Two exclusive branch speakers may share a region index. They are never visible together.
- A path with several co-occurring branch speakers gets that many regions, solved once. Boxes that cannot fit stay clamped and do not hunt for a new slot on later lines.
- Opening the menu does not move speaker bubbles.

### Outcome and guards

- Clean End and empty `nextNodeId` set the Once flag.
- Cancel and Interrupt do not.
- `NPC_Choice_BadTarget` and a null-entry asset do not set the flag. After cooldown, the pad can fire again. The lock is not held.
- `NPC_Choice_AutoLoop` fails on the repeated automatic node, does not set Once, and releases the lock.
- A long automatic chain with unique ids completes and does set Once.
- Manual play through a repeated line does not fail.

### Choice presentation

- Default fixtures sit in the lower center of the dialogue safe rect, inset by `marginRef`, fully on screen at the TestLab aspect.
- `NPC_Choice_NearPlayer` sits off the player bubble by `gapFromBubbleRef` and stays inside the safe rect. While the session is non-blocking, walking moves the panel and does not move the NPC bubble.
- Clearing `choicePrefab` still shows the placeholder. A prefab, when assigned, receives the same labels and selection index.

### Phase A regression

Ambient without lock or portrait. Interact prompts, including Hidden. Both dismisses ambient. Typewriter, second Confirm to advance, Shift / Right Shoulder to fast-forward. Ignore, Queue, Interrupt. OncePersistent on the existing narrative pad only after a clean finish. Party swap locked while `lockGameplay` is on.

---

## 10. Risks

| Risk | Mitigation |
|------|------------|
| Partial path intersection marks a skippable speaker as core | The 1024-path stop refuses the partial intersection and uses the trunk fallback. |
| Core rebuild on camera still swaps the pair | Same rule as Phase A, and only for the core. Branch choice does not call it. |
| `!WasCancelled` left in the trigger | Implementation step 2 changes that condition to `Outcome == Completed` before choice fixtures depend on it. |
| Deferred queue runs after `Failed` | No Phase B reward uses it. The comment tells Phase C to require `Completed`. |
| Automatic skip-Confirm hides a cycle | Skip does not clear the auto-visit set. Only a choice confirmation does. |
| Near-player panel tracks into a speaker bubble | Clamp keeps it in the safe rect. It never writes the speaker rect, so the speaker plan stays put even if the panel overlaps visually. Overlap of the menu with a bubble is accepted; moving the bubble is not. |
| Phase A menu regenerates lab assets | Phase B menu uses new asset paths. |

---

## 11. Decisions still requiring approval

Reply on these four. Implementation waits on them.

1. **Layout.** Core = intersection of speakers on every simple path, laid out only by the existing `BuildConversationPlan`. Extra regions = the most branch speakers on any one path. Exclusive speakers share an index and keep their own frozen offsets. No cap of four. No BFS-first pair. One- and two-speaker linear scenes stay on the current builder because they have no branch list.
2. **Outcome.** `Completed` / `Cancelled` / `Failed`. Once is consumed only for `Completed`. `WasCancelled` stays “this was a cancel”. Missing destination, null entry, unsupported node, automatic cycle, and the frame fuse are `Failed` and do not consume Once. Cooldown still applies after `Failed`. This changes Phase A only for a bad id and a null entry; empty next and `DialogueEndNode` still complete.
3. **Guard.** Delete the 256-step cap. Manual play and choice loops are unbounded. Repeating a node under Automatic without a choice confirmation since the last visit is `Failed`. More than 32 node entries in one frame is `Failed`.
4. **Choice presentation.** `DialogueChoicePresentation` on `DialogueLayoutSettings`, optional per-asset override. Default `LowerCenter`. `NearPlayerChatbox` ships in Phase B and falls back to LowerCenter when the player has no bubble. Safe-area clamp. Prefab-swappable view. Speaker slots are not moved.

No other Phase B decisions are waiting. Phase C remains unstarted.
