# Sprint 05 — Conversation Layout QA

**Gate:** Chatboxes must keep assigned regions after conversation start.

## Setup
1. Exit Play → regenerate Dialogue fixtures if needed  
2. Play `EventSystem_TestLab`  
3. Talk to `NPC_Interactive` (ActivePlayer ↔ Villager A)

## Cases

| # | Steps | Expect | Fail if |
|---|--------|--------|---------|
| 1 | Player left of NPC | Player bubble stays **left**, NPC **right** whole talk | Swap/jump on turn change |
| 2 | Player right of NPC | Player **right**, NPC **left** | Jump when Ax speaks again |
| 3 | Player south / NPC north | Still stable L/R composition | Boxes teleport Above each turn |
| 4 | Player north / NPC south | Same | — |
| 5 | Ax → NPC → Ax → NPC | **No jump**; same slots | Second Ax line moves the box |
| 6 | Mild camera pan | Follow inside slots | Full re-layout every frame |
| 7 | Near screen edge | Clamped; slots kept if possible | Off-screen forever |
| 8 | Cover check | Boxes do not sit on Ax/Jyn sprite | Player covered |

## Notes
- Plan is built **before** the first line (`[Dialogue] Conversation layout plan built…` in console if debugLog).  
- Rebuild only on large framing flip / offscreen / unexpected speaker.  
