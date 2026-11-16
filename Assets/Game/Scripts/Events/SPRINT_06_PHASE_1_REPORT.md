# Sprint 06 — Fase 1: Switch y restore

Cerrada. A y B jugadas en TestLab. Las dos pasaron. Fase 2, 3 y 4 no están hechas.

## Qué quedó

Una cámara dirigida, distinta de `VCam_Main`. El evento la enciende, espera el blend y la apaga. `VCam_Main` sigue al jugador aunque no se vea. `CameraAutoTarget`, el prefab, el Perlin de combate, `GameplayLock`, el diálogo y las puertas no se modificaron.

Acciones:

- `CameraFocusAction`. Objetivo y `blendSeconds` (default 1). Al terminar, la concesión sigue tomada.
- `CameraRestoreAction`. `blendSeconds` (default 1). Si no hay concesión, termina en el acto. Si la hay, espera la vuelta a `VCam_Main` y suelta.

Cancelar la acción en curso suelta la concesión sin esperar el blend. Si el foco ya terminó y la acción actual es un `Wait`, `EventRunner.CleanupAfterRun` suelta igual. Esa suelta de seguridad tampoco espera la vuelta.

## Archivos

Nuevos:

- `Assets/Game/Scripts/Camera/CameraDirector.cs`
- `Assets/Game/Scripts/Events/Actions/CameraFocusAction.cs`
- `Assets/Game/Scripts/Events/Actions/CameraRestoreAction.cs`

Modificados:

- `Assets/Game/Scripts/Events/EventRunner.cs`
- `Assets/Game/Scripts/Editor/Events/EventRunnerEditor.cs`
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs`

Fixture: `Tools → ARPG → Events → Add Camera Switch Fixture (Sprint 06 Phase 1)`. Pad `Event_S6_Focus` en `(0, 1, -120)`. El cubo amarillo `Marker_S6_Focus` en `(6, 1.5, -120)` es solo el objetivo de ese pad. No dispara nada. Si se borra, el Focus de ese pad se queda sin target.

## Decisiones

`VCam_Event` tiene prioridad 10, el mismo canal y el mismo lente que `VCam_Main`. El offset sale del `CinemachineFollow` de gameplay. Si no hay, usa `(0, 4.5, -9.3)`. Aim: `CinemachineHardLookAt`. Queda apagada cuando nadie la usa.

Cinemachine 3 no elige una virtual camera cuyo padre directo ya es otra. El host vacío `CameraDirector` es hijo de `VCam_Main`, y `VCam_Event` cuelga de ese host. Así persiste con el padre y entra al pool del Brain. No usa `PersistentCamera`.

Una concesión. El dueño es el `EventRunner`. Otro runner recibe `RequestStop` y no roba el plano. Sin runner, el evento se detiene. El mismo runner puede cambiar de objetivo.

Mientras la concesión existe, `StandbyUpdate` de `VCam_Main` pasa a Always. Al soltar, vuelve al valor anterior.

`DefaultBlend` no se reemplaza como asset. Se copia, se cambia el tiempo de esa transición y, con duración 0, el estilo de esa transición pasa a Cut. El Brain crea la mezcla en LateUpdate y se queda con la duración copiada. La acción espera un frame y solo entonces devuelve el valor original, cuando la mezcla ya arrancó o el destino ya está quieto. Cancel, error y destroy lo devuelven aunque la mezcla no haya arrancado.

Destino alcanzado: el Brain no mezcla y `CinemachineCore.IsLive` es verdadero para la cámara esperada. Tope: `blendSeconds + 0.25`. Si vence, hay warning. Si en esa comprobación el destino está quieto y activo, se acepta. Si no llegó, el evento se detiene y la concesión se suelta.

Ninguna acción de cámara toma `GameplayLock`. El pad de prueba sí toma el control del jugador, para que no camine durante el plano.

## QA

**A. Completar.** `Camera focused.`, luego `Camera restored.`, `[EventLog] S6 camera restored` y `Completed`. Sin warning de blend.

**B. Cancelar durante la espera.** Después de `Camera focused.` y antes del restore, Cancel en el inspector de `Event_S6_Focus`. Log: `Cancel (Inspector)`. No hubo `S6 camera restored` ni `Completed`.

Una salida de Play Mode intermedia escribió `Cancel (OnDisable)`. Esa no cuenta como B.

El caso C, forzar un blend que no llega, no se jugó. El código lo trata como fallo: warning con `live` y `blending`, `RequestStop` y suelta.

## Limitaciones

- El fade de obstáculos sigue mirando al jugador durante el plano.
- El shake de combate sigue en `VCam_Main` y no se ve mientras esa cámara no es la salida.
- Si el estilo por defecto del Brain es Cut, Cinemachine ignora el tiempo. Un `blendSeconds` mayor que 0 igual corta.
- Un segundo Focus del mismo runner, con la cámara dirigida ya activa, cambia el objetivo en el acto. El damping de follow es 0, así que no nace otro blend.
- La suelta al terminar el evento no espera la vuelta. Quien necesite esa espera usa Camera Restore.
- Estas acciones no están en el drawer de consecuencias de diálogo.

## Fuera de esta fase

FOV, distancia, rotación, markers de encuadre, shake de evento e Impulse, y el pad que compone un plano con diálogo. Eso es Fase 2, 3 y 4.
