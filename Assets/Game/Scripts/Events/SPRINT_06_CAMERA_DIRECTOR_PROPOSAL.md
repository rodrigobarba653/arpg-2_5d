# Sprint 06 — Camera Director / Cinemachine 3

Propuesta. Sin código. Sin prompt de implementación.

Sprint 05 quedó cerrado en `1c55904c` sobre `feature/interaction-tool`. No se reabre.

## Director

El proyecto ya usa Cinemachine 3.1.6. La cámara de gameplay es `Main Camera` con `CinemachineBrain` y una `CinemachineCamera` de seguimiento, `VCam_Main`. `CameraAutoTarget` le asigna `Follow` y `LookAt` al personaje activo. El shake de combate ya vive en esa misma cámara, vía `CameraShakeCinemachine` y Perlin. Los billboards miran la salida del Brain.

El Camera Director no es un runner ni una cutscene. Es un dueño de una cámara dirigida, distinta de `VCam_Main`.

`VCam_Main` sigue al jugador todo el tiempo, aunque no sea la que se ve. Una segunda `CinemachineCamera` sube de prioridad cuando un evento pide un encuadre. El Brain hace el blend. Al bajar esa prioridad, la imagen vuelve al seguimiento actual del jugador, no a la posición que tenía al empezar el evento.

`CameraAutoTarget` no se toca. Si el evento reescribiera el `Follow` de `VCam_Main`, el seguimiento de gameplay y el encuadre dirigido pelearían. El fade de obstáculos sigue mirando al jugador durante un plano dirigido; eso se revisa después, no se rediseña ahora.

Las acciones solo hablan con el Director. No toman `GameplayLock`. Si el evento debe congelar al jugador, eso lo sigue decidiendo `EventRunner.seizesPlayerControl` o el diálogo. Si el evento se cancela, el Director baja la cámara dirigida y el Brain vuelve a gameplay. No se guarda un snapshot de la posición del jugador.

## Qué falta mirar antes del primer prompt

- El prefab `Assets/Game/Prefabs/Camera/VCam_Main.prefab`: body, aim, offset, prioridad, y si el Perlin está ahí.
- El blend por defecto del Brain en BattleRing.
- Confirmar que `CameraAutoTarget` solo está en `VCam_Main`.

## Fases

1. **Switch y restore.** Una cámara dirigida, foco a un Transform, blend, y vuelta a gameplay. Cancelar a mitad también restaura.
2. **Encuadre.** Distancia, FOV y rotación de esa cámara dirigida, con duración. El diseñador coloca un empty en la escena; la acción apunta a ese marker.
3. **Shake de evento.** Un shake que no pise el de melee y daño. Lo más limpio en Cinemachine 3 es Impulse, para que lo reciba la cámara que esté activa.
4. **Composición.** Un pad que enfoca, espera, cambia de encuadre y restaura. Puede incluir un `StartDialogueAction` ya existente. No es un sistema nuevo de cutscenes.

## Acciones

| Acción | Cuándo |
|---|---|
| `CameraFocusAction` | Fase 1. Activa el plano, sigue y mira un Transform, espera el blend. |
| `CameraRestoreAction` | Fase 1. Baja el plano. También la llama el `Cancel` de cualquier acción de cámara que lo tenga tomado. |
| `CameraFrameAction` | Fase 2. FOV, distancia y rotación del plano activo. |
| `CameraMoveToAction` | Fase 2. Lleva el plano a un marker. |
| `CameraShakeAction` | Fase 3. Duración, amplitud y frecuencia, sin quedarse dueño de `VCam_Main`. |

Cuando se apruebe esta división, el primer prompt cubre solo la fase 1.
