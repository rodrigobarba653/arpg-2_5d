# TOM — Sprint 07 Phase 0: Legacy Code Quality Audit

Audit only. No code, assets, prefabs, or scenes were changed. Sprint 06 stays closed. No loot table. No Sprint 08.

Sprint 07 can grant chest rewards through the event system that already exists. The inventory, the wallet, and the save snapshot are sound enough to keep. The popup spawner is the one piece that has to change, because a directed shot and several rewards at once pile every notice on the same world point.

## A. Code quality

| Component | Verdict | Technical justification | Dependencies | Risk |
|---|---|---|---|---|
| `PlayerInventory` | REFACTOR | Party-wide static list and the add/remove/use API are the right model. Identity is the asset reference, while save stores `item.id`. A full stack drops the overflow and returns nothing. A non-stackable item added twice becomes a second row that `GetCount` and `Remove` never see. | `SaveManager`, `ItemsPanel`, `PlayerEquipment`, `PlayerMagic`, `SimpleDoor`, `EnemyHealth` | Do not rewrite it this sprint. Chest quantities sit under `maxStack` (99). A grant reads the count before and after. |
| `ItemDefinition` and subclasses | KEEP | Data-only assets. Use behavior stays on the item (`PotionItem.Use`). `id` is a designer string. | `ItemDatabase`, inventory, equipment | Empty or duplicate ids. The database keeps the first id and save skips an empty one. |
| `ItemDatabase` | KEEP | One Resources asset, lookup by id, editor validation for empty and duplicate ids. | `Assets/Resources/ItemDatabase.asset`, load path | A stale cache after an id edit in Play Mode. Not a chest blocker. |
| `ItemPickup` | REFACTOR | Floor loot works. Overlap mode can grant once per collider before `Destroy`. It never calls the notifier. Overlap mode ignores `GameplayLock`; the interact mode does not, because `InteractInput` checks the Interact channel. | `PickupRegistry`, `PlayerInventory` | Leave it. The chest does not use it. Same-scene load does not re-run `Awake`, so an already-active pickup stays visible. |
| `PickupRegistry` | KEEP | Correct consumed-id set for objects that disappear. Saved beside inventory in one snapshot. | `ItemPickup`, `MagicPickup`, `SaveData.consumedPickupIds` | No restore event, and an empty id is never remembered. Wrong store for a chest that must remain visible and open. |
| `ItemPickupNotifier` | REFACTOR | `Show` is a one-call API and enemy drops already use it. Every popup is spawned on the player at the same offset. TestLab has no instance of this component, so `Show` fails silently. | `ItemPickupPopup`, `EnemyHealth.AwardItemDrops` | A directed shot frames the chest while the player stands outside it. The notices spawn off-screen, on top of each other. |
| `ItemPickupPopup` | REFACTOR | The prefab, icon, label, and fade are usable. Motion is world-space and billboarded, so several notices occupy one point and a camera cut leaves them out of frame. | Notifier, `Assets/Game/UI/ItemPickupPopup.prefab` | Changing the default path would move enemy-drop popups. Keep that path. Add a stacked screen-space path for event grants. |
| `SaveManager` / `SaveData` | KEEP | One capture writes Varium, items, equipment, magic, `PickupRegistry`, and `WorldState`. Load restores the registries before the scene wakes, then reloads inventory by id. | All of the above | No autosave. A chest flag and the granted items are consistent if both are in memory before the player saves. Missing database entries are skipped on load. |
| `PlayerWallet` | KEEP | Static party total, `AddVarium` / `SpendVarium` / `SetTotal`. Save uses `PlayerWallet.Total`. | `EnemyHealth.AwardVarium`, `SaveManager` | `AddVarium` with a negative amount spends down to zero. The reward action only sends a positive amount. |

Nothing here is unused or small enough to delete. `SaveData.equippedWeaponId` stays as the old-save fallback.

`EventRunner` does not roll back a finished action. `StopCoroutine` does not rewind `PlayerInventory.Add`. `SetFlagAction` is synchronous and already tolerates `EventContext(null)`, which is how dialogue consequences run.

## B. Architecture

### Chest

One object, four existing pieces plus a small view:

- `ChestView` holds `persistentId`, a closed object, and an open object. On enable and on `WorldState.OnRestored` it shows the open object when that flag is true. This covers a same-scene TestLab load, where `Awake` does not run again.
- `WorldInteractable` starts the runner. A `FlagIsFalse` condition on `persistentId` makes a claimed chest unavailable.
- `EventRunner` with `seizesPlayerControl` so interact and the pause menu stay locked for the whole sequence.
- Rewards are not stored on the view. They live on the grant action below, so a quest or a dialogue choice can use the same action.

### Rewards

One new `GrantRewardsAction`, not two. It carries a list of `ItemDefinition` plus quantity, and an optional Varium amount. In one pass, with no `yield`, it adds each item, adds Varium only when the amount is greater than zero, and raises one notice per line that actually landed. It never reads `ctx.Runner` except for an optional debug log. It does not set the opened flag.

The following action is the existing `SetFlagAction`. Both actions end with `yield break`, so the runner finishes them in the same pass before a cancel can land between them. A later randomized action can call the same grant method. This sprint does not add a loot table.

### Persistence

The claim is the WorldState flag, not `PickupRegistry` and not `EventRunner` completion. Completion is written only when the whole sequence finishes, so a cancel during the return blend would leave the items in hand and the chest claimable again.

### Notices

`ItemPickupNotifier.Show` stays the world-space player popup used by enemy drops. Event grants call a second entry that stacks rows on a screen-space canvas at sorting order 50 (`DialoguePresentationPriority.Notification`), under dialogue at 100. Each row reuses `ItemPickupPopup` for the icon and the “Obtained” label. If that canvas is missing, the call falls back to the world popup and logs once.

### Sequence

Optional shot or dialogue, then `GrantRewardsAction`, then `SetFlagAction`, then optional restore. The runner keeps the camera lease until restore or cleanup.

### Cancel

- Before the grant action: nothing is added, the flag stays false, the chest still looks closed and can be opened again.
- After the grant and the flag: items and Varium stay, the chest stays open, and a second interact does nothing. There is no rollback.
- An empty `persistentId` does not start. `SetFlag` already ignores an empty id, which would otherwise leave a chest that pays forever.

### Save

The player can save only after the lock is released. That snapshot contains the new items, the new Varium total, and the flag together.

## C. Scope

**Reuse unchanged.** `PlayerInventory.cs`, `ItemDefinition.cs` and its subclasses, `ItemDatabase.cs`, `ItemPickup.cs`, `PickupRegistry.cs`, `PlayerWallet.cs`, `SaveManager.cs`, `SaveData.cs`, `SetFlagAction.cs`, `WorldInteractable.cs`, `InteractionDetector.cs`, `EventRunner.cs`, `CameraDirector.cs`, `StartDialogueAction.cs`, and `EnemyHealth.AwardItemDrops`.

**Refactor.** `ItemPickupNotifier.cs` and `ItemPickupPopup.cs`: add the stacked screen-space path. Leave `Show` on the player for enemy drops.

**Create.** `Assets/Game/Scripts/Events/Actions/GrantRewardsAction.cs`. `Assets/Game/Scripts/LevelDesign/Chest/ChestView.cs`. One TestLab menu entry in `EventSystemTestLabBuilder.cs` that only creates the new chest pad.

**Remove.** Nothing.

## D. TestLab QA

One pad past the yellow composition pad. Closed chest, `persistentId` set, `seizesPlayerControl`. Sequence: one-second blend, grant Potion x3 and Varium x200, set the flag, two-second wait, restore.

1. Open it. Inventory gains 3 of that potion, Varium gains 200, three-plus-one notices appear on screen, the mesh switches to open, and the event reaches Completed.
2. Interact again. No second grant.
3. Save and load in the same scene. The chest stays open and the counts stay put.
4. Cancel during the blend. No items, no Varium, chest still closed, no Completed. Open it again and it pays once.
5. Cancel during the wait after the flag. The rewards remain, the chest stays open, and a later interact does not pay again.

No second fixture and no randomized table. Sprint 08 does not start.
