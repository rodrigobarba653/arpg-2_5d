# Sprint 05 — Active Player Phase A QA

**Gate:** Approve this before Phase B (per-line variants).

## Setup
1. Exit Play Mode  
2. Open `EventSystem_TestLab`  
3. **Tools → ARPG → Events → Add Dialogue Test Fixtures (Phase A)**  
4. Confirm party members have `CharacterDefinition.id` = `jyn` / `ax`  
5. Save → Play  

Confirm: **E / Enter / South** · Cancel: **Esc / East**

---

## Cases

| # | Steps | Expect | Fail if |
|---|--------|--------|---------|
| 1 | As **Jyn**, talk to `NPC_Interactive` | Player lines show **Jyn** (roster name); anchor near Jyn | Shows Ax / wrong name |
| 2 | Swap to **Ax**, talk again to `NPC_Interactive` | Same asset; player lines show **Ax** | Still says Jyn |
| 3 | As **Jyn**, talk to `NPC_ActivePlayer_Variants` | Villager opens with **“Jyn! Good to see you.”** | Fallback or Ax line |
| 4 | As **Ax**, talk to `NPC_ActivePlayer_Variants` | **“Ax! Didn't expect you.”** | Jyn line / fallback |
| 5 | Clear overrides on a copy / use host with only fallback | Shared fallback asset plays for both | Null / crash |
| 6 | During interactive (lock on) | Name/portrait stay on session character; chatbox follows that Transform | Rebinds if Party.Active changes |
| 7 | Regression `NPC_Both` / ambient | Ambient + interactive still work | Softlock / overlap |

---

## Inspector (authoring)

### Shared ActivePlayer line
- `DialogueLineNode.speakerId` = `active_player` (aliases: `player`, `party`, `hero`)
- Roster must include the character ids used by party (`jyn`, `ax`, …)

### Character-specific assets (`NpcDialogueHost`)
- `interactiveDialogue` = shared **fallback**
- `interactiveByCharacter` list:
  - Element: `characterId` = `jyn`, `asset` = Jyn dialogue
  - Element: `characterId` = `ax`, `asset` = Ax dialogue

Resolution: `DialogueCharacterAssetResolver` (also for future `StartDialogueAction`).

---

## Known limitations (Phase A)
- No per-line text variants (Phase B)  
- No character-specific conditions/consequences (Phase C)  
- `StartDialogueAction` not wired yet — resolver is ready to reuse  
- Portrait sprites in TestLab roster may be null (name still resolves)  
- If initiating Transform is destroyed mid-session, that bubble stops world-follow (safe null)  
