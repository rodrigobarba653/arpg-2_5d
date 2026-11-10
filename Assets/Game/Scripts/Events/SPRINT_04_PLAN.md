# Sprint 04 — Interaction & Discovery (Approved Plan)

Sprints 01–03 approved. Implement only after this plan. **Stop after Sprint 04. No Sprint 05.**

## Prompt Modes (per interactable)

| Mode | Discovery (?) | Interaction (!) | Interact allowed |
|------|---------------|-----------------|------------------|
| **Hidden** | never | never | yes, inside interaction radius |
| **Interaction Only** | never | yes, in interaction radius | only in interaction radius |
| **Discovery + Interaction** | yes, in discovery (outside interaction) | yes, in interaction radius | only in interaction radius |

- Prompt visibility **never** gates interaction eligibility.
- `discoveryRadius >= interactionRadius` (Inspector warning if violated).
- Objects showing only `?` are **not** interactable until inside interaction radius.

## Selection policy (anti-misleading)

**Input focus** (who receives Interact press):

1. Prefer eligible interactables **within interaction range that would display `!`** (Interaction Only or Discovery+Interaction).
2. If several `!` candidates → **nearest**.
3. If none → nearest eligible **Hidden** within interaction range.
4. Else → no interaction this frame.
5. One press → at most one object.

**Prompt focus** (what appears above the player):

- Ignore Hidden for prompt selection (Hidden must never suppress another object's visible prompt).
- Among non-Hidden eligible/discoverable targets, show `!` or `?` for the nearest appropriate candidate.
- If input focus is Hidden and a farther object shows `!`/`?`, the **visible** object still owns the prompt; the **press** still follows input rules above (so a `!` object always wins the press over Hidden).

Because rule 1 prefers `!` for input, a visible `!` always beats a nearby Hidden for the press.

## FFIX-style player-owned prompt

Hierarchy (do not change):

```
Jyn / Ax (PlayerPrefab)
└── SpriteBody
    ├── WeaponBody
    ├── Prompt          ← PlayerInteractionPrompt + BillboardToCamera
    └── FeetBody
```

Rules:

1. Preserve existing Prompt under SpriteBody — do not move, reparent, or duplicate.
2. Do **not** instantiate prompts on interactables.
3. Do **not** place the prompt at the interactable world position or make it follow the target.
4. `InteractionDetector` only decides: selected interactable + display state (`?` / `!` / none).
5. `PlayerInteractionPrompt` owns presentation, animation, visibility, local offset.
6. Reuse QuestionMark Intro/Idle/Outro; add `!` via **sprite swap** on the same Prompt when possible (avoid new animator states / hierarchy unless required).
7. Preserve SpriteBody billboard + existing prompt local offset.
8. On Jyn↔Ax swap, only the **active** player's prompt is shown.
9. Hidden mode → no prompt above player; interaction still works.
10. Interactable `InteractionPosition` is **only** for distance / selection — never for prompt placement.
11. Disable legacy **object-owned** prompts only on objects explicitly migrated to the central detector.

## Architecture

- `IInteractable` + `InteractableRegistry`
- `InteractionDetector` (follows `Party.Active`; no Motor/combat/camera edits)
- `WorldInteractable` designer component (radii, PromptMode, EventRunner, conditions)
- Opt-in migration: `useCentralDetector` disables local Interact polls + object prompts
- Unmigrated doors/switches/pickups/teleporters/ladders keep Sprint 03 behavior

## Exclusions

Dialogue, Camera Director, scripted move, anim overrides, chests, quest markers, camera targeting, complex priority, input rebinding, PlayerMotor/combat/anim/camera changes.

## QA highlights

- `?` → `!` on approach; Interaction Only; Hidden (no prompt, still works)
- Overlap: `!` visible + Hidden nearer → press goes to `!` object; prompt stays on visible (`!`)
- Overlap: only Hidden in range → interact, no prompt
- Nearest of two `!`; availability / Once / GameplayLock; one press one event
- Prompt always above active player head (FFIX); never on the interactable
- Unmigrated doors/switches/pickups/teleporters/ladder + Sprint 01–03 regression

## Status

Implemented (code). Run **Tools → ARPG → Events → Add Interaction/Discovery Test Fixtures** then Play QA.
