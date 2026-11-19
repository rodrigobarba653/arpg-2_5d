# Sprint 08 — Fase 1: movimiento y animación scripted

**Cerrada.** Movimiento y animación visual del NPC OK. Sprint 06 y 07 no se reabrieron. No hay Timeline, NavMesh de jugador ni miembros dormidos de la party. No se empezó Sprint 09.

## Commit preventivo

Antes de tocar `PlayerMotor`: `78079ee4` — Sprint 07 + audit Phase 0 de Sprint 08.

## Qué hace

`PlayerMotor` tiene un canal scripted. Camina en XZ hacia un punto fijo, con velocidad, radio de parada y timeout en la acción. Sigue usando el mismo `CharacterController.Move`, gravedad y pendientes. Mientras el canal está activo, el stick se ignora y se limpia. `TopDownAnimDriver` ve el input scripted y pone Walk/Idle.

`ScriptedNpcMotor` es el mínimo para NPC narrativos: recta a un marker, facing a 8 direcciones, Walk/Idle si hay Animator. Sin AI ni NavMesh. Elige el Animator con parámetro `IsMoving` (SpriteBody), ignora prompts/VFX, y fuerza Idle al cancelar/llegar.

Acciones:

- `MoveCharacterAction` — jugador activo o Transform explícito.
- `FaceCharacterAction` — mira un Transform o una dirección; no deja el facing locked.
- `PlayCharacterAnimationAction` — estado conocido; pausa el driver de locomoción y lo restaura al terminar o cancelar. Rechaza combate/climb.

`ParallelActions` rechaza dos moves del mismo cuerpo.

## Menú

`Tools → ARPG → Events → Add Scripted Movement Fixture (Sprint 08 Phase 1)`

Salir de Play Mode, correr el menú y guardar la escena. No borra pads anteriores.

`Event_S8_Move` en `(0, 1, -280)`. Interactuar (no trigger OnEnter).

`NPC_S8_Walker` ya no es un cubo: instancia scene-only de `Jyn (PlayerPrefab)`, unpack completo, sin sistemas de jugador. Conserva `SpriteBody` + `JynPlayer.controller`. Lo mueve `ScriptedNpcMotor`.

Orden:

1. Paralelo: blend a `Shot_S8_Wide` + jugador a `Marker_S8_PlayerWalk` + NPC a `Marker_S8_NpcWalk`.
2. NPC a `Marker_S8_NpcWalkB`, luego `Marker_S8_NpcWalkC` (más facings).
3. `FaceCharacter` del NPC hacia `Marker_S8_NpcFace` (sin moverse).
4. `Play Idle` breve en el NPC.
5. Face + Idle del jugador (cobertura previa).
6. Restore.
7. `[EventLog] S8 movement restored` y `Completed`.

## Limitaciones

- Miembros dormidos de la party no caminan. Hace falta un modo narrativo futuro en `PartyMemberControl`.
- El facing sigue siendo relativo a la cámara actual (incluye planos dirigidos).
- `PlayCharacterAnimation` solo estados que existan en el Animator. Idle es el caso seguro del fixture.
- Cancel no retrocede la posición.
- El path del NPC muestra varias direcciones, no las ocho en un solo frame.

## QA movimiento (cerrado)

1. Secuencia completa (versión cubo): paralelo, face, Idle, restore, `Completed`. **OK**
2. Cancel durante el move. **OK**
3. Cancel durante Idle. **OK**

## QA animación NPC

1. Idle → Walk al empezar el move del NPC; Idle al llegar a cada marker. **OK**
2. El sprite cambia de facing en las piernas SW → E → N. **OK**
3. Tras `Marker_S8_NpcWalkC`, Face hacia `Marker_S8_NpcFace` gira el sprite sin desplazar. **OK**
4. Cancel mid-move: Idle inmediato, sin Walk atrapado; stick del jugador vuelve. **OK**
5. Cancel durante `Play Idle` del NPC: ownership liberada, Idle de locomoción, sin animación trabada. **OK**

## Archivos

Creados:

- `Assets/Game/Scripts/Characters/ScriptedNpcMotor.cs`
- `Assets/Game/Scripts/Events/ScriptedCharacterTarget.cs`
- `Assets/Game/Scripts/Events/Actions/MoveCharacterAction.cs`
- `Assets/Game/Scripts/Events/Actions/FaceCharacterAction.cs`
- `Assets/Game/Scripts/Events/Actions/PlayCharacterAnimationAction.cs`

Modificados:

- `Assets/Game/Scripts/Player/PlayerMotor.cs`
- `Assets/Game/Scripts/Events/Actions/ParallelActions.cs`
- `Assets/Game/Scripts/Editor/Events/EventRunnerEditor.cs`
- `Assets/Game/Scripts/Editor/Dialogue/DialogueConsequenceListDrawer.cs`
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs` (NPC visual Jyn + path/face NPC)
- `Assets/Game/Scripts/Characters/ScriptedNpcMotor.cs` (resolver Animator locomoción + Idle al cancelar)

No se tocaron `PartyMemberControl`, `CameraDirector`, `DialogueService`, enemigos, puertas ni inventario. El asset `Jyn (PlayerPrefab)` no se modificó.
