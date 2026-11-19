# Sprint 09 — Phase 2: PlayTimelineAction

**Cerrada.** QA completo + cancel (con fix ActivationTrack) OK. Phase 1 CLOSED. Phase 3 (DialogueCheckpoint) no empezada. Sprints 05–08 no se reabrieron.

## Verificación (Step 1)

| Pregunta | Respuesta |
|----------|-----------|
| Start / await / cancel | `PlayableDirector.Play()`, wait hasta `state != Playing` o `time >= duration`, `Stop`+rewind en cancel |
| Compleción natural | No-Loop wrap; `HasFinished` cubre Stopped/Paused y fin por duración (Hold) |
| Cleanup | `stopOnCancel` → Stop + time 0 + Evaluate; luego `CameraDirector.Abort(runner)` |
| Ownership cámara | Política abajo |
| Conflicto CM / Director | Rechazo si un track bindea un Transform bajo el host `CameraDirector` |
| Restore gameplay | Abort oculta event slots; ActivationTrack del VCam de Timeline se apaga al Stop |

**Sin blocker.** No hace falta refactor grande de CameraDirector.

## Política de cámara (explícita)

**Timeline owns the shot while `PlayTimelineAction` runs; CameraDirector must not write at the same time.**

1. Al empezar: `CameraDirector.Abort(runner)` — suelta lease, oculta event cams, baseline = gameplay.
2. Durante Timeline: **no** `TryAcquire`. Un solo dueño de GameplayLock = el EventRunner (ya existente).
3. Timeline puede activar **sus propios** CinemachineCamera (prioridad alta).
4. Prohibido bindear slots hijos de `CameraDirector` → `RequestStop` / warning de editor.
5. Al terminar o cancelar: Stop del director + `Abort` otra vez (idempotente).

No se anima el root de `PlayerMotor` vía Timeline en este fixture ni en la acción.

## Archivos

Creados:

- `Assets/Game/Scripts/Events/Actions/PlayTimelineAction.cs`
- `Assets/Game/Events/Timelines/Lab_S9_Timeline.playable` (al correr el menú TestLab)
- `Assets/Game/Scripts/Events/SPRINT_09_PHASE_2_REPORT.md`

Modificados:

- `Assets/Game/Scripts/Editor/Events/EventActionRegistry.cs` — categoría Timeline
- `Assets/Game/Scripts/Editor/Events/EventActionEditorUtility.cs` — summary / Ping / warnings
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs` — fixture Phase 2

## Fixture

`Tools → ARPG → Events → Add Timeline Fixture (Sprint 09 Phase 2)`

Pad `Event_S9_Timeline` en `(0,1,-360)`.

Secuencia:

1. Log `S9 timeline begin`
2. `PlayTimelineAction` → `Timeline_S9_Root` (2.5s: activa `Subject_S9_Timeline` + `VCam_S9_Timeline` Priority 20)
3. Log `S9 timeline finished — next action ran` + `Completed`

## QA manual

1. Completo: begin → Timeline 2.5s → finished → `Completed`. **OK**
2. Cancel a mitad: sin `Completed`, Timeline/VCam apagados, control y cámara gameplay vuelven. **OK** (tras fix Evaluate@0)

## Fix post-QA

Cancel dejaba el VCam del Timeline activo: `Stop` + `Evaluate` a `time=0` reactivaba ActivationTracks. Ahora en interrupt: `Stop` sin Evaluate@0 y se desactivan bindings de `ActivationTrack`.

## Limitaciones

- No DialogueCheckpoint / signals Timeline → diálogo.
- No tracks custom para PlayerMotor.
- Wrap `Loop` se rechaza.
- `waitUntilFinished = false` arranca y sigue el runner sin esperar (avanzado; fixture usa true).
- PlayTimeline no está en Parallel ni en consequences de diálogo.
- Tras cancel, ActivationTargets se apagan (válido para el fixture que arranca inactive).
