# Dialogue Authoring Editor — Architecture Fix Report

Corrective pass over the Dialogue Editor + Start Dialogue UX. No second dialogue engine.

## Files changed

### New
- `Assets/Game/Scripts/Dialogue/Data/DialoguePortraitMode.cs`
- `Assets/Game/Scripts/Dialogue/Data/DialogueRememberFlag.cs`
- `Assets/Game/Scripts/Dialogue/NPC/DialogueSpeakerBinding.cs`
- `Assets/Game/Scripts/Dialogue/NPC/DialogueSpeakerBindingLookup.cs`

### Updated
- `DialogueLineNode.cs` — `portraitMode`
- `DialogueChoiceOption.cs` — `rememberChoice`, `rememberAs`, `rememberFlagId`
- `DialoguePortraitResolver.cs` — Default / Custom / Hidden + NPC binding default
- `DialogueSession.cs` — multi-NPC anchors via binding lookup; remember flags; display name from binding
- `StartDialogueAction.cs` — no longer requires Tied To for authored multi-NPC lines
- `StartDialogueActionDrawer.cs` — simple Start Dialogue UI
- `DialogueEditorWindow.cs` — per-line speaker / portrait / choice UX
- `DialogueAuthoringOps.cs`, `EventActionEditorUtility.cs`
- This report

## 1. How to assign an NPC instance per dialogue line

1. Open the scene that contains the NPCs.
2. **Tools → ARPG → Dialogue → Dialogue Editor** → open your asset.
3. On a Dialogue Line: **Speaker = NPC (scene character)**.
4. **Scene Character** → pick `Soldier-Melee 1` (ObjectField).
5. Editor adds/uses `DialogueSpeakerBinding` on that GameObject (stable `speakerId` auto-generated).
6. Another line can pick `Soldier-Melee 2` — different binding, different bubble anchor.
7. **Active Player** lines use Speaker = Active Player (no scene pick).

The Dialogue Asset stores only the stable `speakerId` string (safe on ScriptableObjects). The live Transform comes from `DialogueSpeakerBinding` in the loaded scene.

## 2. NPC default portraits

On the NPC GameObject → `DialogueSpeakerBinding` → **Default Portrait** (Sprite).

Also editable from the Dialogue Editor when that NPC is selected on a line (**NPC Default Portrait**).

**Use Default** on a line reads that sprite (else roster by speaker id).

## 3. Player default portraits

**Use Default** + Active Player → `CharacterPortraitRoster` Neutral for resolved `jyn` / `ax`.

**Custom** → optional per-hero sprites on the line. **Hidden** → no portrait.

Ambient dialogue still never shows portraits.

## 4. Start Dialogue in EventRunner (now)

```
Start Dialogue
- Dialogue Asset
- Wait Until Finished
- Allow Cancel
- Progression Mode
- Auto Advance Hold Sec
- Advanced (legacy / fixtures): Npc Speaker Id, Npc Anchor, Dialogue By Character
```

No Origin / Tied To / Npc Speaker Id in the normal UI. Conversation authorship lives in the Dialogue Editor.

Legacy fields remain serialized for `NpcDialogueHost` and Sprint fixtures.

## 5. Choices / flags / consequences (plain language)

| Concept | UI | Meaning |
|---------|----|---------|
| What the player says | Option text | Label only |
| Where it continues | Continue to | Next line / end |
| Remember this choice | Toggle + Remember as | Writes a WorldState **story fact** |
| Immediate action | Add action… | Door / item / extra SetFlag / etc. |

**Persistence:** `WorldState.SetFlag` runs when the option is confirmed. It does **not** auto-write disk. `SaveManager` snapshots WorldState on save (same as other flags). Cancel before confirm → no remember write. Confirm then cancel later → flag already set in runtime memory until save/load rules apply.

## 6. Existing asset compatibility

- Old lines without `portraitMode` → UseDefault; if they already had `portraitSprite` / variants, resolver treats them as Custom.
- Old `npcAnchor` / `npcSpeakerId` on Start Dialogue / NpcDialogueHost still work as fallback after binding lookup.
- `dialogueByCharacter` still available under Advanced.
- `originKind` / `originSpeakerId` on DialogueAsset remain serialized but are no longer the authoring path.
- TestLab fixtures should keep working via legacy single-anchor path.

## 7. Manual QA (do not claim passed)

1. Place two distinct NPCs in a scene; ensure each can receive `DialogueSpeakerBinding`.
2. New Dialogue Asset: Line1 → NPC Soldier A + default portrait; Line2 → Active Player text; Line3 → NPC Soldier B + custom portrait; Choice with Remember as plain language + one immediate action.
3. EventRunner → Start Dialogue → only assign the asset (+ wait/cancel).
4. Play: bubbles follow the correct soldiers; player lines follow the party member; portraits match Default/Custom/Hidden.
5. Pick a remembered choice; confirm WorldState has the flag; save/load if testing persistence to disk.
6. Open an old TestLab dialogue + Start Dialogue fixture; confirm it still plays (legacy anchor).

Manual QA was **not** executed by the agent.
