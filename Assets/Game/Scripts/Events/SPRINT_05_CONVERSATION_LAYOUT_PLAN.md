# Sprint 05 — Conversation-Level Chatbox Layout

**Status:** Phase implemented — awaiting manual QA (`SPRINT_05_CONVERSATION_LAYOUT_QA.md`).  
**Scope:** Interactive chatboxes only. Ambient unchanged (except existing interactive priority).  
**Goal:** Precompute a stable multi-speaker composition **before the first line**; speakers keep their regions for the whole conversation so boxes do not jump when the turn changes.

---

## Problem (current)

Today `DialogueSession` places **one speaker at a time**:

1. First line → create bubble → `PlaceInteractiveSticky` picks a free slot (often Above / nudge).
2. Second speaker appears → solver treats the first bubble as a blocker → **pushes the new (or old) box**.
3. Sticky state is per-bubble and discovered **reactively**, so later speakers reshape the layout.

`DialogueLayoutSolver.PlaceInteractiveSticky` is a good **local** tool (safe area, avoid characters, overlap), but the **policy** is wrong: conversation composition is decided incrementally instead of up front.

Result: jumps, boxes drifting away from speakers, recomposition on every new voice.

---

## Target model

```text
Session Start
  → freeze ActivePlayer id + initiating Transform (already done)
  → CollectSpeakers(asset)           // walk reachable Line nodes
  → Resolve anchors (existing rules)
  → Project to screen (Camera.main)
  → AssignConversationSlots(...)     // ONE shared plan
  → store ConversationLayoutPlan

Each visible line
  → show/hide bubble for that speaker
  → follow: screen = WorldToScreen(anchor) + slot.localOffset
  → do NOT re-run full slot assignment

Rare invalidation only
  → camera/framing shift large
  → speaker left viewport
  → unexpected speaker
  → plan no longer viable (hard collision)
```

**Acceptance:** After conversation start, each speaker keeps an assigned region. Changing who talks must not teleport prior/future boxes without a valid invalidation reason.

---

## Audit — what to reuse

| Piece | Keep / adapt |
|-------|----------------|
| `DialogueSession` speaker resolve (`active_player`, npc, `Party.FindByCharacterId`) | **Reuse** — single identity source |
| Session-frozen ActivePlayer Transform | **Reuse** |
| `DialogueLayoutSolver` safe area, RefToScreen, avoid rects, clamp | **Reuse** as geometry helpers |
| `PlaceInteractiveSticky` as per-frame discoverer | **Stop using for interactive conversation policy** (or demote to rare repair) |
| Ambient `TryPlaceAmbient` | **Unchanged** |
| Interactive > ambient dismiss | **Unchanged** |

**Do not** invent a parallel speaker-id system.

---

## Proposed architecture

### 1. Precompute speakers (`DialogueSession` / small helper)

Before first `PlayLine`:

```csharp
// Pseudocode
speakers = CollectUniqueResolvedSpeakerIds(Asset)
// Walk from entry via nextNodeId; Phase A: Line + End only.
// Resolve each line.speakerId via DialogueActiveCharacter.ResolveSpeakerId(sessionId)
// Ignore End / empty
```

For each speaker id:

- Resolve `Transform` with existing `ResolveAnchorForResolvedId`
- Skip / warn if transform missing (still reserve a screen-side slot from last known / safe fallback)

Phase A graphs are linear enough to collect by walking `nextNodeId`.  
*(Phase B/C with branches: collect **union of all reachable** Line nodes from entry, or author optional “participants” list on the asset — prefer reachable walk to avoid a second authoring path.)*

### 2. `ConversationLayoutPlan` (new small data type)

Owned by the session, computed once (or on rare rebuild):

```text
ConversationLayoutPlan
  speakers[]:
    speakerId
    Transform anchor          // live follow
    SlotSide: Left | Right | AboveBand | BelowBand  // composition role
    screenAnchor0             // snapshot at plan time
    localOffsetRef            // fixed offset in ref UI units within the slot
    reservedRectHint          // optional AABB for occupancy / ambient yield
```

**Composition rules (2 speakers — typical Ax ↔ NPC):**

1. Compare screen X of player vs NPC (and optionally Y for N/S).
2. Prefer **horizontal split**:
   - Player left of NPC on screen → Player **Left** slot, NPC **Right** slot (or reverse).
3. If nearly same X (N/S stack):
   - Still prefer L/R composition for legibility (player gets the side with more margin; NPC the other).
4. Do **not** require “directly above head”.
5. Association = side + modest offset toward the character, clamped to safe area.

**3+ speakers:** fan Left / Center-Left / Center-Right / Right using sorted screen X; if too crowded, stack secondary band (upper/lower) **as part of the same plan**, not per-line panic moves.

### 3. Reserve, then show

- Allocate slots for **all** planned speakers before line 1.
- Spawn or pre-register sticky/slot state for each id **without** requiring the line to have played (views can stay hidden/`alpha=0` until first line for that speaker).
- Only the active (or recently spoken) bubbles are visible; **slots remain reserved**.
- Turning NPC → Player → NPC **reuses the same slots**.

### 4. Per-frame behavior (stable follow)

Each frame for visible bubbles:

```text
screen = WorldToScreen(anchor.position)
pos    = ComposeSlotPosition(screen, plan.slot, plan.localOffset) 
pos    = ClampToSafe(pos)
apply immediately (no lerp to hide bad layout)
```

- Soft tracking inside the slot only (offset locked; position follows world).
- **No** full `PlaceInteractiveSticky` search every frame.
- **No** lerp/transitions as a substitute for a bad plan (remove slot-change lerp from the interactive conversation path, or keep only for rare rebuild).

### 5. When to rebuild the plan

Rebuild only if:

| Trigger | Example |
|---------|---------|
| Camera / framing shift | Anchor screen delta or camera pose change beyond threshold |
| Speaker left viewport | Anchor off-screen past margin |
| Unexpected speaker | Line speaker not in initial set (branch / authoring miss) |
| Plan invalid | Reserved regions overlap hard, or cover player beyond reject ratio |

On rebuild: recompute **all** slots together (conversation-level), then snap (or one short shared transition — optional; default **snap** to avoid masking errors).

### 6. Collision avoidance (conversation-level)

Priorities (as requested):

1. Legible, on-screen dialogue  
2. No chatbox–chatbox overlap  
3. Clear visual association with speaker  
4. Don’t cover characters / HUD  
5. Minimize unnecessary movement  

Implementation: score **candidate compositions** (L/R, R/L, L/R with vertical bias, etc.) once for the full speaker set; pick best score. Individual boxes do not “steal” space from each other mid-conversation.

### 7. Solver API shape (single source of truth)

Extend `DialogueLayoutSolver` (do not fork):

```text
BuildConversationPlan(speakers[], sizeHint, preferred) → ConversationLayoutPlan
FollowSlot(view, planEntry, anchorScreen)             → set position
TryRebuildPlan(...)                                   → only on invalidation
```

Deprecate interactive use of reactive `PlaceInteractiveSticky` for multi-speaker sessions (keep temporarily for single-speaker or as repair helper inside rebuild).

Ambient continues to use `TryPlaceAmbient` + interactive rect blockers from `CollectScreenRects`.

---

## Files (when authorized)

| File | Change |
|------|--------|
| `ConversationLayoutPlan.cs` | **New** — plan + per-speaker slot |
| `DialogueLayoutSolver.cs` | `BuildConversationPlan` / `FollowSlot`; interactive sticky demoted |
| `DialogueSession.cs` | Collect speakers → build plan before first line; follow slots; rare rebuild |
| `StickyPlacementState.cs` | Likely unused for interactive conversation path (or store slot id only) |
| QA notes | Geometry cases L/R, R/L, N/S, camera, 3+ speakers |

**Out of scope:** ambient redesign, Phase B line variants, Party changes, combat/camera systems.

---

## QA (post-implementation)

| # | Case | Expect |
|---|------|--------|
| 1 | Player left / NPC right | Stable L/R for whole talk |
| 2 | Player right / NPC left | Stable R/L |
| 3 | Player south / NPC north | Still L/R composition if clearer |
| 4 | Player north / NPC south | Same |
| 5 | Ax → NPC → Ax repeats | **No jump** of either box |
| 6 | Camera move | Follow inside slots; rebuild only if threshold hit |
| 7 | Near screen edge | Clamped; slots preserved if possible |
| 8 | 3+ speakers | Preassigned fan; no per-line reshuffle |

**Main criterion:** once the conversation starts, chatboxes keep their assigned regions. Speaker changes alone must not re-roll positions.

---

## Phasing suggestion (implementation, after approval)

1. Collect speakers + build L/R plan for 2 speakers; follow-only per frame.  
2. Hide/show without releasing slots; kill reactive sticky for interactive.  
3. Invalidation + rebuild; 3+ speaker fan.  
4. QA matrix above.

Stop after each step if you want mid-gate checks; default is ship 1–3 together then QA.
