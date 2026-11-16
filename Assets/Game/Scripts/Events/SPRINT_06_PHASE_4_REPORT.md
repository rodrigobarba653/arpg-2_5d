# Sprint 06 — Fase 4: composición y ParallelActions

**Cerrada.** Sprint 06 cerrado. Fases 1, 2 y 3 no se reabrieron. No hay movimiento de NPC ni animación narrativa.

## Qué hace

`ParallelActions` es una acción más de la lista. El runner sigue yendo de una en una. El grupo arranca a sus hijos con corrutinas del mismo `EventRunner`, comparte el `EventContext` y espera a que todos terminen.

Dentro del grupo solo puede haber una acción que escriba la pose: Focus, Shot, Move o Restore. Si hay dos, no arranca a nadie y hace `RequestStop`. Un grupo dentro de otro también se rechaza. `CameraShakeAction`, la espera, el log, los flags, las puertas y el diálogo pueden ir junto al viaje.

`Cancel` llama `Cancel()` solo en los hijos que siguen vivos y después corta sus corrutinas. Si un hijo hace `RequestStop`, el grupo cancela al resto. El runner sigue soltando la concesión, el lock y el scheduler en `CleanupAfterRun`. No marca `Completed`.

La secuencia de plano, diálogo y restore no cambió. El diálogo no toca la cámara. La concesión sigue hasta el restore o hasta el cleanup.

## Fixture

Menú: `Tools → ARPG → Events → Add Camera Composition Fixture (Sprint 06 Phase 4)`.

Pad amarillo `Event_S6_Composition` en z≈-260, más allá del pad verde del shake. `eventId` `lab_s6_composition`. El menú no borra los pads anteriores. Hay que salir de Play Mode, correr el menú y guardar la escena.

Geometría: muro gris `Subject_S6_CompWide` y figura violeta `Subject_S6_CompSpeaker`. El diálogo es `Lab_S6_Composition`, dos líneas del personaje activo, `lockGameplay` activado.

Orden:

1. Blend de 1 s a `Shot_S6_CompWide`.
2. Grupo paralelo: viaje de 2 s a `Shot_S6_CompMove`, espera de 1 s, y `S6 overlap`.
3. Cut a `Shot_S6_CompSpeaker`.
4. Diálogo. Hay que avanzar las dos líneas.
5. Restore de 1 s.
6. `[EventLog] S6 composition restored` y `Completed`.

`S6 overlap` sale al arrancar el grupo, antes de `Camera move settled`.

## Archivos

- `Assets/Game/Scripts/Events/Actions/ParallelActions.cs`
- `Assets/Game/Scripts/Editor/Events/EventRunnerEditor.cs`
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs`
- `Assets/Game/Scripts/Dialogue/Runtime/DialogueLayoutSolver.cs` — deja de rehacer el plan en cada frame mientras el hablante sigue fuera de cuadro.
- `Assets/Game/Dialogue/TestLab/Lab_S6_Composition.asset` — lo crea el menú.

`EventRunner`, `CameraDirector`, `StartDialogueAction`, `DialogueService` y las acciones de cámara no se modificaron.

## Limitaciones

- El diálogo del fixture va después del corte, no durante el viaje. Meterlo dentro del grupo lo haría coincidir con el movimiento.
- `OpenDoorAction` puede ir en el grupo. Si se cancela a mitad, la puerta sigue moviéndose. Este pad no crea una puerta.
- Un `SetFlag` dentro del grupo queda escrito aunque después se cancele.
- Caminar de un NPC y la animación de sorpresa quedan para Sprint 08. Entran como acciones del mismo grupo si no escriben la pose.

## QA

Pasó en el log de Play Mode:

1. Secuencia completa: blend a `Shot_S6_CompWide`, `Parallel group started (3).`, viaje de 2 s, `[EventLog] S6 overlap` antes de `Camera move settled.`, `Parallel group settled.`, cut a `Shot_S6_CompSpeaker`, diálogo `lab_s6_composition` completado, `Camera restored.`, `S6 composition restored`, `Completed`.
2. Segunda pasada: Cancel del Inspector durante el viaje, después de `S6 overlap` y antes de `Camera move settled`. No hay restore ni `Completed`.
3. Tercera pasada: Cancel del Inspector con el diálogo abierto. `[Dialogue] Session cancelled (event-cancel) asset='lab_s6_composition'.` No hay `Completed`.

El primer log llenó la consola con cientos de `[Dialogue] Conversation layout plan built for 1 speaker(s)`. `PlayLine` pide el layout en cada frame. El plan se rehacía siempre porque el personaje activo queda fuera del plano dirigido, y “fuera de cuadro” contaba como motivo nuevo en cada muestra. El hueco del slot no cambia. Ahora solo se rehace al entrar o salir del área segura, o si dos hablantes cruzan el orden.

La repetición, después de recompilar `DialogueLayoutSolver` (20:53), fue una sola pasada del pad amarillo. El diálogo siguió el cierre normal: un plan al arrancar la sesión (`DialogueSession.Run`), `Interactive start 'lab_s6_composition'`, `Session completed`, `Dialogue outcome=Completed`, restore y `[Event:Event_S6_Composition] Completed.` No hubo un segundo plan durante las líneas. No apareció `Fail` ni `Session cancelled`.
