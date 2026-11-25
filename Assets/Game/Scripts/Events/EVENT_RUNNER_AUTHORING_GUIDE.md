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
| **Gameplay Follow Offset**    | Cambia el FollowOffset de gameplay (Apply / Restore). No usa lease. En Toggle OFF también sirve Camera Restore (ahora restaura follow). |
| **Start Mode → Toggle Cross** | Plano fino + cooldown: 1er cruce = Actions (ON), 2º = Toggle OFF Actions. Preferí espesor ≤ 1.            |
| **Camera Restore**            | Vuelve a gameplay: suelta lease (Shot/Focus) **y** restaura Gameplay Follow Offset si se aplicó. Blend 0 = cut. |
| **Move Character**            | Jugador o NPC. Destino: **World Position**, **Marker**, o **Relative Offset** (ej. X=2 desde donde está). Hold Until Event Ends (NPC) deja el patrol pausado hasta fin del event. |
| **Face Character**            | Mira un punto sin caminar (mismo seize en NPCs ambient)                                                   |
| **Play Animation**            | Pose 2D. **Hold Until Event Ends** = se queda en la pose. **Play Once** = una vez y sigue. Sin crossfade. |
| **Start Dialogue**            | Diálogo interactivo. **Lock World**: Use Asset / Stop World / Keep Moving. Espera a que termine por defecto. |
| **Wait**                      | Pausa en segundos                                                                                         |
| **Play Audio**                | SFX 2D / SFX 3D (en el runner) / **Voice** (canal stoppable). `Wait Until Finished` opcional. Voces de diálogo por línea van en el Dialogue Editor, no aquí. |
| **Parallel**                  | Varios pasos a la vez (ver abajo)                                                                         |
| **Play Timeline**             | Cinemática Timeline (opcional)                                                                            |
| **Set / Clear Flag**          | WorldState                                                                                                |
| **Open / Close Door**         | Puertas                                                                                                   |
| **Move Object**               | Mueve un `MovableObject` (escalera, roca, puente…) Rest↔Active. Wait opcional. Persistencia en el componente. |

---

## 4b. Ambient NPC (patrol + EventRunner)

Prefab: `Assets/Game/Prefabs/NPC/NPC_Ambient.prefab` (skin Soldier, **sin** EnemyAI).

| Pieza | Rol |
| --- | --- |
| `ScriptedNpcMotor` | Move / Face / Play Animation del EventRunner |
| `AmbientNpc` | Patrulla waypoints mientras está libre; se pausa al seize |
| `NpcDialogueHost` | Ambient + interactive |

Move / Face / Play Animation hacen **Seize** → se corta la ruta y (opcional) `dialogue.available = false`. Al soltar → vuelve a patrullar y al diálogo.

Menú: **Tools → ARPG → Level Design → Create Ambient NPC Prefab** (y **Replace TestLab Ambient NPC**).

No uses Soldier/`EnemyAI` para NPCs de historia. Enemigos son otro stack; el EventRunner aún no overridea su patrol.

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

Consequences del diálogo (en el asset) pueden SetFlag, OpenDoor, Move, **Play Audio**, etc. — **no** Camera Shot ni Parallel ni Start Dialogue anidado.

**Voz por línea:** en el Dialogue Editor, cada Dialogue Line tiene **Voice / Audio → Clip**. Arranca con el typewriter; se corta al Continue / cancel / siguiente línea. Necesita `AudioManager` en escena.

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

## 10b. Move Object (escalera, roca, puente…)

Para props que **se desplazan** (no puertas):

1. En el mesh (escalera, piedra…): Add Component → **Movable Object**.
2. Pose en escena = **Rest (A)**. Active (B) = **Offset** (ej. `Y = -4`) o un **Marker** vacío.
3. Persistencia: **Persist State** + **Flag Id** (`lab_ladder_down`). Al terminar el tween escribe el flag; al load hace snap.
4. En el EventRunner del switch (u otro):
   - Add → **World → Move Object** → asigná el `MovableObject`, State = **To Active**, Wait = ON.
   - Toggle OFF: otro Move Object → **To Rest**.

El `Ladder` de trepar no cambia: solo hace falta que el collider/trigger quede al alcance tras el move.

No hace falta un Set Flag aparte si Persist State está ON (el componente escribe el flag al llegar).

---

## 10c. Encounter Kill Gate (matar N enemigos → event)

**No** es “matar → abrir puerta”. Es: **matar N enemigos elegidos → arrancar un EventRunner**.  
Ese runner hace lo que quieras (Open Door, Move Object, Wait, Camera, Set Active del boss, diálogo…).

### Setup

1. GO vacío (ej. `Encounter_RoomA`) + **Encounter Kill Gate** + **Event Runner**.
2. EventRunner → **Start Mode = Manual Only** (el gate lo dispara).
3. En el Kill Gate:
   - **Enemies**: arrastrá los malos que cuentan, **o** parenéalos bajo el GO y pulsá **Collect Children**.
   - **Required Kills**: `0` = todos los listados; `15` = bastan 15 aunque haya más en la lista.
   - **Persist Completion** + **Completed Flag Id** (`room_a_cleared`) para no re-disparar tras save.
4. En el EventRunner → Actions: Open Door / Move Object / Set Active (boss) / Wait / Camera…

### Spawns en runtime

En el prefab del enemigo: **Encounter Member** → asigná el Kill Gate. Al spawnear se registra solo.

### Ejemplos

| Querés… | Kill Gate | EventRunner Actions |
|---------|-----------|---------------------|
| Matar 3 → abrir puerta | 3 enemigos, Required = 0 o 3 | Open Door (+ Persist en la puerta) |
| Matar 15 de un área → boss | lista grande, Required = 15 | Set Active del boss / Play Timeline / Dialogue |
| Matar un jefe concreto | 1 enemigo | cutscene, Move Object, flags… |

El gate **solo cuenta kills y llama `TryStart()`**. La consecuencia vive en el EventRunner.

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
