# Sprint 09 — Resumen

Authoring de EventRunner + Start Delay + Timeline + DialogueCheckpoint.

Sprints 05–08 CLOSED. No se reabrieron.

## Estado

| Phase | Contenido | Estado |
|------:|-----------|--------|
| 0 | Architecture & Editor audit | CLOSED (aprobado) |
| 1 | Inspector UX + `DelayedStartAction` | CLOSED (QA OK) |
| 2 | `PlayTimelineAction` + política cámara | CLOSED (QA OK + fix ActivationTrack) |
| 3 | `DialogueCheckpoint` Pause&Wait / Continue | CLOSED (QA OK) |

## Qué entrega cada phase

### Phase 1
- `DelayedStartAction` + Parallel delay en Inspector (sin armar wrapper a mano).
- Registry de acciones compartido / menú por categoría.
- Fixture `Event_S9_Delay` @ z≈-320.

### Phase 2
- `PlayTimelineAction`: play, await, cancel, Stop sin reactivar VCam.
- Cámara: Abort lease de CameraDirector; Timeline usa VCams propios.
- Fixture `Event_S9_Timeline` @ z≈-360.

### Phase 3
- `DialogueCheckpointMarker` + `TimelineDialogueBridge`.
- Pause & Wait / Continue sobre `DialogueService` existente.
- Fixtures `Event_S9_DlgPause` @ -400, `Event_S9_DlgContinue` @ -408.
- Puertas/switches: ya conectadas a EventRunner (audit read-only en Phase 3 report).

## Docs

- `SPRINT_09_PHASE_0_AUDIT.md`
- `SPRINT_09_PHASE_1_REPORT.md`
- `SPRINT_09_PHASE_2_REPORT.md`
- `SPRINT_09_PHASE_3_REPORT.md`

## Siguiente

Sprint 09 CLOSED. Commit cuando lo pidas.
