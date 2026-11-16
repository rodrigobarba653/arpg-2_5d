# Sprint 06 — Fase 2, Paso 2: Blend, FOV y Follow

Cerrado. Jugado en TestLab. La secuencia completa pasó. El cancel durante el blend al cubo cian se frenó. No incluye CameraMoveAction, la secuencia de la iglesia, shake ni diálogo.

## Qué se vio

En `Event_S6_Blend` la consola escribió, en orden:

- `Camera blend 'Shot_S6_Wide' blend=1.20s.` y `Camera blend settled.`
- `Camera cut 'Shot_S6_Close'.` y `Camera cut settled.`
- `Camera blend 'Shot_S6_Follow' blend=1.50s.` y `Camera blend settled.`
- `Camera restored.`
- `[EventLog] S6 blend sequence restored`
- `Completed`

No hubo warning de duración ni de timeout. El corte al cubo blanco sigue siendo un salto. El blend al cubo cian lo hace el Brain entre los dos slots.

Una cancelación con `Cancel (Inspector)` cayó en la espera después del plano abierto, antes del corte. No hubo Follow, ni restore, ni `Completed`.

Después, el cancel durante `Camera blend 'Shot_S6_Follow'` frenó el plano. La concesión se suelta y el evento no completa.

Los avisos de Animator, `DontDestroyOnLoad` y `WeaponPerspectiveAlignment` salen al arrancar TestLab. No vienen de este plano.

## Qué quedó

`CameraShotAction` acepta Cut o Blend. Cut sigue en duración 0. Blend usa `blendSeconds` y el estilo que ya tiene el Brain. El pad naranja del Paso 1 no se regeneró: su acción sigue en Cut por el valor por defecto.

El slot que no se ve se prepara un frame en prioridad 1 cuando ya hay un plano dirigido. El Brain mezcla posición, rotación y FOV. Desde gameplay el slot no se enciende en prioridad 1, porque le ganaría a `VCam_Main`.

Fixed sin LookAt usa la rotación del marker. Fixed con LookAt apunta a ese objetivo. El close-up del fixture tiene LookAt al cubo blanco y la rotación del marker queda en identidad. Follow usa `CinemachineFollow`. Sin LookAt, mira al target. El offset y el FOV del marker se aplican si están marcados. Si el FOV no está marcado, se usa el de gameplay.

Cancelar durante el blend restaura `DefaultBlend`, apaga las dos dirigidas y suelta la concesión. `VCam_Main` no cambia de Follow, LookAt ni prioridad.

## Archivos

- `Assets/Game/Scripts/Events/Actions/CameraShotAction.cs`
- `Assets/Game/Scripts/Camera/CameraDirector.cs`
- `Assets/Game/Scripts/Camera/CameraShotMarker.cs`
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs`

Fixture: `Tools → ARPG → Events → Add Camera Blend Fixture (Sprint 06 Phase 2 Step 2)`. Pad azul `Event_S6_Blend` en z≈-148. Cubo azul grande, cubo blanco y cubo cian. El menú no borra los pads anteriores.

## Limitaciones

- El cubo cian no se mueve. Se ve el encuadre Follow, no un target en marcha.
- Un Blend con `blendSeconds` en 0 corta.
- El fade de obstáculos sigue mirando al jugador.

## Siguiente

Paso 3: `CameraMoveAction`, movimiento dentro del plano que ya está en pantalla. No está hecho.
