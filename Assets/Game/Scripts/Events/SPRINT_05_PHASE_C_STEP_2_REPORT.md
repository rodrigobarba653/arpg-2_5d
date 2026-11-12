# Sprint 05 — Phase C Step 2 Report

**Fecha:** 2026-11-11  
**Estado:** QA jugada. Los cuatro casos pasaron.  
**Siguiente:** Parar. No implementar consecuencias ni el paso 3.

---

## 1. Archivos

Creados:

- `Assets/Game/Scripts/Dialogue/Data/DialogueConditionNode.cs`
- Este reporte.

Modificados:

- `Assets/Game/Scripts/Dialogue/Runtime/DialogueSession.cs`
- `Assets/Game/Scripts/Dialogue/Runtime/DialogueGraph.cs`
- `Assets/Game/Scripts/Editor/Dialogue/DialogueAssetEditor.cs`
- `Assets/Game/Scripts/Editor/Dialogue/DialogueAssetValidator.cs`
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs`

No se tocó `EventRunner` ni `StartDialogueAction`. No se reescribieron assets de Phase A ni Phase B.

---

## 2. Datos

`DialogueConditionNode` hereda de `DialogueNode`.

- `conditions`: `[SerializeReference] List<EventCondition>`. La misma lista que la disponibilidad de un choice.
- `trueNodeId`: destino si todas pasan.
- `falseNodeId`: destino si alguna falla.

Lista vacía es true. Los null se saltan. El resto es AND, vía `DialogueChoiceAvailability.ConditionsAllow`. No escribe `WorldState` y no corre `EventAction`.

Destino vacío termina en `Completed`. Destino con id que no existe es `Failed (missing-node)`. `Finish` suelta la UI y el lock de la sesión, como en una línea rota.

---

## 3. Recorrido

Al llegar al nodo, en ese momento, se evalúan las condiciones y se sigue al destino. No hay caja ni menú ni confirmación. La sesión y el personaje congelado no cambian.

Se vuelve a evaluar si la charla pasa otra vez por el mismo nodo. No se guarda el resultado.

El log, con `debugLog`:

`[Dialogue] Condition 'id' passed=true|false next='destino' asset='…'.`

---

## 4. Ciclo, Manual y Automatic

El conjunto de líneas automáticas no cambió. Sigue fallando con `automatic-cycle` / `revisited without a choice confirmation`, y solo se limpia al confirmar un choice.

Un nodo Condition entra en otro conjunto, en Manual y en Automatic. Si se repite antes de que el jugador tenga que esperar una línea o un menú, falla con `automatic-cycle` / `revisited without a player wait`. Ese conjunto se limpia cuando una línea o un choice sí espera, y también al confirmar un choice. Un loop de choice que ya pasaba el paso 6 sigue pudiendo volver a pasar por una condición.

El tope de 32 por frame no se movió. Cuenta el nodo Condition. A los 32 cede un frame y reintenta el mismo nodo, sin marcarlo como fallo.

---

## 5. Layout

`DialogueGraph` recorre `trueNodeId` y `falseNodeId`. Las dos ramas entran en el análisis de speakers. No evalúa `WorldState`. El plan se arma una vez, antes de la primera línea, igual que en el paso 4. Evaluar la condición no rehace el plan.

El tronco lineal se corta en la condición, como ya se cortaba en un choice. El core sale de la intersección de los caminos.

---

## 6. Inspector y validación

El menú de nodos tiene Condition. La barra es verde agua. El título dice `Condition`. `trueNodeId` y `falseNodeId` tienen el botón Ids. La lista de condiciones usa el mismo `[SerializeReference]` que las opciones.

Un destino no vacío que no existe es error. Uno vacío no. Las condiciones null o con flag vacío avisan igual que en un choice.

El self-check del paso 8 ahora dice `11 passed`. El caso nuevo es un false destino roto y un true destino real.

---

## 7. Fixtures

Menú: `Tools → ARPG → Events → Add Conditional Dialogue Fixtures (Phase C Step 2)`.

NPC en z≈−92. Pads de flag en z≈−96. No regenera lo anterior.

| Objeto | Qué es |
|--------|--------|
| `NPC_C2_Flag` x=0 verde | Flag `lab_c2_branch`. Off: "The flag is off." On: "The flag is on." |
| `Pad_C2_FlagOn` x=0 | Pone el flag. |
| `Pad_C2_FlagOff` x=8 | Lo apaga. |
| `NPC_C2_Layout` x=14 azul | Línea común y luego villager_b o el jugador, según el mismo flag. |
| `NPC_C2_Missing` x=28 naranja | La rama true apunta a `missing`. |
| `NPC_C2_Cycle` x=42 blanco | 40 condiciones en anillo. Manual. |

Todos bloquean el control mientras hablan. Manual. E cierra la línea. Esc cancela.

---

## 8. QA

Jugada el 2026-11-11.

**A.** `lab_c2_flag`: primero `passed=False next='c_no'`, un plan de 1 speaker, `Session completed`. Después de `SetFlag 'lab_c2_branch' = True`, `passed=True next='c_yes'` y otra vez `Session completed`. Sin menú.

**B.** `lab_c2_layout`: un solo plan, `3 speaker(s). core=1 branchRegions=1 [villager_b@0, Ax@0]`. Los dos de rama comparten región, que es el slot exclusivo. La condición salió después del plan: `passed=False next='l_you'`. No hubo un segundo plan. El aviso de clamp es el del paso 4. En esta pasada no se habló la rama de villager_b; el plan ya la tenía reservada.

**C.** `lab_c2_missing`: `passed=True next='missing'` y `Session failed (missing-node)`. Se pudo volver a interactuar, así que el lock se soltó. El `Interactive start` sale después del fallo porque el log del servicio se escribe al volver de `Start`.

**D.** `lab_c2_cycle`: condiciones `c00`…`c31`, una vez `Frame fuse yielded after 32 transitions`, luego `c32`…`c39`, y `Session failed (automatic-cycle) node='c00' revisited without a player wait`. Después se pudieron pisar los pads.

Los pads de flag entran dos veces por el segundo collider. El flag queda en el valor correcto.

---

## 9. Compilación

El Play Mode del usuario compiló estos scripts. El log sale de `DialogueSession.Run` y de `AdmitNode`.

---

## 10. Límites

- No hay grupos OR ni un lenguaje de expresiones.
- El layout no sabe qué flag está activo. Reserva las dos ramas.
- Una condición que vuelve a sí misma después de una línea confirmada es un loop válido. Una que vuelve sin espera falla.
- No hay consecuencias de diálogo.
- El self-check del paso 8 ahora cuenta 11 casos. Los 10 anteriores siguen.

No empecé el paso 3.
