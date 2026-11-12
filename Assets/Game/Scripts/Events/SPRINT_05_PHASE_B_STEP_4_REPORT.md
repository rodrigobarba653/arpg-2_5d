# Sprint 05 — Phase B Step 4 Report

**Fecha:** 2026-11-10  
**Estado:** Consola de TestLab revisada. La clasificación, el plan único por charla y el loop cuadran. El aviso de clamp salió en los cuatro NPC con rama.  
**Siguiente:** Parar. No empezar Step 5.

La base sigue siendo `SPRINT_05_PHASE_B_PLAN.md` y los reportes de Steps 1 a 3. `BuildConversationPlan` no cambió su matemática. El menú de Step 3 tampoco.

---

## Archivos

Creados:

- `Dialogue/Runtime/DialogueGraph.cs`
- `Events/SPRINT_05_PHASE_B_STEP_4_REPORT.md` (este archivo)

Modificados:

- `Dialogue/Runtime/DialogueSession.cs` — arma núcleo y ramas antes de la primera línea
- `Dialogue/Runtime/DialogueLayoutSolver.cs` — offsets de rama y rebuild solo del núcleo
- `Dialogue/Runtime/ConversationLayoutPlan.cs` — `isCore` en el slot
- `Editor/Events/EventSystemTestLabBuilder.cs` — menú nuevo, sin regenerar fixtures viejos

Sin cambios: tamaño de chatbox, retratos, tipografía, `GameplayLock`, variantes, outcomes, filtros de opciones, `NearPlayerChatbox`.

---

## Recorrido y núcleo

`DialogueGraph.Analyze` parte del entry. Una línea sigue `nextNodeId`. Un choice sigue cada opción escrita y `fallbackNodeId`. Un end, un next vacío o un id repetido en el mismo camino cierran ese camino. El conjunto visitado es del camino, no del grafo entero, así que un loop escrito a mano termina y las ramas que se vuelven a juntar se cuentan las dos.

`active_player` se resuelve con el id congelado de la sesión.

| Conjunto | Qué es |
|----------|--------|
| Reachable | Habló en algún nodo alcanzable |
| Core | Está en todos los caminos simples |
| Branch | Alcanzable y no es core |
| Displayed | Ya dijo una línea en esta partida; la burbuja se crea entonces y se queda |

Si la intersección sale vacía, el núcleo es el tronco anterior al primer choice. Si ese tronco también está vacío, el camino con más hablantes se pasa una vez a `BuildConversationPlan` y esos slots quedan fijos. No se asume que los dos primeros del BFS sean el par principal.

Un diálogo sin choices tiene un solo camino: todos son core y entran a `BuildConversationPlan` igual que en Phase A. Uno solo queda arriba. Dos quedan izquierda y derecha según la X. Tres o más usan el abanico.

---

## Regiones de rama

Dos hablantes de rama comparten región solo si ningún camino simple los trae juntos. El índice es el menor que no esté usado por alguien que sí coincida con ellos, en el orden en que se descubrieron.

El índice arma el offset inicial: arriba, lift `(índice + 1) * (lift + gap)`, y un sesgo horizontal `0, +banda, -banda, +2 bandas…`. Cada hablante guarda el suyo. Un pase por camino puede subir solo ese lift o ese sesgo hasta que no choque con las cajas de ese camino, o hasta que el área segura no dé más. El núcleo no se escribe en ese pase. Si después del clamp siguen chocando, se loguea una vez y la conversación sigue:

```text
[Dialogue] Branch chatbox clamped; not all regions fit in the safe area.
```

Elegir una opción no reconstruye el plan. La burbuja de la rama sale en el offset que ya tenía. `ShouldRebuildConversationPlan` mira solo los slots `isCore`. Un hablante de rama que cruce en X o se salga de pantalla no baraja al núcleo. Si un ancla del núcleo se sale por `planRebuildDeltaRef`, o el par del núcleo cambia de orden en X más que esa distancia, se corre el mismo algoritmo otra vez. Eso es la política de Phase A, no un salto por la choice.

Si alguien habla y no estaba en el grafo, recibe la región `max + 1`. El núcleo se queda.

Un hijo del NPC con el nombre del speaker id (`villager_b`, `villager_c`) es el ancla de ese hablante. Si no hay hijo, sigue valiendo el ancla del NPC, como en Phase A.

---

## Tope de 1024 caminos

Si el conteo pasa de 1024, se deja la intersección parcial. El núcleo pasa a ser el tronco anterior al primer choice. Cada otro hablante alcanzable recibe su propia región, sin compartir. Una sola advertencia:

```text
[Dialogue] Path enumeration stopped at 1024. Core is the trunk before the first choice; other speakers get unique regions.
```

No es un límite de lo larga que puede ser la charla. No hay guarda de ciclos automáticos ni fuse de 32 nodos.

---

## Compilación

La sesión de Play Mode del 2026-11-10 ejecutó `DialogueGraph`, el log `core=` y el menú de choices. El editor ya compiló este paso.

---

## QA en TestLab

1. Sal de Play Mode. Abre `EventSystem_TestLab`.
2. **Tools → ARPG → Events → Add Dialogue Branch Layout Fixtures (Phase B Step 4)**. Guarda. No toca Phase A ni los NPC de Steps 1 a 3.
3. Play. Están en **z ≈ −44**. **E** habla. En la consola, al empezar, busca `core=` y `branchRegions=`.

| x | Objeto | Qué mirar en el log | En pantalla |
|---|--------|---------------------|-------------|
| 0 | `NPC_Branch_OneExclusive` | `core=2`, `villager_b@0` | El aldeano y tú no cambian de lado al traer al scout. El scout sale a la izquierda del grupo. |
| 8 | `NPC_Branch_Exclusive` | `villager_b@0, villager_c@0` | Scout y vigía no salen juntos. Cada uno usa el mismo índice, desde su propia marca. |
| 16 | `NPC_Branch_Together` | `villager_b@0, villager_c@1` | "Both" los muestra a la vez, en dos sitios distintos, sin tapar al núcleo. |
| 24 | `NPC_Branch_Converge` | `core=2`, B y C en `@0` | Las dos opciones llegan a "Same meeting". El aldeano y tú siguen en su sitio. |
| 32 | `NPC_Branch_Linear` | `core=3 branchRegions=0` | Tres hablantes, sin choice. Es el abanico de Phase A, no una rama. |
| 40 | `NPC_Branch_Loop` | El log sale una vez y el juego no se cuelga | "Again" vuelve a la pregunta. "Finish" cierra. |

Abrir o cerrar el menú no debe imprimir otro `Conversation layout plan built` en la misma charla. Un segundo `built` solo aparece si un ancla del núcleo se fue de la pantalla o cruzó al otro en X más que `planRebuildDeltaRef`. Eso es el rebuild de Phase A.

Los NPC de Steps 1 a 3 y los pads de outcomes siguen donde estaban. Q sigue bloqueado mientras la caja de un NPC con lock está abierta.

### Consola del 2026-11-10

Cada charla imprimió `Conversation layout plan built` una sola vez, antes del menú. Elegir una opción no imprimió otro plan. El siguiente `built` es el NPC siguiente.

| Asset | Log | Lectura |
|-------|-----|---------|
| `lab_narrative_auto` | `core=1 branchRegions=0` | Una línea, igual que Phase A. |
| `lab_branch_one` | `core=2 branchRegions=1 [villager_b@0]` | Aldeano y jugador en el núcleo. El scout es la única rama. |
| `lab_branch_exclusive` | `core=2 branchRegions=1 [villager_b@0, villager_c@0]` | Scout y vigía comparten la región 0. |
| `lab_branch_together` | `core=2 branchRegions=2 [villager_b@0, villager_c@1]` | Pueden salir juntos, así que usan regiones distintas. |
| `lab_branch_converge` | `core=2 branchRegions=1 [villager_b@0, villager_c@0]` | Las dos ramas vuelven al mismo par. |
| `lab_branch_linear` | `core=3 branchRegions=0` | Sin choice. Los tres entran al abanico de Phase A. |
| `lab_branch_loop` | `core=2 branchRegions=0`, y `p_choice` dos veces | "Again" reabrió el menú sin armar otro plan. |

Los cuatro assets con rama imprimieron, al empezar y una sola vez cada uno:

```text
[Dialogue] Branch chatbox clamped; not all regions fit in the safe area.
```

Eso reserva la caja completa de 760×340 para el hueco de la rama. Con el núcleo ya a los lados, esa caja no cabe en el área segura, se recorta y la charla sigue. El lineal y el loop no lo imprimen porque no tienen hablantes de rama. El aviso sale antes de elegir, también si esa rama luego no aparece.

La consola no muestra un salto de lado ni un segundo plan dentro de la misma charla. Tampoco muestra un cambio de personaje con el chatbox abierto.

Animator (`SpriteLightingController.OnValidate`) y `DontDestroyOnLoad` de `GameState`, `MenuManager` y `AudioManager` son el arranque de siempre.

---

## Límites

El pase de separación intenta ocho veces y luego deja la caja dentro del área segura. En un viewport chico puede quedar un solape; el warning lo dice una vez.

El nudge de cada frame contra el cuerpo del personaje sigue existiendo y no cambia el lado ni el offset guardado.

Step 5 no filtra opciones: el layout cuenta todas las que están escritas.
