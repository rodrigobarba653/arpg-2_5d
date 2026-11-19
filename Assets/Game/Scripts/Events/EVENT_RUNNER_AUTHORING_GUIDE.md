# Guía rápida — EventRunner (authoring)

Cómo armar cutscenes de gameplay con el Event System. Sin Timeline al principio; Timeline es opcional al final.

---

## 1. Qué es

`EventRunner` es la lista de pasos de un event:

- Corre **en orden** (uno tras otro).
- Puede tener un bloque **Parallel** (varios a la vez).
- Puede **pausar al jugador** (`Seize Player Control`).
- Al terminar bien → `Completed`. Si cancelás o falla → **no** marca Completed.

No es una ventana aparte. Es el **Inspector** del componente `EventRunner`.

---

## 2. Crear un event desde cero

1. Creá un GameObject vacío (o un trigger/volumen).
2. Add Component → **Event Runner**.
3. En el Inspector del EventRunner hay **dos controles distintos** (no son lo mismo):
   - **Execution Mode (Run Limit)** — cuántas veces puede _completar_ bien: **Once** o **Repeat**.
   - **Start Mode (How It Starts)** — qué dispara el intento: Manual / Trigger / Toggle Cross / Interact.
4. **Start Mode** (vive en el propio EventRunner):
   - **Manual Only** — solo Try Start / código.
   - **Trigger On Enter** — al caminar dentro del volumen (usa el Collider del mismo GO; Is Trigger).
   - **Trigger On Exit** — al salir del volumen.
   - **Toggle Cross** — cada cruce del plano dispara ON / OFF (collider fino + cooldown). Suele ir con **Repeat**.
   - **Interact** — pulsar Interact cerca (crea `WorldInteractable` + radios).
5. Completá:
   - **Event Id** — id estable (ej. `intro_pontiff`). Obligatorio si usás Persist Once.
   - **Seize Player Control** — casi siempre ON en cutscenes.
   - **Persist Once** — solo con Once; guarda el Completed en WorldState (save).

---

## 3. Inspector — Actions

Seleccioná el GO → Inspector → sección **Actions**.

| Control      | Qué hace                                                                         |
| ------------ | -------------------------------------------------------------------------------- |
| **+** (Add)  | Menú por categoría: Camera, Characters, Dialogue, World, Flow, Rewards, Timeline |
| Arrastrar ↑↓ | Reordenar pasos                                                                  |
| Foldout      | Abrir/cerrar detalles del paso                                                   |
| **Ping**     | Resalta en Hierarchy el marker / personaje / director asignado                   |

Los pasos se numeran: `1. …`, `2. …`.

### Conditions (opcional)

Arriba de Actions. Todas deben pasar (AND) para que el event arranque (flags, “event completed”, etc.).

---

## 4. Acciones más usadas

| Acción                        | Uso típico                                                                                                |
| ----------------------------- | --------------------------------------------------------------------------------------------------------- |
| **Camera Shot**               | Corte o blend a un `CameraShotMarker` (Transition Cut / Blend)                                            |
| **Camera Focus**              | Sin offset: follow gameplay al Target. Con offset: truck desde la **cámara** (X/Y/Z mundo), luego **sigue al Target** manteniendo ese encuadre. |
| **Gameplay Follow Offset**    | Cambia el FollowOffset de la cámara de gameplay (sigue al player). Apply / Restore. No usa lease de shot. |
| **Start Mode → Toggle Cross** | Plano fino + cooldown: 1er cruce = Actions (ON), 2º = Toggle OFF Actions. Preferí espesor ≤ 1.            |
| **Camera Restore**            | Vuelve a gameplay: **Blend Seconds = 0 → cut**, **>0 → blend**                                            |
| **Move Character**            | Jugador activo o NPC con `ScriptedNpcMotor`                                                               |
| **Face Character**            | Mira un punto sin caminar                                                                                 |
| **Play Animation**            | Pose 2D. **Hold Until Event Ends** = se queda en la pose. **Play Once** = una vez y sigue. Sin crossfade. |
| **Start Dialogue**            | Diálogo interactivo; por defecto espera a que termine                                                     |
| **Wait**                      | Pausa en segundos                                                                                         |
| **Parallel**                  | Varios pasos a la vez (ver abajo)                                                                         |
| **Play Timeline**             | Cinemática Timeline (opcional)                                                                            |
| **Set / Clear Flag**          | WorldState                                                                                                |
| **Open / Close Door**         | Puertas                                                                                                   |

---

## 5. Parallel + Start Delay

1. Add → **Flow → Parallel**.
2. Dentro del Parallel, **+** para hijos (Camera, Move, etc.).
3. En cada fila: **Delay (s)**.
   - `0` = arranca ya.
   - `0.5` = espera medio segundo y después corre esa acción.
4. El grupo **espera a que todos** terminen antes de seguir al siguiente paso del EventRunner.

Ejemplo:

```
Parallel
  ├── Camera Shot     Delay 0.0
  ├── Move Pontiff    Delay 0.5
  └── Move Jyn        Delay 1.5
```

No hace falta crear a mano un “DelayedStart”: el Delay del Inspector lo arma solo.

**Reglas:** no dos Camera Shot/Move del mismo personaje en el mismo Parallel. El Inspector avisa con warnings.

---

## 6. Asignar referencias

- **Markers de cámara:** creá un GO + `CameraShotMarker` (posición/rotación/FOV del plano).
- **Destinos de walk:** un Transform vacío en el mundo.
- **NPC scripted:** GO con `CharacterController` + `ScriptedNpcMotor` (+ Animator si querés Walk/Idle).
- **Explicit character:** en Move/Face/Play, Source = Explicit y arrastrá el Transform del NPC.

Usá **Ping** para ubicar lo que ya asignaste.

---

## 7. Probar en Play

1. Entrá a Play Mode.
2. Seleccioná el EventRunner.
3. Interactúá en el mundo **o** pulsá **Try Start** en el Inspector.
4. Mientras corre: **Cancel** corta el event (no marca Completed; el control vuelve).

---

## 8. Timeline (opcional)

Para cinemáticas largas y precisas:

1. GO con **Playable Director** + Timeline asset.
2. En el EventRunner: Add → **Timeline → Play Timeline** → asigná el Director.
3. El runner **espera** a que el Timeline termine (salvo que desactives Wait).
4. **Cancel** detiene el Timeline y limpia cámara/activaciones.

### DialogueCheckpoint (diálogo en medio del Timeline)

1. Abrí **Window → Sequencing → Timeline**.
2. Seleccioná el GO del Playable Director.
3. En el track de Markers: Add → **Dialogue Checkpoint Marker**.
4. Asigná un `DialogueAsset` y el modo:
   - **Pause And Wait** — pausa el Timeline, diálogo, al completar resume.
   - **Continue** — el Timeline sigue mientras corre el diálogo.
5. El mismo GO debe tener (o se añade solo) **Timeline Dialogue Bridge**.

Política de cámara: mientras corre Timeline, no pelees con CameraDirector (no bindees los slots del CameraDirector). Usá un VCam propio del Timeline si necesitás plano dirigido.

---

## 9. Diálogo sin Timeline

Add → **Dialogue → Start Dialogue** → asigná el asset.  
Por defecto espera a que el jugador termine el diálogo. Esc / cancel → el event no Completa.

Consequences del diálogo (en el asset) pueden SetFlag, OpenDoor, Move, etc. — **no** Camera Shot ni Parallel ni Start Dialogue anidado.

---

## 10. Puertas + Switch (Door Creator)

**Tools → ARPG → Level Design → Door Creator**

- **Auto / Key** — la puerta se maneja sola (proximidad o llave). No hace falta EventRunner.
- **Switch** — crea:
  1. `SimpleDoor` (Open Mode = Switch)
  2. `DoorSwitch` (Interact cerca)
  3. `EventRunner` en el mismo Switch, Start Mode = **Manual Only**, con action **Open Door**
  4. `InteractivePromptTrigger` (prompt del jugador)

Flujo: jugador cerca del switch → Interact → EventRunner → abre la puerta.

Para cutscene (cámara, wait, diálogo): seleccioná el **EventRunner del Switch** y agregá Actions **antes o después** de Open Door.

| Querés… | Dónde |
|---------|--------|
| Que solo se use una vez | EventRunner → **Execution Mode = Once** (+ Persist Once si debe guardarse) |
| Que la puerta quede abierta tras save | **SimpleDoor** → Persist State + Persistent Id |
| Prompt “Interact” | Ya viene `InteractivePromptTrigger` |
| Sonido / mesh palanca ON-OFF / no saltar cerca | **DoorSwitch** (Look & sound / Lock Jump) |

No uses Legacy Doors / One Shot / Persist del switch (están en Advanced solo por compat).

---

## 11. Menús TestLab (fixtures listos)

Con la escena TestLab abierta, **salí de Play** y usá:

**Tools → ARPG → Events → …**

Ejemplos Sprint 09:

- Add Staggered Delay Fixture
- Add Timeline Fixture
- Add DialogueCheckpoint Fixtures

Crean pads de prueba. Guardá la escena después.

---

## 12. Checklist rápido

1. EventRunner + (opcional) WorldInteractable
2. Event Id si Persist Once
3. Actions en orden; Parallel solo donde hace falta
4. Delays en Parallel, no Wait hermanos para “stagger”
5. Si usaste Camera Shot y querés volver a gameplay, agregá **Camera Restore** y elegí cut (0) o blend (>0). Sin Restore, al terminar el event igual vuelve (cut).
6. Probar: completo + Cancel a mitad

### Cámara: cut vs blend (ida y vuelta)

```
1. Camera Shot     Transition = Cut   (o Blend + Blend Seconds)
2. Wait            1.5                (opcional: cuánto dura el plano)
3. Camera Restore  Blend Seconds = 0  → cut de vuelta
                   Blend Seconds = 1  → blend de vuelta
```

- **Ida** la elige el Camera Shot (Cut / Blend).
- **Vuelta** la elige el Camera Restore (0 = cut, >0 = blend).
- Cancel / fin sin Restore → vuelve en **cut** (no ease sorpresa).

---

## 13. Qué no hace esta herramienta

- No es un node graph ni un editor aparte.
- No convierte Actions ↔ clips de Timeline.
- No mueve miembros dormidos de la party.
- No anida Parallel dentro de Parallel.
