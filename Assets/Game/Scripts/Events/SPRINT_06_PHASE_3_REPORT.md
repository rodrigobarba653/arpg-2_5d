# Sprint 06 — Fase 3: Event Camera Shake

Jugado en TestLab. El usuario aprobó la fase. Dos pasadas completas de `Event_S6_Shake` y un `Cancel (Inspector)` durante el segundo shake. No hubo warning de shake, blend ni restore.

Fase 1 y Fase 2 no se reabrieron. Fase 4 no está hecha.

## Qué se vio

Cada pasada completa escribió, en orden:

- `Camera shake amplitude=2.50 duration=0.45s frequency=1.50.` y `Camera shake settled.`
- `Camera blend 'Shot_S6_Shake' blend=1.00s.` y `Camera blend settled.`
- `Camera shake amplitude=3.50 duration=0.60s frequency=2.00.` y `Camera shake settled.`
- `Camera restored.`
- `[EventLog] S6 shake restored`
- `Completed`

El primer shake ocurre con la cámara de gameplay. El segundo ocurre después del blend, en el plano del cubo rojo.

La cancelación aprobada llegó a `Camera shake amplitude=3.50` y ahí salió `Cancel (Inspector)`. No hubo `Camera shake settled.`, ni restore, ni `Completed`.

Una cancelación anterior, en otra pasada, cortó durante `Camera blend 'Shot_S6_Shake'`, antes del segundo shake. También soltó el control y no marcó `Completed`. No es el caso del shake dirigido.

Los avisos de Animator, `DontDestroyOnLoad` y `WeaponPerspectiveAlignment` salen al arrancar TestLab. El cambio de Ax a Jyn es el swap del grupo. No vienen de este shake.

## Qué quedó

`CameraShakeAction` emite un Impulse Legacy y espera su duración. Cada disparo construye su propio `CinemachineImpulseDefinition`. La señal es el perfil 6D Shake que ya tiene el Perlin de `VCam_Main`; ese Perlin no se escribe.

El listener está en `VCam_Main` y en los dos slots dirigidos. Canal 1, gain 1, después de Noise, en espacio de cámara, sin ruido secundario. La acción no toma la concesión. Cancelar llama `ImpulseEvent.Cancel` sobre ese evento. No usa `CinemachineImpulseManager.Clear()`. Si el runner tenía un plano, el cleanup que ya existía suelta la concesión.

`CameraShakeCinemachine`, el Perlin de combate, `GameplayLock`, `CameraAutoTarget` y el diálogo no se modificaron.

## Archivos

- `Assets/Game/Scripts/Events/Actions/CameraShakeAction.cs`
- `Assets/Game/Scripts/Camera/CameraDirector.cs`
- `Assets/Game/Prefabs/Camera/VCam_Main.prefab`
- `Assets/Game/Scripts/Editor/Events/EventRunnerEditor.cs`
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs`

Fixture: `Tools → ARPG → Events → Add Camera Shake Fixture (Sprint 06 Phase 3)`. Pad verde `Event_S6_Shake` en z≈-236, `eventId` `lab_s6_shake`. El menú no borra los pads anteriores.

## Limitaciones

- El shake dura lo que dice la acción y después sigue la secuencia. No acompaña un viaje de cámara.
- Solo tiemblan `VCam_Main` y las dos cámaras dirigidas.
- Un golpe de combate durante un plano dirigido sigue sin verse. El Perlin de combate no se copió a esos slots.
- El fade de obstáculos sigue mirando al jugador.

## Siguiente

Fase 4: composición de cámara y diálogo. No está hecha.
