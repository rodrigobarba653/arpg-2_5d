# Sprint 09 — Phase 1: Event Authoring UX + Start Delay

**Cerrada.** QA de delays + cancel OK. Timeline no se implementó. Sprints 05–08 no se reabrieron.

## Archivos

Creados:

- `Assets/Game/Scripts/Events/Actions/DelayedStartAction.cs`
- `Assets/Game/Scripts/Editor/Events/EventActionRegistry.cs`
- `Assets/Game/Scripts/Editor/Events/EventActionEditorUtility.cs`
- `Assets/Game/Scripts/Events/SPRINT_09_PHASE_1_REPORT.md`

Modificados:

- `Assets/Game/Scripts/Events/Actions/ParallelActions.cs` — unwrap de `DelayedStartAction` en validación de cámara/move; rechaza wrappers ilegales.
- `Assets/Game/Scripts/Editor/Events/EventRunnerEditor.cs` — menú categorizado, acciones numeradas, summaries, Ping, warnings; `ParallelActionsDrawer` con reorder + Delay (s) por fila.
- `Assets/Game/Scripts/Editor/Dialogue/DialogueConsequenceListDrawer.cs` — usa el registry (sin acciones EventRunner-only).
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs` — menú fixture S09.

No se tocaron PlayerMotor, ScriptedNpcMotor, CameraDirector, DialogueService, GameplayLock, EventScheduler, cofres ni saves.

## Start Delay — serialización y ejecución

- Tipo: `DelayedStartAction` con `startDelay` (≥0) + un hijo `[SerializeReference] EventAction`.
- Execute: espera el delay (sale si cancel); luego corre el hijo; no arranca el hijo si ya canceló; `Cancel` reenvía al hijo en curso.
- Rechaza hijo null, otro `DelayedStart`, o `ParallelActions`.
- Parallel wait-for-all sin cambios de semántica: el grupo termina cuando todos los hijos (con o sin delay) terminan.
- Conflictos: Parallel inspecciona el **leaf** (`DelayedStartAction.Unwrap`). Un delay **no** permite dos moves al mismo cuerpo ni dos poses de cámara.

Compatibilidad: entradas Parallel antiguas sin wrapper siguen válidas (delay UI = 0). Al poner Delay > 0 el drawer envuelve; al volver a 0, desenvuelve.

## Inspector

- Root: `1. Summary…`, foldout Unity, menú Add por categoría (Camera / Characters / Dialogue / World / Flow / Rewards), botón Ping.
- Parallel: lista reorderable; cada fila `Delay (s)` + summary del leaf + Ping; el autor no arma el wrapper a mano.
- Warnings suaves (HelpBox): refs faltantes, nested Parallel/DelayedStart, conflictos move/cámara. No bloquean editar.
- Consequences de diálogo: mismo catálogo filtrado (`EventRunnerOnly` fuera).

## Fixture QA

`Tools → ARPG → Events → Add Staggered Delay Fixture (Sprint 09 Phase 1)`

Salir de Play, correr el menú, guardar. Pad `Event_S9_Delay` en `(0,1,-320)` (aislado de S05–S08). Interactuar.

Parallel:

| Delay | Acción |
|------:|--------|
| 0.0s | CameraShot `Shot_S9_Wide` |
| 0.5s | Move `NPC_S9_Walker` → `Marker_S9_NpcWalk` |
| 1.5s | Move ActivePlayer → `Marker_S9_PlayerWalk` |
| 2.0s | Play `Idle` en `NPC_S9_IdleActor` (no se mueve; sin conflicto) |

Luego CameraRestore + log `S9 staggered delay restored`.

## QA manual

1. Secuencia completa: delays 0 / 0.5 / 1.5 / 2.0, parallel settle, restore, `S9 staggered delay restored`, `Completed`. **OK**
2. Cancel con delays aún pendientes (player@1.5 / Idle@2.0 sin arrancar; NPC ya en move): sin `Completed`, locks liberados. **OK**
3. Cancel con moves/anim ya en curso: sin `Completed`, locks liberados. **OK**
4. Inspector Delay / reorder / Ping / warnings: a criterio del authoring en editor (no bloqueante para cerrar runtime).

## Limitaciones

- `DelayedStart` no está en el menú Add (solo vía Delay del Parallel, o fixture/código).
- Delay en la lista root secuencial es posible si se serializa a mano; el sugar UX es del Parallel.
- Warnings de editor no cubren todos los campos de diálogo/puertas.
- No hay Timeline / PlayTimelineAction / DialogueCheckpoint.
