# Sprint 05 — Triggered Narrative Dialogue QA (Phase A)

## Once consumption policy (implemented)

| Event | OncePerScene / OncePersistent |
|-------|-------------------------------|
| Failed `TryStartInteractive` | **Not** consumed — can retry |
| Cancel mid-dialogue | **Not** consumed — can re-enter |
| Session finishes cleanly (`!WasCancelled`) | **Consumed** |
| Stay inside after fire | No re-fire until **exit + re-enter** (and cooldown / Once rules) |

Persistent flag: `dialogue.narrative.{triggerId}.seen` via `WorldState`.

Conflict:
- **Ignore** — drop activation if busy  
- **Queue** — one pending; starts when free; cancelled on exit if `cancelPendingOnExit`  
- **Interrupt** — cancel active interactive immediately, then start this trigger (interrupted Once is **not** consumed)

---

## Setup
1. Exit Play  
2. **Tools → ARPG → Events → Add Dialogue Test Fixtures (Phase A)**  
3. Play — yellow pads at **z ≈ −24**

| Object | Mode |
|--------|------|
| `Trigger_Narrative_Auto_NonBlocking` | Auto + non-blocking + Queue |
| `Trigger_Narrative_Manual_Blocking` | Manual + blocking + Ignore |
| `Trigger_Narrative_OncePersistent` | Auto + non-blocking + OncePersistent |

---

## Cases

| # | Steps | Expect |
|---|--------|--------|
| 1 | As **Jyn**, walk Auto pad | Speaks as Jyn, no Interact; can move; camera follows |
| 2 | As **Ax**, same pad | Speaks as Ax |
| 3 | Stay inside Auto pad | No spam; leave + re-enter after cooldown to repeat |
| 4 | Manual Blocking pad | Movement locked; Confirm advances; unlocks after |
| 5 | Auto pad while talking to NPC Interactive | Queued; after NPC ends (if still inside / policy), narrative starts |
| 5b | Start Auto, walk into **Manual Blocking** (Interrupt) | Auto cancels; Manual starts immediately |
| 6 | Queue then **leave** pad before free | Pending cancelled (`cancelPendingOnExit`) |
| 7 | OncePersistent pad once | Plays; second enter after cooldown does **not** |
| 8 | Save after OncePersistent, reload | Still suppressed (`WorldState` flag) |
| 9 | Ambient + NPC Interact | Unchanged regression |

---

## Camera note
Blocking stops the motor via `GameplayLock` → follow cam idles. Non-blocking does not touch Cinemachine.
