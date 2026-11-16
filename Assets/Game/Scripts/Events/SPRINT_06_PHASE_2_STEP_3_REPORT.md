# Sprint 06 — Fase 2, Paso 3: CameraMoveAction

Jugado en TestLab. Cuatro pasadas completas de `Event_S6_Move`. El usuario confirmó que el recorrido se ve bien. Este log no incluye un `Cancel (Inspector)` a mitad del viaje de 2 segundos.

No incluye la secuencia de la iglesia, shake ni diálogo.

## Qué se vio

Cada pasada escribió, en orden:

- `Camera cut 'Shot_S6_MoveStart'.` y `Camera cut settled.`
- `Camera move 'Shot_S6_MoveEnd' duration=2.00s.` y `Camera move settled.`
- `Camera move 'Shot_S6_MoveSnap' duration=0.00s.` y `Camera move settled.`
- `Camera restored.`
- `[EventLog] S6 move restored`
- `Completed`

No hubo warning de blend ni de move. El corte inicial salta al cubo dorado. El viaje de 2 segundos va hacia el cubo verde sin cambiar de slot. La duración 0 salta al cubo blanco. La vuelta al personaje sigue siendo el restore.

Los avisos de Animator, `DontDestroyOnLoad` y `WeaponPerspectiveAlignment` salen al arrancar TestLab. El error de `GameObjectInspector` / `m_Targets` es el Inspector apuntando a un objeto ya destruido. No vienen de este movimiento.

## Qué quedó

`CameraMoveAction` mueve la cámara dirigida que ya está activa. No alterna A/B y no pide un blend al Brain.

Fixed sin LookAt interpola posición, rotación y FOV. Si el plano y el destino tienen LookAt, la posición y el FOV se mueven y la mirada sigue el LookAt del destino. Si uno tiene LookAt y el otro no, el evento se detiene.

Follow solo interpola offset y FOV, con el mismo target y el mismo LookAt. Cambiar de Fixed a Follow, o de target, se hace con Cut o Blend.

La pose de destino se copia al empezar el movimiento. Duración 0 aplica esa pose y cede un frame. SmoothStep es la curva por defecto. Cancelar corta la interpolación, apaga las dos dirigidas y suelta la concesión.

## Archivos

- `Assets/Game/Scripts/Events/Actions/CameraMoveAction.cs`
- `Assets/Game/Scripts/Camera/CameraDirector.cs`
- `Assets/Game/Scripts/Editor/Events/EventRunnerEditor.cs`
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs`

Fixture: `Tools → ARPG → Events → Add Camera Move Fixture (Sprint 06 Phase 2 Step 3)`. Pad violeta `Event_S6_Move` en z≈-168. El menú no borra los pads anteriores.

## Limitaciones

- El pad solo viaja de Fixed a Fixed. El offset de Follow está implementado y no tiene fixture.
- Si el marker se mueve durante los 2 segundos, la cámara no lo persigue.
- Si ambos planos tienen LookAt y no es el mismo objeto, la mirada pasa al destino en el primer frame.
- El fade de obstáculos sigue mirando al jugador.
- El cancel a mitad del viaje de 2 segundos no está en el log de esta sesión.

## Siguiente

Paso 4: una secuencia de iglesia que reúne corte, blend, movimiento y restore. No está hecho.
