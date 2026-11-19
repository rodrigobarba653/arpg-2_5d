# Sprint 09 — Phase 3: DialogueCheckpoint

**Cerrada.** Pause + Continue QA OK. Phase 1–2 CLOSED. Sprint 09 cerrado. Sprints 05–08 no se reabrieron.

## Verificación

| Tema | Hallazgo |
|------|----------|
| APIs | `Marker` + `INotification` + `INotificationReceiver` (Timeline 1.8.12) |
| Diálogo | `DialogueService.TryStartInteractive` / `DialogueSession` / outcomes Completed\|Cancelled\|Failed |
| Pause vs finished | `PlayState.Paused` a mitad **no** es fin — `PlayTimelineAction.HasFinished` ajustado |
| Cámara | Política Phase 2 intacta (Abort lease; ActivationTrack cancel fix) |
| Locks | Sin segundo GameplayLock; el session puede lockear según el asset |
| Blocker | Ninguno |

## Archivos

Creados:

- `Assets/Game/Scripts/Events/Timeline/DialogueCheckpointMarker.cs`
- `Assets/Game/Scripts/Events/Timeline/TimelineDialogueBridge.cs`
- `Assets/Game/Events/Timelines/Lab_S9_DialoguePause.playable` (al correr el menú)
- `Assets/Game/Events/Timelines/Lab_S9_DialogueContinue.playable` (al correr el menú)
- `Assets/Game/Scripts/Events/SPRINT_09_PHASE_3_REPORT.md`

Modificados:

- `Assets/Game/Scripts/Events/Actions/PlayTimelineAction.cs` — bind/unbind bridge; Pause ≠ finished; espera Continue al final
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs` — fixtures Phase 3

## Authoring (Inspector / Timeline)

1. Timeline con `PlayableDirector` + componente `TimelineDialogueBridge` (el menú lo pone; `PlayTimelineAction` también lo añade si falta).
2. Window → Sequencing → Timeline, seleccionar el director.
3. En el **Markers** track: Add Marker → **Dialogue Checkpoint Marker**.
4. Asignar `DialogueAsset`, modo **Pause And Wait** o **Continue**, `allowCancel`, progression.
5. EventRunner: `PlayTimelineAction` apuntando a ese director.

## Pause & Wait

1. Marker dispara → `director.Pause()`.
2. `TryStartInteractive` (mismo DialogueService).
3. Bridge espera `session.IsFinished`.
4. **Completed** → `Resume()`.
5. **Cancelled / Failed** → `RequestStop` + `Stop` Timeline → EventRunner no marca Once/Completed.
6. `PlayTimelineAction` no trata el Pause intermedio como finished.

## Continue

1. Marker dispara → diálogo sin pausar Timeline.
2. Bridge **owns** la session; al cancelar el event / unbind → `session.Cancel`.
3. Si el Timeline termina con diálogo aún abierto, `PlayTimelineAction` **espera** esa session (no abandona).
4. Outcome ≠ Completed → `RequestStop` (no Complete silencioso).
5. Segundo interactive ya running → reject → fail cinematic.

## Cancel / ownership

- Cancel EventRunner → `PlayTimelineAction.Cancel` → bridge Unbind (cancela diálogo owned) → Stop Timeline → ActivationTrack deactivate → CameraDirector.Abort.
- Sin deadlock: checkpoint pausa **después** de emitir; el diálogo no espera un signal post-resume.
- Sin lease CameraDirector durante Timeline (Phase 2).

## Fixture

`Tools → ARPG → Events → Add DialogueCheckpoint Fixtures (Sprint 09 Phase 3)`

Requiere `Assets/Game/Dialogue/TestLab/Lab_Event_Dialogue.asset`.

| Pad | z | Modo |
|-----|---|------|
| `Event_S9_DlgPause` | -400 | Pause & Wait @ 1.0s |
| `Event_S9_DlgContinue` | -408 | Continue @ 1.0s |

Secuencia cada uno: Log begin → PlayTimeline → Log finished.

## QA manual

**Pause pad (`Event_S9_DlgPause`)**

1. Completar con E → resume → finished → `Completed`. **OK**
2. Esc en diálogo → Cancelled, sin `Completed`. **OK**
3. Cancel EventRunner durante diálogo → `timeline-unbind`, sin `Completed`. **OK**

**Continue pad (`Event_S9_DlgContinue`)**

4. Corrida completa (diálogo + Timeline sin pause) → finished → `Completed`. **OK**
5. Cancel EventRunner con diálogo abierto → ambos limpios, sin `Completed`. **OK**

## Puertas / switches (READ ONLY)

### Qué existe

- `SimpleDoor` — Open/Close, modos Switch/Key/etc.
- `DoorSwitch` — interact en trigger; legacy `doors[]` **o** `EventRunner` / `deactivateEventRunner` + toggle.
- `WorldInteractable` — Interact → `EventRunner.TryStart()`.
- EventActions: `OpenDoorAction`, `CloseDoorAction`, `SetDoorStateAction`, `UnlockDoorAction`.
- TestLab: `Tools → ARPG → Events → Add Door/Switch Test Fixtures`.

### Qué ya conecta a EventRunner

| Dirección | Soporte |
|-----------|---------|
| Switch → EventRunner | **Sí** (`DoorSwitch.eventRunner`) |
| EventRunner → Door | **Sí** (Open/Close/Set/Unlock actions) |
| WorldInteractable → EventRunner | **Sí** |
| Diálogo → Door | **Sí** vía consequences (`OpenDoorAction` etc. en el registry de consequences) o flags WorldState + condiciones |
| Door → dispara EventRunner al abrirse | **No** como callback genérico (salvo que el author ponga un trigger/switch aparte) |

### Qué falta (no implementado en Phase 3)

- No hay un “SwitchAction” genérico de world props más allá de puertas.
- No hay auto-wire Door.open → EventRunner sin `DoorSwitch` / interactable.
- Switches de gameplay que no sean `DoorSwitch` no están catalogados aquí.

## Limitaciones

- Markers usan estilo SignalEmitter (USS reutilizado).
- Continue con outcome Failed/Cancelled falla el cinematic entero (política explícita).
- No DialogueCheckpoint → branching Timeline.
- No tracks PlayerMotor.
