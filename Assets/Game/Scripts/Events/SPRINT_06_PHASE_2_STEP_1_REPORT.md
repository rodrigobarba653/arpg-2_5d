# Sprint 06 — Fase 2, Paso 1: Corte a un plano fijo

Cerrado. Jugado en TestLab. Pasó en consola y en pantalla. No incluye Blend, Follow, CameraMoveAction ni los pasos 2, 3 y 4.

## Qué se vio

En `Event_S6_Cut` el cubo magenta aparece de golpe. La vuelta al personaje se desliza. Esa vuelta es el `CameraRestoreAction` de Fase 1, no un blend entre dos planos dirigidos.

Tres pasadas completas escribieron, en orden: `Camera cut settled.`, `Camera restored.`, `[EventLog] S6 shot cut restored` y `Completed`. Sin warning de blend.

`Event_S6_Focus`, el pad teal de Fase 1, siguió mezclando, restauró, escribió `S6 camera restored` y marcó `Completed`.

Una entrada más cortó, llegó a `Camera cut settled.` y se canceló con `Cancel (Inspector)`. No hubo restore, ni `S6 shot cut restored`, ni `Completed`.

## Qué quedó

`CameraShotMarker` define el plano. Este paso solo acepta modo Fixed. Si el marker tiene LookAt, la cámara apunta ahí. Si no tiene, usa la rotación del marker. El fixture `Shot_S6_Cut` no tiene LookAt: su rotación encuadra el cubo magenta.

`CameraShotAction` hace el corte y conserva la concesión. `CameraRestoreAction` apaga las dos dirigidas y espera la vuelta a `VCam_Main`.

`CameraDirector` tiene dos slots, `VCam_Event` y `VCam_Event_B`, bajo el host `CameraDirector`. La que se ve queda en prioridad 10. La otra solo se enciende en prioridad 1 cuando ya hay un plano dirigido, y espera un frame antes del corte. Desde gameplay no se enciende en prioridad 1, porque le ganaría a `VCam_Main`. Al soltar se apagan las dos.

`CameraFocusAction` y `CameraRestoreAction` conservan sus campos. El pad de Fase 1 no se regeneró.

## Archivos

Nuevos:

- `Assets/Game/Scripts/Camera/CameraShotMarker.cs`
- `Assets/Game/Scripts/Events/Actions/CameraShotAction.cs`

Modificados:

- `Assets/Game/Scripts/Camera/CameraDirector.cs`
- `Assets/Game/Scripts/Editor/Events/EventRunnerEditor.cs`
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs`

Fixture: `Tools → ARPG → Events → Add Camera Cut Fixture (Sprint 06 Phase 2 Step 1)`. Pad naranja `Event_S6_Cut` en z≈-132. Cubo magenta `Subject_S6_Cut`. Pose `Shot_S6_Cut`. El menú solo borra esos tres objetos.

## Fuera de este paso

Blend entre dos planos, modo Follow, movimiento dentro del plano, FOV animado y la secuencia de la iglesia. Eso sigue en los pasos 2, 3 y 4.
