# Sprint 05 — Phase C Step 4 Report

**Fecha:** 2026-11-11  
**Estado:** A y B jugadas. Las dos pasaron.  
**Siguiente:** Parar. El alcance autorizado de Phase C puede cerrarse.

**Recomendación:** sin P0 ni P1. A y B pasaron. El alcance autorizado de Phase C queda cerrado. Skip y seen/cooldown siguen fuera de este cierre.

---

## 1. Integración

La cadena real, leída en código, es esta:

`EventRunner.RunSequence` bombea cada `EventAction` con `MoveNext` y hace `yield return step.Current`. No hay un segundo runner.

`StartDialogueAction` llama `TryStartInteractive`, guarda `LastInteractiveSession` y, con `waitUntilFinished` (el default), espera esa instancia. No toma `GameplayLock`. El lock lo toma la sesión solo si el asset o el override lo piden. Los pads del paso 1 usan `seizesPlayerControl = false`, así que el único dueño es la sesión.

`DialogueSession` corre la choice. Solo la opción confirmada entra a `RunConsequences`. Ahí se crea `new EventContext(null, token)`. `Runner` queda null a propósito. No se llama `EventScheduler.TryBegin`.

Si todas las acciones terminan sin stop ni cancel, entonces se lee `nextNodeId`. Un `DialogueConditionNode` llama `ConditionsAllow` en ese momento. No hay caché. `DialogueGraph.VisitCondition` camina los dos destinos y no llama `Evaluate` ni escribe `WorldState`.

`Finish` pone `Completed` solo si el outcome sigue en `None`. `StartDialogueAction` ve ese outcome. Si no es `Completed`, `RequestStop`. `EventRunner` entonces no corre lo que sigue y no marca Once ni `WorldState` completed. `CleanupAfterRun` suelta el lock del runner si lo había tomado.

Desvíos del plan maestro, aceptados por los pasos ya autorizados:

- Una consecuencia no encola un `EventRunner` para más tarde. `StartDialogueAction` dentro de una choice se rechaza.
- Skip y `dialogue.{id}.seen` no existen. El plan los ponía en Phase C. Este paso prohíbe añadirlos.

---

## 2. Los diez puntos

| #   | Pregunta                                      | Evidencia                                                                                                           |
| --- | --------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| 1   | El runner espera su sesión                    | **Jugada**, paso 1. `after dialogue` sale después de `Session completed`.                                           |
| 2   | Solo la opción confirmada corre               | **Jugada**, paso 3. Leave it no loguea `Consequence`. Set it loguea un `SetFlag`.                                   |
| 3   | Van en orden                                  | **Jugada**, paso 3. `Wait (2s)` y después `SetFlag 'lab_c3_wait'`.                                                  |
| 4   | El destino espera a la acción async           | **Jugada**, paso 3. El cancel cae en `RunConsequences:1015` y el `SetFlag` siguiente no sale.                       |
| 5   | El condition ve el flag nuevo                 | **Jugada**, paso 3. `SetFlag 'lab_c3_seen'` y en la misma sesión `c3_check passed=True next='c3_on'`. Un solo plan. |
| 6   | `Completed` deja seguir al runner             | **Jugada**, paso 1, con una línea. **No jugada** unida a una choice.                                                |
| 7   | `Cancelled` / `Failed` no completan el evento | **Jugada**, paso 1. Esc y el pad naranja de entry rota.                                                             |
| 8   | No hay consecuencia ni evento duplicado       | **Jugada**, paso 3. Dos **Mark** seguidos, uno por confirm. Sin `automatic-cycle`.                                  |
| 9   | No se abre otra sesión                        | **Jugada**. Un `Interactive start` por charla en los logs de los pasos 1–3.                                         |
| 10  | El lock no se queda                           | **Jugada**. Después de completar, cancelar el menú o fallar, se puede hablar o pisar el pad otra vez.               |

La unión 6+5 en un solo `EventRunner` (choice, flag, condition, acción siguiente, evento completado) no tiene fixture y no se jugó. Cada costura sí. No hay una rama nueva entre ellas que tire el outcome.

---

## 3. Cancel e interrupt

El bombeo no entrega el `IEnumerator` de la acción a Unity. Llama `MoveNext` y cede `step.Current`. Las acciones actuales ceden `null` o terminan. Ninguna hace `yield return` de otro enumerador.

Por eso Esc se ve al frame siguiente, antes del próximo `MoveNext`. El Wait del paso 3 lo demuestra: cancel desde `RunConsequences`, sin el `SetFlag` de después. La acción abandonada no se vuelve a mover. `WaitAction.Cancel` está vacío. El token ya quedó cancelado y no hay otro `MoveNext`.

`Cancel` del runner, el interrupt y el destroy usan `stopRoutine: true`. Eso llama `AbortConsequences` y `action.Cancel` antes de `StopCoroutine` sobre `Run`. Esa corrutina es la única que mueve la acción. Pararla no deja la acción avanzando.

| Caso                                        | Resultado                                                                                                                                                                                                                                                            |
| ------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Esc con el menú abierto                     | **Jugada**, paso 3. `PlayChoice:922`. Sin `Consequence`. Luego arrancó otro NPC.                                                                                                                                                                                     |
| Esc durante el Wait                         | **Jugada**, paso 3. `RunConsequences:1015`. Sin `SetFlag 'lab_c3_wait'`. Outcome `Cancelled`.                                                                                                                                                                        |
| Runner cancelado mientras espera el diálogo | **Código.** `StartDialogueAction.Cancel` llama `session.Cancel("event-cancel")` antes de `StopCoroutine`. El runner no marca Once. No se volvió a pisar un pad con el evento cancelado desde fuera.                                                                  |
| Interrupt a mitad de una consecuencia       | **Código.** `InterruptInteractive` llama `Cancel` con stop de corrutina. La reserva del paso 9 sigue igual. No se jugó con un Wait en curso.                                                                                                                         |
| Queue + Interrupt                           | **Jugada**, paso 9. Este paso no lo tocó.                                                                                                                                                                                                                            |
| Interrupt que no arranca                    | **Jugada**, paso 9. El slot se suelta y la cola sigue.                                                                                                                                                                                                               |
| Diálogo que falla al entrar                 | **Jugada**, paso 1. `null-entry`, `RequestStop`, sin la acción siguiente.                                                                                                                                                                                            |
| `RequestStop` dentro de una consecuencia    | **Código.** El camino existe (`consequence-stop`, sin destino). Ninguna acción admitida llama `RequestStop`. `StartDialogueAction` se rechaza antes de `Execute`.                                                                                                    |
| Excepción en `Execute` o `MoveNext`         | **Código.** Se atrapa, `Failed (consequence-exception)`, `Finish` suelta UI y lock. No se forzó una excepción.                                                                                                                                                       |
| Runner y sesión con lock a la vez           | **Código.** Dueños distintos. `GameplayLock` cuenta por dueño. La sesión suelta el suyo en `Finish`. El runner suelta el suyo en `CleanupAfterRun` o en `Cancel`. Los fixtures de diálogo usan `seizesPlayerControl = false`. No se jugó el caso de los dos en true. |

`waitUntilFinished = false` deja la sesión viva y un cancel posterior del runner no la cancela. El default es true. Los fixtures lo usan en true.

---

## 4. WorldState

`SetFlagAction`, `ClearFlagAction` y `MarkEventCompletedAction` escriben `WorldState` y no necesitan runner. Con runner null no hay log `[Event:…]`. El rastro es `[Dialogue] Consequence`.

`SaveManager` guarda `WorldState.SnapshotFlags()` y `SnapshotCompletedEvents()`. Un flag puesto por una choice entra en ese diccionario. No hay persistencia nueva. F5/F9 de un flag puesto por diálogo no se jugó en Phase C. El camino de save ya existía.

Once de un evento: solo `MarkCompletedSuccessfully`, y solo si no hubo cancel ni `RequestStop`. **Jugada** en el paso 1.

Once de un trigger narrativo: `ConsumeOnce` solo si `Outcome == Completed`. Cancel y Failed no consumen. Eso es Phase A / paso 9, no se rompió en el código del paso 3.

No hay rollback. El `SetFlag` que ya corrió se queda. El que no alcanzó a correr, no corre. **Jugada** en el Wait cancelado.

El análisis de layout no evalúa condiciones. **Jugada** en el paso 2: el plan lista los dos speakers de la rama antes de saber cuál se dice.

Repetir la charla no dispara consecuencias sola. Hace falta otro confirm. **Jugada** en el loop blanco.

---

## 5. Puertas

`OpenDoorAction`, `CloseDoorAction`, `SetDoorStateAction` y `UnlockDoorAction` usan `ctx.Runner` solo como contexto de un `LogWarning` si falta la puerta. Unity acepta ese contexto null. El movimiento mira `ctx.IsCancelled` y `yield return null`. No piden runner, asset de evento ni `GameplayLock`.

Con el contexto null de una choice son seguras. No hay defecto por tenerlas en la lista admitida.

No se jugaron los pads de puertas después de Phase C. Siguen en TestLab (menú de Door/Switch). Esta pasada no los pisó.

---

## 6. Diálogo

Phase C no cambió el solver, el filtro, el fusible de líneas automáticas, ni NearPlayer. Lo que se añadió al fusible es el set de conditions, ya jugado en el paso 2.

Lo que Phase C no volvió a pisar en esta auditoría, y que los reportes anteriores dan por jugado: ambient, interactivo, narrativo, Manual/Automatic, variantes Ax/Jyn, personaje congelado, filtro de choices, layout de ramas, NearPlayer, Queue / Ignore / Interrupt.

Limitaciones que se quedan:

- El warning de clamp en ramas que no caben. Paso 4 de Phase B.
- El segundo collider del jugador puede repetir un trigger narrativo. Los pads con `EventSourceTrigger` deduplican por raíz. No se cambia.
- NearPlayer no sigue al jugador cada frame.
- No hay grupos OR.

---

## 7. Inspector

El choice sigue en el inspector del paso 8. `DialogueConsequenceListDrawer` pinta la lista `[SerializeReference]`: alta por tipo, quitar, reordenar. El menú no ofrece `StartDialogueAction`. El alta guarda la ruta del property y usa `ApplyModifiedProperties`, así que Undo queda en el `SerializedObject`.

El validador marca consecuencia null y `StartDialogueAction`. `SetFlag` no es error. El self-check ahora cuenta **12**. Nadie pegó el diálogo de "12 passed" después de ese cambio.

No se abrió un asset viejo para mirar la lista vacía, ni se probó Undo a mano. El runtime trata la lista null como vacía.

---

## 8. Defectos

No hay P0. No hay P1.

**P2, ya aceptados.** No se arreglan aquí.

- Clamp de ramas.
- Segundo collider en triggers narrativos.
- NearPlayer quieto.
- Sin grupos OR.
- `UnityEventAction` puede llamar un diálogo o un runner. El validador no lo ve.
- `waitUntilFinished = false` no cancela esa sesión si el evento se cancela después.

**P3**

- El self-check de 12 no se volvió a correr.
- Undo y reordenar consecuencias no se jugaron.
- `SetFlag` desde una choice no imprime `[Event:nombre]` porque el runner es null.

No se propone un parche. El plan maestro aún pide skip, seen/cooldown y la regresión de puertas. Eso es alcance no hecho, no un bug de lo ya construido.

---

## 9. QA que ya está

- Phase A, incluido narrativo e Interrupt.
- Phase B pasos 1–9, incluido el arreglo de Queue + Interrupt.
- Phase C paso 1: espera, `Completed` sigue, `Cancelled` y `Failed` hacen `RequestStop`, sin acción posterior, lock suelto.
- Phase C paso 2: flag, plan único, `missing-node`, fusible y `automatic-cycle` en conditions.
- Phase C paso 3: Leave it, Set it una vez, condition en la misma sesión, Esc en el menú, Esc durante el Wait, dos Mark y Done.

---

## 10. Fixture y QA pendiente

Menú: **Tools → ARPG → Events → Add Final Integration Fixture (Phase C Step 4)**.

Crea solo:

- `Assets/Game/Dialogue/TestLab/Lab_C4_Chain.asset` (`dialogueId` `lab_c4_chain`)
- `Event_C4_Chain` en z≈-112

No destruye otros pads ni otros assets. El menú reescribe ese asset y ese objeto si se corre otra vez.

El runner: `eventId` `lab_c4_chain`, Repeat, `seizesPlayerControl = false`, `persistOnce = false`, `debugLog = true`. Acciones: `StartDialogueAction` (`waitUntilFinished`, `allowCancel`) y `DebugLogAction` "C4 after chain".

El diálogo: choice `c4_ask`. **Leave it.** va a una línea y termina, sin consecuencia. **Set the flag.** corre `SetFlag lab_c4_chain_flag = true` y sigue a `c4_check`. True dice "C4 condition passed." False dice "C4 condition failed."

Salir de Play Mode, correr el menú, guardar la escena. F6 limpia todos los flags, incluido este.

### A. Completar con B

1. F6.
2. Pisá `Event_C4_Chain`.
3. Elegí **Set the flag.** y confirmá con E.
4. Leé "C4 condition passed." y confirmá con E.

Consola, en este orden, una vez cada una:

- `[Event:Event_C4_Chain] Start (2 actions, playerControl=False, eventId='lab_c4_chain').`
- `[Dialogue] Interactive start 'lab_c4_chain'.`
- Un solo `Conversation layout plan`.
- `[Dialogue] Consequence [0] SetFlag 'lab_c4_chain_flag' = True option='b' asset='lab_c4_chain'.`
- `[Dialogue] Condition 'c4_check' passed=True next='c4_yes' asset='lab_c4_chain'.`
- `[Dialogue] Session completed asset='lab_c4_chain'.`
- `[Event:Event_C4_Chain] Dialogue outcome=Completed asset='lab_c4_chain'.`
- `[EventLog] C4 after chain`
- `[Event:Event_C4_Chain] Completed.`

No debe haber un segundo `Interactive start`, un segundo `SetFlag`, ni un segundo plan.

### B. Esc en el menú

1. F6.
2. Pisá el mismo pad.
3. Con el menú abierto, Esc. No confirmes.

Esperado:

- `[Dialogue] Session cancelled (user-cancel) asset='lab_c4_chain'.`
- `[Event:Event_C4_Chain] Dialogue outcome=Cancelled asset='lab_c4_chain'.`
- `[Event:Event_C4_Chain] Stopped at action [0]`
- Sin línea `Consequence`.
- Sin `[EventLog] C4 after chain`.
- Sin `[Event:Event_C4_Chain] Completed.`

Salí del pad y volvé a pisarlo. Tiene que poder arrancar otra vez.

Estas pasadas se jugaron el 2026-11-11. Play Mode compiló el fixture.

El log tiene tres arranques.

- Primero y tercero: menú `c4_ask`, `Session completed` sin `Consequence` ni `Condition`, luego `outcome=Completed`, `[EventLog] C4 after chain` y `Completed`. Es **Leave it.** El diálogo termina bien y el evento sigue. No es Esc.
- Segundo: un plan (`1 speaker, core=1`). Un `SetFlag 'lab_c4_chain_flag' = True option='b'`. En la misma sesión `Condition 'c4_check' passed=True next='c4_yes'`. Luego `Session completed`, `outcome=Completed`, `C4 after chain` y `Completed`. Sin segundo plan, sin segundo `Interactive start` y sin segundo `SetFlag`. **A pasó.**

No hay `Session cancelled` en esa primera tanda. **B llegó en el log siguiente.**

Esc con el menú abierto (`PlayChoice` línea 922):

- `[Dialogue] Session cancelled (user-cancel) asset='lab_c4_chain'.`
- `[Event:Event_C4_Chain] Dialogue outcome=Cancelled asset='lab_c4_chain'.`
- `[Event:Event_C4_Chain] Stopped at action [0] — dialogue Cancelled asset='lab_c4_chain'. Completed NOT marked.`

No hay `Consequence`, ni `[EventLog] C4 after chain`, ni `Completed`. El runner terminó la secuencia sin marcar el evento, así que el pad Repeat puede arrancar otra vez. **B pasó.**

---

## 11. Compilación

Play Mode compiló el builder y corrió `Event_C4_Chain`. El log sale de `EventRunner`, `StartDialogueAction` y `DialogueSession`.

---

## 12. Cierre

**El alcance autorizado de Phase C queda cerrado.** A y B pasaron. No hay P0 ni P1.

Puertas, skip y seen/cooldown no entran en este cierre.

Limitaciones que siguen: clamp de ramas, segundo collider en triggers narrativos, NearPlayer quieto, sin grupos OR, `UnityEvent` en una choice puede llamar lo que el autor le ponga.
