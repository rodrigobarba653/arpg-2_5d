# Sprint 05 — Phase B Step 5 Report

**Fecha:** 2026-11-10  
**Estado:** Consola de TestLab revisada. Filtro, fallback, Failed y el id congelado cuadran. El NPC morado permite Q y caminar porque su lock está apagado.  
**Siguiente:** Parar. No empezar Step 6.

La base sigue siendo `SPRINT_05_PHASE_B_PLAN.md` y los reportes de Steps 1 a 4. El filtro no toca el layout de Step 4. El aviso de clamp de las cajas de rama queda como está.

---

## Archivos

Creados:

- `Dialogue/Runtime/DialogueChoiceAvailability.cs`
- `Events/SPRINT_05_PHASE_B_STEP_5_REPORT.md` (este archivo)

Modificados:

- `Dialogue/Data/DialogueChoiceOption.cs` — los campos de Step 3 ya se evalúan
- `Dialogue/Runtime/DialogueSession.cs` — filtra al entrar al choice y resuelve la etiqueta
- `Editor/Events/EventSystemTestLabBuilder.cs` — menú nuevo, sin regenerar fixtures viejos

Sin cambios: `DialogueTextResolver`, `DialogueGraph`, el solver de layout, `GameplayLock`, `WorldState`, variantes de línea, outcomes, input del menú.

---

## Filtro

Al entrar en el choice, una sola vez por visita:

1. Se cuentan las opciones escritas que no son null.
2. Cada una pasa por `DialogueChoiceAvailability`.
3. Las que pasan forman la lista visible. El menú y el índice usan esa lista.
4. La etiqueta sale de `DialogueTextResolver.Resolve` con `SessionActiveCharacterId`.

`requiredCharacterId` vacío deja la opción para cualquiera. `ax` o `jyn` tienen que coincidir con el id congelado, sin importar mayúsculas. `active_player`, `player`, `party` y `hero` se resuelven a ese mismo id. No se vuelve a leer `Party.Active`.

`availability` vacío no pide nada a WorldState. Las entradas null se saltan, igual que en `EventRunner`. Las demás se evalúan con `EventCondition.Evaluate()` y tienen que ser todas verdaderas. Una falsa oculta la fila. No hay grupos OR.

Un texto de variante vacío se ignora y queda la etiqueta por defecto. Si la etiqueta resuelta también está vacía, la fila muestra el `optionId`.

Volver al mismo choice más tarde vuelve a evaluar. Mientras el menú está abierto, no se refiltra.

---

## Fila visible y opción escrita

`choiceScratch` guarda las opciones visibles, no una copia de la lista completa. Confirmar lee `choiceScratch[index].nextNodeId`. El índice de la fila no se usa contra la lista sin filtrar. Las filas ocultas no entran en la navegación.

---

## Sin filas visibles

| Caso | Resultado |
|------|-----------|
| Hay opciones escritas, todas ocultas, `fallbackNodeId` con texto | Sigue ese id. No abre menú. Si el nodo no existe: `Failed` (`missing-node`). |
| Hay opciones escritas, todas ocultas, fallback vacío | `choiceDestination` queda vacío y la sesión termina `Completed`. |
| No hay opciones escritas y no hay fallback | `Failed` (`empty-choice`). |
| No hay opciones escritas, pero el fallback apunta a un nodo | Sigue ese id. |

`Completed` sigue siendo el único outcome que consume Once. `Failed` suelta el lock y la UI, como en Step 2.

---

## Layout

`DialogueGraph` sigue recorriendo todas las opciones escritas, también las que el filtro ocultaría. Elegir, ocultar o caer al fallback no reconstruye el plan ni mueve las burbujas. El aviso de clamp de Step 4 no se tocó.

---

## Compilación

La sesión de Play Mode del 2026-11-10 ejecutó el filtro, los pads y el menú. El editor ya compiló este paso.

---

## QA en TestLab

1. Sal de Play Mode. Abre `EventSystem_TestLab`.
2. **Tools → ARPG → Events → Add Dialogue Condition Fixtures (Phase B Step 5)**. Guarda. No toca Phase A ni los NPC de Steps 1 a 4.
3. Play. Los NPC están en **z ≈ −48**. Los pads de flags están en **z ≈ −52**. **E** habla. **Q** cambia de personaje solo fuera de un chatbox con lock, y dentro del NPC morado.

| x | Objeto | Qué comprobar |
|---|--------|----------------|
| 0 | `NPC_Cond_Labels` | Ax ve "Ax's road." Jyn ve "Jyn's road." "The plain road." y "The unmarked road." salen igual para los dos. Log: `visible=3 authored=3`. |
| 8 | `NPC_Cond_Who` | Ax ve "Anyone." y "Ax's answer." Jyn ve "Anyone." y "Jyn's answer." Log: `visible=2 authored=3`. La línea siguiente dice quién la oyó. |
| 16 | `NPC_Cond_Flags` | Al empezar solo "Always here." Pisa el pad rosa (`dialogue.test.step5.gate`). Vuelve a hablar: sale también "Needs the gate." Pisa el pad cian (`dialogue.test.step5.second`): sale "Needs both flags." El pad gris apaga las dos. "Needs both flags." no sale con una sola flag. |
| 24 | `NPC_Cond_Fallback` | No hay menú. La consola dice `visible=0 fallback='fb_line'` y luego se oye "Fallback line." |
| 32 | `NPC_Cond_Complete` | No hay menú. La consola dice `visible=0 completed`. No hay `Session failed`. Puedes moverte. |
| 40 | `NPC_Cond_Malformed` | `Session failed (empty-choice)`. El lock se suelta. |
| 48 | `NPC_Cond_BadFallback` | `visible=0 fallback='missing_condition_node'` y después `Session failed (missing-node)`. El lock se suelta. |
| 56 | `NPC_Cond_Frozen` | Empieza como Ax. El menú muestra "Ax reply." y "Ax only." Cambia a Jyn con Q mientras el menú sigue abierto: las filas no cambian. "Jyn only." no aparece. Esc cierra. |

Cada charla imprime `Conversation layout plan built` una sola vez. Abrir el menú o confirmar no imprime otro.

Los NPC con lock siguen sin aceptar Q mientras la caja está abierta. El morado es el único con el lock apagado, para poder cambiar de personaje durante la sesión.

### Consola del 2026-11-10

| Asset | Log | Lectura |
|-------|-----|---------|
| `lab_cond_labels` | Dos veces `visible=3 authored=3` | Las tres filas se muestran. El texto de Ax y de Jyn no sale en la consola. |
| `lab_cond_who` | Una vez `visible=2 authored=3` | Una fila de personaje quedó oculta. |
| `lab_cond_flags` | Tras el pad rosa, `visible=2 authored=3` | "Needs the gate." ya entra. "Needs both flags." sigue oculta con una sola flag. |
| `lab_cond_fallback` | `visible=0 fallback='fb_line'` | No hay menú ni `Session failed`. |
| `lab_cond_complete` | Dos veces `visible=0 completed` | Termina sin menú y se puede volver a hablar. |
| `lab_cond_malformed` | Dos veces `empty-choice` | `Failed`. Se puede volver a hablar, así que el lock se soltó. |
| `lab_cond_bad_fallback` | `fallback='missing_condition_node'` y `missing-node` | `Failed`. El siguiente NPC pudo empezar. |
| `lab_cond_frozen` | `visible=2 authored=3` en cada reapertura | La tercera fila no entra. Cambiar de personaje no cambia ese conteo. |

Cada charla armó el plan una sola vez. El segundo plan de `lab_cond_frozen` es otra conversación, después del cancel.

La primera sesión del morado empezó como Ax. Q pasó a Jyn antes del menú y la línea del jugador siguió mostrando a Ax. La segunda sesión empezó ya siendo Jyn, así que el menú dice "Jyn reply." y "Jyn only." Q de vuelta a Ax no subió el conteo a 3.

En ese NPC se puede caminar y usar Q porque `interactiveLockGameplay` está en falso. Los demás NPC de este paso lo tienen en verdadero.

Abrir el menú de estado durante la línea canceló la charla (`user-cancel` desde `PlayLine`). El aviso de `MenuManager` es ese menú de estado.

Animator y `DontDestroyOnLoad` son el arranque de siempre. Los pads rosa y cian se disparan dos veces por el segundo collider del jugador. No cambia la flag.

No quedó en el log una segunda charla de flags con las dos flags encendidas, ni otra después del pad gris.

---

## Límites

El grafo reserva sitio para hablantes que están en una opción oculta. Esos slots no se reasignan al filtrar. En estos fixtures las ramas vuelven al mismo aldeano, así que no aparece un hablante extra.

Una lista de condiciones hecha solo de entradas null cuenta como vacía y la opción se muestra. Es la misma regla que `EventRunner`.

Las flags de prueba viven en el `WorldState` de la sesión de Play. F6 las borra junto con el resto. No se escriben solas al disco.

Step 6 no está empezado.
