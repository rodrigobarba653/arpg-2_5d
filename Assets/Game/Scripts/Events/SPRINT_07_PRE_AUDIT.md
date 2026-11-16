# TOM — Technical Systems Status & Sprint 07 Pre-Audit

Audit only. No code, assets, prefabs, or scenes were changed.

Sprint 07 does not need a new inventory, save, or interaction stack. Those already exist and are wired into the event system. What is missing is a chest, a way for an event to grant an item, and a reusable loot table.

## Table A — Overall systems status

| System | Existing implementation | Integration status | Changed during Sprints 00–06 | Notes |
|---|---|---|---|---|
| EventRunner / EventContext | `EventRunner`, `EventContext`, `EventScheduler`, `EventAction`, trigger and interact sources | Functional | Introduced in `a3aed938` (Sprints 01–04 in one commit). Sprint 06 only added `CameraDirector.NotifyRunnerFinished` on cleanup | One sequence coroutine. One player-control runner at a time. `Completed` only on a full run. Cancel does not mark Once |
| WorldState / flags / conditions | `WorldState` plus `SetFlagAction`, `ClearFlagAction`, `MarkEventCompletedAction`, flag and event conditions | Functional | Introduced with the event system (Sprint 02 comments). Sprint 05 reuses the same conditions in dialogue | Bool flags and completed event ids. Missing flag reads false. Setting a flag does not save to disk |
| GameplayLock | `GameplayLock` and `GameplayLockChannels` | Functional | Introduced with events. Sprint 05 dialogue acquires `PlayerControl` when the asset says so | Ref-counted. Does not change `timeScale`. Interact, menu, magic, and party swap are included in `PlayerControl` |
| Interaction / prompts | `InteractionDetector`, `IInteractable`, `WorldInteractable`, `PlayerInteractionPrompt` | Functional for events and NPC dialogue. Legacy prompts still live on doors, switches, and pickups | Central detector arrived in `a3aed938` (Sprint 04 comment on `NpcDialogueHost`) | `EventSourceInteract` still polls on its own. `EventSourceInteractAdapter` is the opt-in bridge. Gamepad South still also jumps |
| Doors / switches | `SimpleDoor`, `DoorSwitch`, `PressurePlateDoor`, open/close/unlock/set-state actions | Functional, two paths | Door motion predates events (`415f3326`). Event actions and WorldState persistence were added on top, not as a replacement | Direct calls and `EventRunner` both work. `OpenDoorAction` has no `Cancel`. A cancelled event leaves the door moving |
| Dialogue | `DialogueService`, `DialogueSession`, `DialogueAsset`, `StartDialogueAction`, `NpcDialogueHost` | Functional | Built in Sprint 05 (`1984f11a`, `1c55904c`). Sprint 06 only changed off-screen layout rebuild | Consequences are ordinary `EventAction`s. They run with `EventContext(null)`, so a camera action inside a choice stops the sequence |
| Camera Director | `CameraDirector`, `CameraShotMarker`, focus/shot/move/restore/shake actions | Functional | New in Sprint 06 (`ab11b15d`). Gameplay follow, look-at, and Perlin were left alone | One lease, owned by the runner. Two directed slots. Shake uses Impulse and does not take the lease |
| ParallelActions | `ParallelActions` | Functional | New in Sprint 06 | Waits for every child. One pose writer per group. No nested groups. Does not take or release the camera lease |
| Save / persistence | `SaveManager`, `SaveData`, `SaveSystem` | Functional | Predates the named sprints. Later extended to store flags, completed events, and consumed pickups. Sprint 00 is not a labeled commit | Captures scene, party stats, Varium, inventory, equipment, magic, `PickupRegistry`, and `WorldState`. New Game clears them. No autosave on a flag write |

## Table B — Sprint 07 legacy audit

| System | Existing components | Functional status | Recommendation | Risks |
|---|---|---|---|---|
| Chests / containers | None. `ItemPickup.pickupId` only mentions a chest as an id example | Missing | Build a chest as an interactable that starts an `EventRunner`. Do not invent a second container model | A new chest script that grants items itself would bypass events, flags, and the camera |
| Items | `ItemDefinition` and `KeyItem`, `WeaponItem`, `WeaponPart`, `CraftItem`, `PotionItem`, `EtherItem`, `PhoenixDownItem`, `SpellItem`. Assets under `Assets/Game/Prefabs/Items/`. Registry: `Assets/Resources/ItemDatabase.asset` | Functional | Reuse as-is | Items are identified by `ItemDefinition.id`. Empty or duplicate ids drop out of the save |
| Inventory | `PlayerInventory` | Functional | Reuse `Add`, `Remove`, `HasItem`, `UseItem`, `HasKey` | The list is static and shared by the whole party. Rewriting it would break equipment, doors, and the menu |
| Acquisition / rewards | `PlayerInventory.Add`. Enemy death calls it from `EnemyHealth.AwardItemDrops`. Varium is `PlayerWallet`, not an item | Partial | Extend with one `EventAction` that calls `Add`. Leave enemy drops and the wallet alone | A reward path that does not go through `PlayerInventory.Add` will not save |
| Loot tables | `EnemyHealth.ItemDropEntry`: per-enemy item, chance, quantity. No shared loot asset | Partial, combat only | Leave it. Add a loot asset only if a chest must roll | Folding enemy drops into a new table would change combat owned by the other work |
| Pickups | `ItemPickup`, `MagicPickup`, prefabs `Assets/Game/Prefabs/Items/ItemPickUp.prefab` and the weapon-part pickup | Functional | Reuse for floor loot. A chest that also plays a scene should be an event, not another pickup | `ItemPickup` keeps its own prompt and does not call `ItemPickupNotifier`. It also ignores `GameplayLock` |
| Equipment | `PlayerEquipment`, `EquipmentPanel`, `WeaponSlotsPanel`, `PartSelectorPanel` | Functional | Leave untouched | Equipment is per character. Inventory is shared. Auto-equip on the first weapon is already implemented |
| Acquisition UI | `ItemPickupNotifier`, `ItemPickupPopup`, prefab `Assets/Game/UI/ItemPickupPopup.prefab`. Menu: `ItemsPanel`, `ItemSlotWidget`, `ItemTargetSelector` | Popup works for enemy drops. World pickups do not show it. Menu use works | Reuse the notifier. Call it from the new grant action | Dialogue reserves priority 50 for notifications (`DialoguePresentationPriority.Notification`). The current popup is world-space, not that band |
| Persistent ownership / opened state | Inventory rows in `SaveData.items`. Collected world objects in `PickupRegistry` → `SaveData.consumedPickupIds`. Doors and switches use `WorldState` flags | Functional for pickups and doors. No opened-chest record | Reuse `PickupRegistry` for a one-shot world object, or a `WorldState` flag if the chest must stay visible and open | A third “opened chests” list would diverge from the two stores `SaveManager` already writes |
| Event / WorldState / save integration | Save already snapshots both registries. Dialogue consequences can already `SetFlag`. No action grants an item | Save integration exists. Event grant does not | Extend the event action list. Do not change `SaveData` unless a new field is actually required | Consequences run without a runner, so a grant action must not require `ctx.Runner` |

Inventory, equipment, and pickups entered in `cefd3e73` (“Nueva feature”). Doors entered earlier, in `415f3326`. The event system, WorldState, and the central interactor entered together in `a3aed938`. Sprints 01–04 are not separate commits, so their internal history is only verified where the code comments name the sprint. There is no chest, container, or loot-table class anywhere under `Assets`.

## Sprint 07 scope

**Reuse as-is.** `PlayerInventory`, `ItemDefinition` and its subclasses, `ItemDatabase`, `PickupRegistry`, `SaveManager` / `SaveData`, `PlayerEquipment` and the equipment menu, `ItemsPanel`, `PlayerWallet`, `EnemyHealth` drops, `ItemPickup` and `MagicPickup` for floor loot, `WorldState`, `GameplayLock`, `EventScheduler`, doors, dialogue, and the camera director.

**Extend.** Add one event action that grants an existing `ItemDefinition` through `PlayerInventory.Add`, marks persistence with `PickupRegistry` or `SetFlag`, and calls `ItemPickupNotifier.Show`. Put the chest on `WorldInteractable` so the existing detector starts the runner. A directed shot, a line of dialogue, or a parallel group can then sit in that same sequence. `OpenDoorAction` can consume a key only if a chest requirement needs it. Today only `SimpleDoor.TryOpenWithKey` does that.

**Build new.** A chest interactable and, only if contents are rolled rather than authored, one loot asset. There is no chest to adapt. `ItemDropEntry` is not that asset.

**Leave untouched.** `PlayerMotor`, gameplay camera follow and Perlin, `CameraDirector`, `ParallelActions`, dialogue services, door motion, equipment slots, the magic wheel, and the uncommitted `Valcyr_B1_Final.unity` door serialization. That scene change is not part of this sprint.

The other developer’s surface is `Assets/Game/Scripts/Items/`, `Player/PlayerEquipment.cs`, `Player/PlayerWallet.cs`, `Save/`, `UI/Menu/`, `UI/Popups/`, and `LevelDesign/Door/`. Sprint 07 should call those APIs from a new `EventAction` and a chest component. It should not replace them.

No implementation until this scope is approved.
