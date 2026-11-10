# Sprint 05 — Phase B Step 3 Report

**Fecha:** 2026-11-10  
**Estado:** Implementado. Falta la QA manual en TestLab.  
**Siguiente:** Parar. No empezar Step 4.

La base sigue siendo `SPRINT_05_PHASE_B_PLAN.md`, `SPRINT_05_PHASE_B_STEP_1_REPORT.md` y `SPRINT_05_PHASE_B_STEP_2_REPORT.md`. Steps 1 y 2 no se regeneran.

---

## Archivos

Creados:

- `Dialogue/Data/DialogueChoiceOption.cs`
- `Dialogue/Data/DialogueChoiceNode.cs`
- `Dialogue/Runtime/DialogueChoicePresentation.cs`
- `Dialogue/Runtime/DialogueChoiceLayout.cs`
- `Dialogue/Presentation/DialogueChoiceView.cs`
- `Events/SPRINT_05_PHASE_B_STEP_3_REPORT.md` (este archivo)

Modificados:

- `Dialogue/Runtime/DialogueSession.cs` — corre el nodo, esconde el menú en `Finish`
- `Dialogue/Runtime/DialogueInput.cs` — navegación del menú
- `Dialogue/Presentation/DialogueUIController.cs` — `choicePrefab` y el placeholder
- `Dialogue/Runtime/DialogueLayoutSettings.cs` — presentación por defecto, LowerCenter
- `Dialogue/Data/DialogueAsset.cs` — override opcional de esa presentación
- `Dialogue/NPC/NpcDialogueHost.cs` — `interactiveProgression`, default Manual
- `Editor/Dialogue/DialogueAssetEditor.cs` — se puede añadir un Choice en el inspector
- `Editor/Events/EventSystemTestLabBuilder.cs` — menú nuevo, sin tocar fixtures viejos

Sin cambios: `BuildConversationPlan`, retratos, tamaño de chatbox, typewriter, `GameplayLock`, outcomes de Step 2, variantes de Step 1.

---

## Cómo corre un choice

`DialogueSession.Run` sigue siendo el único runner. Un `DialogueChoiceNode` no es un hablante.

1. Junta las opciones escritas. Ignora `textVariants`, `requiredCharacterId` y `availability`. Step 5 las va a usar. Hoy todas se pueden elegir.
2. Si no hay ninguna y `fallbackNodeId` está vacío: `Fail("empty-choice")`.
3. Si no hay ninguna y el fallback tiene id: sigue ese id, sin menú. Si el id no existe, `Failed` (`missing-node`).
4. Si hay opciones, espera a que se suelte Confirm, enseña el menú y espera otra pulsación. Nunca elige solo.
5. La opción elegida aporta `nextNodeId`. Vacío es `Completed`. Un id que no existe es `Failed`.
6. El nodo destino vuelve a entrar al mismo bucle. Varias ramas pueden caer en la misma línea.
7. `Finish` destruye el menú en `Completed`, `Cancelled` y `Failed`. Esc, Interrupt y destruir el servicio pasan por `Cancel` y por ese mismo `Finish`.

El paseo de hablantes también sigue los `nextNodeId` de las opciones, una sola vez al armar el plan. Así el par villager / jugador ya tiene sitio antes del menú y abrirlo no reconstruye las burbujas. Eso no es el layout de ramas de Step 4. `BuildConversationPlan` no cambió.

El menú se coloca con `DialogueChoiceLayout` en el borde inferior del área segura (`LowerCenter`). `NearPlayerChatbox` existe en el enum y, si alguien lo marca, hoy cae en ese mismo sitio. Step 7 lo separa de la burbuja del jugador.

---

## Confirm y el resto del input

| Acción    | Teclas                                       |
| --------- | -------------------------------------------- |
| Anterior  | W, flecha arriba, d-pad arriba, stick arriba |
| Siguiente | S, flecha abajo, d-pad abajo, stick abajo    |
| Elegir    | E, Enter, South                              |
| Cancelar  | Esc, East, solo si `allowCancel`             |

El stick usa umbral 0.5 y no vuelve a moverse hasta regresar al centro. Teclas y d-pad repiten a los 0.28 s y luego cada 0.18 s.

La Confirm que cierra la pregunta llama `SuppressConfirmUntilReleased`. El menú no acepta Confirm hasta soltarla, así que esa pulsación no elige la primera respuesta. La Confirm que elige una respuesta vuelve a suprimir hasta soltarla, y la línea siguiente espera esa suelta antes de escribir. Mantener Confirm no elige dos veces: solo cuenta `WasPressedThisFrame`.

Automatic avanza las líneas y se detiene en el menú. Fast-forward no se lee mientras el menú está abierto. La navegación solo se lee dentro de `PlayChoice`, así que en una línea normal no hace nada.

---

## Compilación

No se lanzó una segunda instancia de Unity. Hay que salir de Play Mode y dejar que el editor recompile. El menú nuevo aparece después de eso.

---

## QA en TestLab

1. Sal de Play Mode. Abre `EventSystem_TestLab`.
2. **Tools → ARPG → Events → Add Dialogue Choice Fixture (Phase B Step 3)**. Guarda. No regenera Phase A, el NPC verde ni los pads de Step 2.
3. Play. Los cuatro NPC nuevos están en **z ≈ −36**. **E / Enter / South** habla. El primer E termina el typewriter. El segundo sale de la línea y abre el menú, si ya soltaste la tecla.

| x   | Objeto                 | Color  | Qué hacer                                               | Qué debe pasar                                                                                                                    |
| --- | ---------------------- | ------ | ------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| 0   | `NPC_Choice_Converge`  | Dorado | Tres respuestas. Prueba cada una en una charla distinta | Izquierda, centro y derecha dicen su línea y las tres llegan a "Then we agree." Las burbujas no saltan al abrir o cerrar el menú. |
| 8   | `NPC_Choice_Automatic` | Cian   | E, no confirmes el menú                                 | La primera línea se escribe y avanza sola. El menú se queda hasta que eliges. Elegir dice "I confirmed it myself."                |
| 16  | `NPC_Choice_BadTarget` | Rojo   | Elige la única respuesta                                | Warning `Session failed (missing-node)` con `missing_choice_node`. El menú desaparece y puedes volver a moverte.                  |
| 24  | `NPC_Choice_Cancel`    | Azul   | Abre el menú y pulsa Esc                                | `Session cancelled (user-cancel)`. No queda menú ni candado.                                                                      |

En el menú, W/S, flechas, d-pad y stick mueven la fila azul. Gamepad South elige. Mantener E al salir de la pregunta no debe marcar una respuesta. Mantener E al elegir no debe completar de golpe el typewriter de la línea siguiente.

Los NPC de Phase A, el verde de variantes y los pads de Step 2 siguen donde estaban. El verde sigue sin lock a propósito.

Interrupt: Esc, el pad morado y destruir el servicio usan el mismo `Finish`, que tira el menú. Estos cuatro NPC bloquean el movimiento, así que no se puede caminar hasta el morado con el menú abierto. Para ver ese caso hay que apagar `interactiveLockGameplay` en uno de ellos. No se tocó `GameplayLock`.

---

## Fuera de este paso

Slots de rama, filtros, condiciones, etiquetas por personaje, guarda de ciclos, fuse de frames, `NearPlayerChatbox`, inspector grande, `DialogueBranchNode`, `StartDialogueAction`, consecuencias y quests.
