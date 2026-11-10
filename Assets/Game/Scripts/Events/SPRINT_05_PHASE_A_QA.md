# Sprint 05 — Phase A QA (TestLab)

## Setup
1. Exit Play Mode  
2. Open `EventSystem_TestLab`  
3. **Tools → ARPG → Events → Add Dialogue Test Fixtures (Phase A)**  
4. Save → Play  

NPCs sit at **z ≈ −20** along +X.  
Confirm dialogue: **E / Enter / Gamepad South**  
Cancel: **Esc / Gamepad East**  
Fast-forward typewriter: **Right Shoulder / Left Shift only** (not hold-E)  

Interactive (FFIX-style): chatboxes **float near the speaking character**, sticky slot, clamp to safe area.  
Multi-speaker: each bubble follows its own anchor (ActivePlayer → session Transform; NPC → `speechAnchor`).  
Ambient unchanged except yielding to interactive. Interaction radius is **360°**.  
Active-player cases: see `SPRINT_05_ACTIVE_PLAYER_QA.md`.  
Conversation layout (no jump on turn change): see `SPRINT_05_CONVERSATION_LAYOUT_QA.md`.

---

## Cases

| # | Object | Steps | Expect | Fail if |
|---|--------|-------|--------|---------|
| 1 | `NPC_Ambient_A` | Walk near | Small chatbox appears, auto typewriter, auto dismiss | No bubble / needs E |
| 2 | Ambient | Watch bubble | **No portrait** | Portrait shows |
| 3 | Ambient | Move while talking | Player **keeps moving** | Movement locked |
| 4 | `NPC_Ambient_A/B` | Approach both | Up to **2** bubbles; no overlap; interactive reserve respected | Stacked on top of each other |
| 5 | Ambient leave | Walk away mid-line (`dismissOnExit`) | Bubble dismisses | Stays forever |
| 6 | Cooldown | Leave and re-enter quickly | Waits ~3s cooldown before repeat | Spam every frame |
| 7 | `NPC_Interactive` | Approach: `?` then `!` · **E** | Large chatbox near speaker, lock movement | Opens ambient-style / can walk |
| 8 | Interactive | Same **E** that opens | First line starts typing — **does not** skip to line 2 | Skips first line |
| 9 | Interactive | Tap Confirm while typing | Full line reveals; **another** tap advances | Same tap advances |
| 10 | Interactive | Hold Confirm | Faster typewriter | No effect |
| 11 | Interactive | Esc / East | Ends, **movement returns** | Softlock |
| 12 | `NPC_Interactive_NoPortrait` | Talk | Layout works without portrait panel | Broken UI |
| 13 | `NPC_Both` | Near without E | Ambient mutter | — |
| 14 | `NPC_Both` | **E** | Interactive starts; ambient dismissed | Both fight / overlap |
| 15 | `NPC_HiddenPrompt` | Near | **No** `?`/`!` · **E** still opens dialogue | Prompt shows or can't talk |
| 16 | Camera move | During ambient/interactive | Anchors stay on-screen (clamped) | Off-screen forever |
| 17 | Sprint 04 pads | z≈−14 | `?`/`!` still work | Regression |
| 18 | Sprint 03 doors | If present | Switches still work | Regression |
| 19 | Multi-speaker | Lines swap Jyn ↔ Villager | Each bubble tracks its speaker; sticky unless collision/edge | Jumps every frame / wrong anchor |
| 20 | Screen edge | Talk near map edge / zoom | Bubble stays in margins; readable | Clipped / illegible |
| 21 | Resolutions | 16:9 and tall/narrow Game view | Layout uses Canvas scale + ref units | Broken only at one res |

---

## Inspector cheat-sheet
- `NpcDialogueHost.mode` = AmbientOnly / InteractiveOnly / Both  
- Ambient: asset, proximity, cooldown, hold, dismissOnExit  
- Interactive: asset, lockGameplay, promptMode, preferred (**Auto**), offset (ref units), maxWidth override  
- `DialogueService.layoutSettings`: margin 24, speaker offset 24, max width 420, transition 0.15s  
