# Sprint 06 — Fase 1: Switch y restore

Plan para aprobación. Sin código. No incluye encuadre, shake ni composición.

## Hallazgos

Cinemachine 3.1.6 ya está en el proyecto.

`VCam_Main` (`Assets/Game/Prefabs/Camera/VCam_Main.prefab`) es la cámara de gameplay:

- `CinemachineCamera` con prioridad apagada (`Priority.Enabled = 0`, valor 0). Compite como prioridad por defecto.
- `CinemachineFollow`, offset `(0, 4.5, -9.3)`, damping de posición en 0.
- `CinemachineHardLookAt`.
- `CinemachineBasicMultiChannelPerlin` con amplitud 0. El shake de combate lo sube `CameraShakeCinemachine` en ese mismo objeto.
- `CameraAutoTarget` solo está en este prefab. Escribe `Follow` y `LookAt` del personaje activo. No escribe prioridad.
- `CameraObstructionFade` mira a ese mismo personaje.
- `PersistentCamera` kind Vcam. El objeto es raíz y sobrevive al cambio de escena. No puede haber un segundo `PersistentCamera` de kind Vcam.

El `CinemachineBrain` está en `Main Camera`, no en el prefab. El blend por defecto es el del Brain de la escena. Fase 1 no lo reemplaza.

`EventRunner.Cancel` llama `Cancel` solo de la acción en curso y después `StopCoroutine`. `CleanupAfterRun` corre en éxito, stop y cancel. Una acción que ya terminó no vuelve a recibir `Cancel`.

## Ownership

`VCam_Main` sigue siendo del gameplay. Fase 1 no cambia su `Follow`, `LookAt`, offset, Perlin, prioridad ni `CameraAutoTarget`.

El Director es dueño de una segunda `CinemachineCamera`, hija de `VCam_Main`, mismo canal de salida y el mismo offset de follow. No usa `PersistentCamera`. Al ser hija, persiste con el padre.

Esa cámara queda desactivada cuando nadie la usa. Activarla con prioridad 10 hace que el Brain mezcle hacia ella. Desactivarla hace que el Brain vuelva a `VCam_Main`, que nunca dejó de apuntar al jugador.

Mientras el plano está tomado, el Director pone `StandbyUpdate` de `VCam_Main` en Always y guarda el valor anterior. Así el seguimiento no se queda atrás y la vuelta usa la posición actual del personaje. Al soltar, restaura el StandbyUpdate. Un cambio de personaje durante el plano lo sigue resolviendo `CameraAutoTarget` sobre `VCam_Main`.

Una sola concesión. El dueño es el `EventRunner` de `EventContext.Runner`.

- Otro runner que pida foco mientras hay concesión: `RequestStop` de ese segundo evento. No le quita el plano al primero.
- El mismo runner puede enfocar otro Transform. Sigue siendo la misma concesión y espera el blend nuevo.
- `Runner` null: `RequestStop`. Fase 1 no acepta estas acciones como consecuencia de un diálogo.

## Blend

`CameraFocusAction` sube el plano y no termina hasta que el Brain ya no está mezclando y la cámara dirigida es la activa, o hasta `blendSeconds + 0.25s`. Recién ahí sigue la acción siguiente. La concesión sigue tomada.

`CameraRestoreAction` baja el plano y no termina hasta que `VCam_Main` es la activa y el Brain ya no mezcla, con el mismo tope. Si no había concesión, termina en el acto.

Duración 0 no espera un blend. El Brain usa su estilo por defecto. La duración de la acción solo cambia el tiempo de esa transición: se guarda `DefaultBlend`, se aplica el tiempo, se dispara el cambio, y se devuelve el blend anterior en cuanto la mezcla ya arrancó. La mezcla en curso conserva la definición con la que nació.

## Cancel y fin del evento

`Cancel` de la acción de cámara, si es la acción en curso, suelta la concesión en el momento. No espera: `StopCoroutine` mataría esa espera. El Brain completa la vuelta solo.

Eso no alcanza. Si el foco ya terminó y la acción en curso es un `Wait` o un diálogo, `Cancel` de esa acción no conoce la cámara.

Por eso `CleanupAfterRun` llama `CameraDirector.Release(this)`. Corre en éxito, stop, cancel, disable y destroy. Soltar dos veces no hace nada. Si el autor no puso `CameraRestoreAction`, el plano no se queda pegado al terminar el evento. Esa suelta de seguridad no espera al blend. Quien necesite que la vuelta termine antes de la acción siguiente usa `CameraRestoreAction`.

Ninguna acción de cámara toma `GameplayLock`.

## Acciones de Fase 1

`CameraFocusAction`: Transform objetivo, `blendSeconds` (default 1). Sin objetivo o sin Brain: `RequestStop`.

`CameraRestoreAction`: `blendSeconds` (default 1).

Entran al menú de acciones del inspector del runner. No se añaden zoom, rotación, movimiento a un punto ni shake.

## Fuera de esta fase

- FOV, distancia, rotación y markers.
- Shake de evento. El Perlin de combate no se reasigna.
- El fade de obstáculos sigue mirando al jugador durante el plano. Puede desvanecer geometría que no tapa al NPC. Se anota, no se cambia.
- No se arma la cadena con diálogo. Eso es la fase 4.

## QA previsto, después de aprobar

Un pad nuevo en TestLab, sin rehacer los anteriores. `seizesPlayerControl = true`, Repeat. Acciones: foco a un empty, espera, restore, log.

- Completar: la mezcla termina antes de la espera, la vuelta termina antes del log, y el evento marca Completed.
- Cancelar desde el inspector durante la espera: vuelve `VCam_Main`, no hay Completed, y el jugador puede moverse.
