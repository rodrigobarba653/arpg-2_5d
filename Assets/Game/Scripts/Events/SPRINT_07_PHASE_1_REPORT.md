# Sprint 07 — Fase 1: cofres, premios y avisos

**Cerrada.** Sprint 07 cerrado. Sprint 06 no se reabrió. No hay tabla de botín ni Sprint 08.

## Qué hace

`GrantRewardsAction` entrega ítems y Varium. No escribe el flag del cofre. Sirve con `EventContext` sin runner, así que una consecuencia de diálogo puede usarla.

`ClaimChestAction` es el camino del cofre. En la misma pasada, antes de ceder el frame, revisa `persistentId`, no vuelve a pagar si el flag ya está, entrega con `RewardDelivery` y entonces escribe el flag. Un cancel posterior no separa los ítems del reclamo.

`ChestView` solo cambia el objeto cerrado y el abierto. La interacción sigue en `WorldInteractable`. Al cargar, `WorldState.OnRestored` vuelve a aplicar el estado, también en un load de la misma escena.

Los drops de enemigos siguen llamando `ItemPickupNotifier.Show` en el mundo. Los premios de evento llaman `ShowEventReward`, en pantalla, orden 50, una fila por premio recibido. Si el prefab no puede pasar a pantalla, cae al popup de mundo.

## Menú

`Tools → ARPG → Events → Add Chest Fixture (Sprint 07 Phase 1)`

Salir de Play Mode, correr el menú y guardar la escena. No borra los pads anteriores.

`Chest_S7` queda en `(8, 0.75, -308)`, a la derecha del pasillo, después del pad amarillo. `persistentId` `lab_s7_chest`. Interacción con el detector, no con un trigger de entrada.

Orden:

1. Blend de 1 s a `Shot_S7_Chest`.
2. Reclamo: Potion x3 y Varium x200, y el flag en ese mismo paso.
3. Espera de 2 s.
4. Restore de 1 s.
5. `[EventLog] S7 chest restored` y `Completed`.

## Deuda que no se tocó

`PlayerInventory.Add` identifica el asset, y el save guarda `item.id`. Un stack lleno tira el sobrante sin avisar. Una segunda fila de un ítem no apilable no entra en `GetCount`. El aviso usa ese delta, así que el sobrante no se anuncia.

## QA

Pasó en Play Mode:

1. Reclamo completo: blend, `Chest 'lab_s7_chest' claimed.`, restore, `S7 chest restored`, `Completed`. Premios y avisos en pantalla.
2. Tras el reclamo, un interact posterior no vuelve a pagar.
3. Save (slot 0) y load en la misma escena: `flags=1`, cofre abierto, cantidades intactas, sin segundo pago.
4. Cancel del Inspector durante el blend, antes de `claimed`. Sin premios, sin `Completed`. Un segundo intento paga una vez.
5. Cancel del Inspector después de `claimed`, durante la espera. Los premios se quedan, el cofre sigue abierto, sin segundo pago.

Salir y volver a entrar a Play Mode borra `WorldState` vía el bootstrap de prueba. Eso no es un fallo del cofre.

## Archivos

Creados:

- `Assets/Game/Scripts/Events/RewardDelivery.cs`
- `Assets/Game/Scripts/Events/Actions/GrantRewardsAction.cs`
- `Assets/Game/Scripts/Events/Actions/ClaimChestAction.cs`
- `Assets/Game/Scripts/LevelDesign/Chest/ChestView.cs`

Modificados:

- `Assets/Game/Scripts/UI/Popups/ItemPickupNotifier.cs`
- `Assets/Game/Scripts/UI/Popups/ItemPickupPopup.cs`
- `Assets/Game/Scripts/Editor/Events/EventRunnerEditor.cs`
- `Assets/Game/Scripts/Editor/Dialogue/DialogueConsequenceListDrawer.cs`
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs`

No se tocaron inventario, wallet, save, drops, equipo, puertas, `DialogueService`, `CameraDirector` ni `PlayerMotor`.
