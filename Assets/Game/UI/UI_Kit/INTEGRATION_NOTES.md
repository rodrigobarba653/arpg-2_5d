# UI Kit v2 — integration notes

## What was built

1. **Sprite import** — `Tools → ARPG → UI → 1. Configure UI Kit Sprites`  
   Sets Sprite (UI), Full Rect, Alpha Is Transparency, Compression None, and README borders:
   - Dialogue 9-slice: L/R 264, T/B 100
   - Notification 9-slice: L 230, R 100, T/B 60  
   Writes `Assets/Game/UI/UI_Kit/UiKitSprites.asset`.

2. **Visual test scene** — `Tools → ARPG → UI → 2. Build UI Kit Visual Test Scene`  
   Creates `Assets/Game/Scenes/UIKit_VisualTest.unity` with:
   - 3 chatbox widths (portrait on)
   - 4 tail directions
   - 2 notification sizes  
   Uses **assembled 9-slice only** (not corners+edges at the same time).  
   Tail uses `tail_*` + `tail_connection_mask` for a seamless join.

## Verify before runtime wiring

Open the visual test scene and check:

- [ ] Corners stay sharp at all three widths  
- [ ] Portrait frames are not stretched  
- [ ] Each of the four tails joins without a double border (mask hides frame edge)  
- [ ] Notifications: icon left, text right; no pointer  
- [ ] No corner/edge PNGs drawn together with the 9-slice frame  

## Runtime (wired)

After **Configure UI Kit Sprites** (copies to `Assets/Game/Resources/UiKitSprites.asset`):

- `DialogueUIController` builds interactive/ambient chatboxes with kit 9-slice + portrait frame + tail/mask  
- `RewardNoticePanel` uses notification 9-slice (icon left / text right)  
- `ItemPickupNotifier.Show` prefers kit screen toasts when the kit is present  

Progression, EventRunner, flags, and layout solver are unchanged — chrome only.

Fallback: if kit sprites are missing, legacy flat panels still build.
