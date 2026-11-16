# Sprint 06 — Fase 2: cierre

La fase queda armada en un solo pad nuevo. Los pasos 1, 2 y 3 no se reabrieron. Este archivo no sustituye sus reportes de QA.

Jugado en TestLab. El usuario confirmó que se ve bien. Una pasada completa y una cancelada a mitad del viaje a la puerta. No hubo warning de blend ni de move.

No incluye shake ni diálogo. Fase 3 y Fase 4 no están hechas.

## Qué se vio

La pasada completa escribió, en orden:

- `Camera blend 'Shot_S6_SeqWide' blend=1.20s.` y `Camera blend settled.`
- `Camera move 'Shot_S6_SeqEntrance' duration=2.00s.` y `Camera move settled.`
- `Camera cut 'Shot_S6_SeqArtifact'.` y `Camera cut settled.`
- `Camera blend 'Shot_S6_SeqFollow' blend=1.20s.` y `Camera blend settled.`
- `Camera move 'Shot_S6_SeqFollowClose' duration=1.25s.` y `Camera move settled.`
- `Camera restored.`
- `[EventLog] S6 sequence restored`
- `Completed`

La segunda pasada llegó a `Camera move 'Shot_S6_SeqEntrance'` y ahí salió `Cancel (Inspector)`. No hubo `Camera move settled.`, ni restore, ni `Completed`.

## Fixture

Menú: `Tools → ARPG → Events → Add Camera Sequence Fixture (Sprint 06 Phase 2 Final)`.

Pad terracota `Event_S6_Sequence` en z≈-198, `eventId` `lab_s6_sequence`. Doce acciones, `seizesPlayerControl`, Repeat, `debugLog`. El menú solo destruye los objetos de esta secuencia. Hay que guardar la escena después de crearlo.

Geometría, sin arte nuevo:

- Iglesia de piedra `Church_S6` y torre `ChurchSteeple_S6`.
- Puerta negra `ChurchEntrance_S6`.
- Reliquia dorada `Subject_S6_Artifact`, delante de la puerta.
- Figura rosa `Subject_S6_Jyn`. El jugador real nace en Play Mode y no es una referencia de escena. Esta figura es el objetivo del Follow.

Orden:

1. Blend 1.20 s desde gameplay al plano ancho Fixed, FOV 64.
2. Espera 1 s.
3. Move 2.00 s SmoothStep hacia la puerta, FOV 36. Misma cámara, sin blend.
4. Espera 0.60 s.
5. Cut al primer plano de la reliquia, FOV 18.
6. Espera 0.60 s.
7. Blend 1.20 s al Follow de la figura rosa, offset `(0, 3.2, 6.5)`, FOV 46.
8. Espera 0.60 s.
9. Move 1.25 s del offset a `(0, 1.7, 3)` y FOV 30. Mismo target, sin LookAt propio.
10. Espera 0.40 s.
11. Restore 1 s.
12. `[EventLog] S6 sequence restored` y `Completed`.

El offset positivo en Z deja la cámara del lado del pad, con la iglesia detrás de la figura.

## Cancelación

No hubo cambio de código. El camino que ya existe cubre los tres casos:

- Durante un Move, `CameraMoveAction.Cancel` invalida la interpolación y llama `Abort`. `Abort` devuelve `DefaultBlend` si este runner lo cambió, apaga los dos slots y suelta la concesión. `StopCoroutine` no llega a `MarkCompletedSuccessfully`.
- Durante un Wait, la acción no tiene cancel propio. `CleanupAfterRun` llama `NotifyRunnerFinished`, que hace el mismo `Abort`.
- Durante un Blend, `CameraShotAction.Cancel` llama `Abort` antes de cortar la corrutina.

En los tres, el evento no escribe `Completed`.

## Archivos

- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs` — menú prioridad 224 y el pad.
- `Assets/Game/Scripts/Events/SPRINT_06_PHASE_2_FINAL_REPORT.md` — este reporte.

`CameraDirector`, las acciones de cámara, `EventRunner`, `VCam_Main`, `CameraAutoTarget`, `GameplayLock`, el shake de combate y el diálogo no se modificaron.

## Decisiones

- Cut, Blend y Move siguen siendo tres operaciones. El viaje a la puerta y el acercamiento del Follow no cambian de slot.
- Los dos Follow comparten target y no tienen LookAt, que es la condición para interpolar solo offset y FOV.
- La figura rosa sustituye a Jyn. Enganchar al jugador activo pediría un asignador en runtime que esta fase no agrega.

## Limitaciones

- La figura no camina. El Follow demuestra encuadre y cambio de offset, no persecución.
- El Move copia el destino al empezar. Si alguien mueve el marker durante el viaje, la cámara no lo sigue.
- El fade de obstáculos sigue mirando al jugador mientras el plano dirigido está activo.
- El restore de cleanup, si se cancela, no espera el blend de vuelta. El `CameraRestoreAction` del final sí espera. El cancel de esta sesión cortó durante el viaje a la puerta, antes de ese restore.

## QA

Pasado en esta sesión. El usuario dijo que se ve bien. El log cubre la secuencia completa y el cancel durante el viaje de 2 segundos.

1. Secuencia completa. En consola, en orden: blend `Shot_S6_SeqWide` 1.20 s, move `Shot_S6_SeqEntrance` 2.00 s, cut `Shot_S6_SeqArtifact`, blend `Shot_S6_SeqFollow` 1.20 s, move `Shot_S6_SeqFollowClose` 1.25 s, `Camera restored.`, `[EventLog] S6 sequence restored`, `Completed`.
2. El plano ancho entra deslizando. El primer plano de la reliquia aparece de golpe. El paso a la figura rosa vuelve a deslizar, posición, orientación y FOV.
3. El viaje a la puerta dura unos dos segundos y no teletransporta la cámara. El acercamiento a la figura acerca el encuadre sin cambiar de sujeto.
4. Al terminar, la cámara vuelve a seguir al personaje que se controla.
5. Otra pasada: pulsar Cancel en el Inspector de `Event_S6_Sequence` mientras dice `Camera move 'Shot_S6_SeqEntrance'`. Debe frenar, volver al gameplay y no escribir `Completed`.
6. En la pasada completa, después de `Camera blend 'Shot_S6_SeqFollow'`, el encuadre se acerca durante `Camera move 'Shot_S6_SeqFollowClose' duration=1.25s`.
