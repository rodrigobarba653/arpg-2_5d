# Sprint 05 — Phase C Step 3 Report

**Fecha:** 2026-11-11  
**Estado:** QA jugada. Los cinco casos pasaron.  
**Siguiente:** Parar. No implementar skip, seen/cooldown ni el paso 4.

---

## 1. Archivos

Creados:

- `Assets/Game/Scripts/Editor/Dialogue/DialogueConsequenceListDrawer.cs`
- Este reporte.

Modificados:

- `Assets/Game/Scripts/Dialogue/Data/DialogueChoiceOption.cs`
- `Assets/Game/Scripts/Dialogue/Runtime/DialogueSession.cs`
- `Assets/Game/Scripts/Editor/Dialogue/DialogueAssetValidator.cs`
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs`

No se tocó `EventRunner`, `StartDialogueAction`, el solver de layout, el filtro de opciones, el fusible, ni la colocación NearPlayer. No se reescribieron assets de Phase A, Phase B ni de los pasos 1 y 2.

---

## 2. Datos

`DialogueChoiceOption.consequences` es `[SerializeReference] List<EventAction>`.

Los campos anteriores siguen igual: `optionId`, `text`, `textVariants`, `nextNodeId`, `requiredCharacterId`, `availability`.

Lista null o vacía no hace nada. Un asset viejo sin consecuencias se confirma y sigue a `nextNodeId` como antes.

No hay interfaz nueva ni lenguaje de script. Se reutilizan las `EventAction` que ya existen.

---

## 3. Cómo se ejecutan

Solo la opción confirmada. Resaltar, filtrar, mostrar o saltar una opción no corre nada.

Al confirmar:

1. Se congela esa opción.
2. Se esconde el menú y se suprime el Confirm hasta soltar la tecla, para que el mismo press no dispare otra vez.
3. Se recorre `consequences` en orden. Cada `Execute` se bombea hasta que su `IEnumerator` termina. Si cede un frame, el destino espera.
4. Si todas terminan sin stop ni cancel, entonces se usa `nextNodeId`.

El destino no empieza antes. No hay segundo `EventRunner` ni `EventScheduler.TryBegin`.

El contexto es `new EventContext(null, token)`. `Runner` queda null a propósito: no se inventa un dueño ni se usa el evento que esté activo en la escena. `ctx.Log` no imprime, porque mira `Runner.debugLog`. Las acciones que ya comprueban `Runner != null` siguen escribiendo `WorldState`. El log de la charla es otro:

`[Dialogue] Consequence [i] {DisplayName} option='…' asset='…'.`

---

## 4. Cancelación y dueño

El token es un `CancellationTokenSource` de esa confirmación. `Cancel` de la sesión lo cancela y llama `EventAction.Cancel` de la acción activa, y luego `Finish` suelta burbujas, menú y el `GameplayLock` de la sesión.

Eso cubre Esc, un interrupt y un `event-cancel`. No convierte el resultado en `Completed`.

`RequestStop` de una acción tampoco sigue con las demás ni abre el destino. La sesión queda `Failed (consequence-stop)`. Lo ya ejecutado no se revierte, igual que en `EventRunner`.

Una excepción en `Execute` o en `MoveNext` es `Failed (consequence-exception)`. Tampoco abre el destino.

`Finish` solo escribe `Completed` si el outcome sigue en `None`. Un solo outcome terminal.

---

## 5. Acciones admitidas y rechazadas

Admitidas, porque no abren otra sesión ni otro runner:

- `SetFlagAction`, `ClearFlagAction`, `MarkEventCompletedAction`
- `DebugLogAction`
- `WaitAction`
- `OpenDoorAction`, `CloseDoorAction`, `SetDoorStateAction`, `UnlockDoorAction`
- `UnityEventAction`

Rechazada:

- `StartDialogueAction`. El inspector no la ofrece. Si está en el asset, el validador marca error y, en juego, la sesión falla con `unsupported-consequence` sin llamarla. Las consecuencias anteriores quedan. Las siguientes no corren. El destino no corre.

No se difiere un `EventRunner` hasta el final de la charla. El plan lo mencionaba para no anidar un seize. Aquí se rechaza esa configuración en vez de encolarla.

`UnityEventAction` está admitida, pero el callback no debe arrancar un diálogo ni un `EventRunner`. Eso no se puede ver desde el validador.

---

## 6. Orden

Highlight no escribe. Confirm de la opción A no corre las acciones de B.

Dentro de B: acción 0, se espera, acción 1, se espera, luego el nodo `nextNodeId`. Un `DialogueConditionNode` posterior lee el flag ya escrito, en la misma sesión. No se cachea el resultado. La sesión no escribe `WorldState` por su cuenta: lo hace `SetFlagAction`.

El plan de layout sigue armándose una vez al empezar, antes de la caja. Las consecuencias no son speakers y no piden otro plan.

---

## 7. Una vez por confirmación

No hay flag Once ni persistencia nueva. La opción congelada se ejecuta en ese confirm y el menú ya no está.

Si un loop válido vuelve al mismo choice y el jugador confirma otra vez, las consecuencias corren otra vez. El Confirm se suprime hasta soltar, y el choice siguiente espera a que se suelte, así que el mismo press no las corre dos veces.

Esperar una línea no borra `automaticVisited`. Confirmar el choice sí, como en el paso 6. Un loop Manual choice → línea (E) → choice no es `automatic-cycle`.

---

## 8. Fallo y cancel

- Todas bien: se sigue al destino. Si el destino está vacío, `Completed`.
- `RequestStop` o `StartDialogueAction` o excepción: `Failed`. Sin destino. Sin consecuencias posteriores.
- Esc o interrupt a mitad de una acción: `Cancelled`. La acción activa recibe `Cancel`. El resto no corre. El destino no corre.
- UI y lock de la sesión se sueltan en `Finish`.

---

## 9. Inspector y validador

El drawer del campo pinta una lista reordenable dentro del choice que ya dibuja el inspector del paso 8. Alta por menú de tipos, quitar, reordenar, editar campos. Undo sale de `SerializedObject`. `StartDialogueAction` no está en el menú.

El validador marca:

- entrada null
- `StartDialogueAction`

No hay validadores previos por acción (un `flagId` vacío tampoco es error en un `EventRunner`). No se añadieron.

El self-check del paso 8 ahora dice **12 passed** e incluye el caso de consecuencia null / `StartDialogueAction` / `SetFlag` permitido.

---

## 10. TestLab y QA

Menú: **Tools → ARPG → Events → Add Dialogue Consequence Fixtures (Phase C Step 3)**.

Solo destruye `NPC_C3_*` y crea `Lab_C3_*`. NPCs en z≈-104. Salir de Play Mode, correr el menú, guardar la escena. F6 limpia flags si hace falta repetir.

| NPC | x | Qué mirar |
|---|---|---|
| `NPC_C3_Flag` verde | 0 | A no escribe. B escribe `lab_c3_choice` una vez. |
| `NPC_C3_Then` azul | 14 | B escribe `lab_c3_seen` y el condition de la misma charla va a `c3_on`. |
| `NPC_C3_Wait` naranja | 28 | Wait 2s y después `lab_c3_wait`. Esc durante la espera cancela. |
| `NPC_C3_Loop` blanco | 42 | `Mark` loguea una vez por confirm. `Done` cierra. |

### A. Flag solo al confirmar B

Verde. No pulses E todavía.

1. Abre el menú. Baja a **Set it.** No pulses E. En consola no debe salir `Consequence` ni `lab_c3_choice`.
2. Sube a **Leave it.** E. Línea "Left the flag alone." E. `Session completed asset='lab_c3_flag'`. Sin línea `Consequence`.
3. Habla otra vez. **Set it.** E. Una sola línea  
   `[Dialogue] Consequence [0] SetFlag 'lab_c3_choice' = True option='b' asset='lab_c3_flag'.`  
   Línea "Wrote the flag." E. `Session completed`. No debe repetirse el SetFlag.

### B. Consequence y luego condition

Azul. **Set and test.** E.

Orden en la misma charla, sin un segundo `Interactive start` y sin un segundo `Conversation layout plan`:

1. El plan, una vez, al abrir.
2. `Consequence [0] SetFlag 'lab_c3_seen' = True`
3. `Condition 'c3_check' passed=True next='c3_on'`
4. Línea "The condition saw the flag."
5. `Session completed asset='lab_c3_then'`

**Skip.** no debe escribir el flag ni pasar por `c3_check`.

### C. Cancel antes de elegir

Verde, menú abierto, Esc.

`Session cancelled (user-cancel)`. Ningún `Consequence`. Se puede hablar de nuevo (el lock no se quedó).

### D. Cancel durante el Wait

Naranja. **Wait, then set.** E.

Debe salir `Consequence [0] Wait (2s)`. Durante esos 2s, Esc.

Esperado: `Session cancelled`. No sale `Consequence [1] SetFlag 'lab_c3_wait'`. No sale la línea "Wait finished." Se puede hablar de nuevo.

Si dejas pasar los 2s, sí sale el SetFlag y la línea. Eso es el camino sin cancel.

### E. Loop

Blanco. **Mark.** E. Deben salir, una vez cada uno:

- `Consequence [0] Log: c3 loop mark`
- `[EventLog] c3 loop mark`

Línea "Marked. E returns to the choice." E. Vuelve el menú. **Mark.** otra vez: el par de logs otra vez. Sin `automatic-cycle`. **Done.** termina en `Completed` sin consequence.

---

## 11. Compilación y QA

Play Mode compiló los scripts. El log del 2026-11-11 sale de `DialogueSession.Run`, `PlayChoice` y `RunConsequences`.

Jugado:

- Verde, primera charla: menú `c3_ask` visible=2 y `Session completed` sin ninguna línea `Consequence`. Es **Leave it.**
- Verde, segunda charla: un solo `Consequence [0] SetFlag 'lab_c3_choice' = True option='b'`, luego `Session completed`. Se pudo hablar otra vez, así que el lock se soltó.
- Azul: un solo plan (`1 speaker, core=1`). Después `SetFlag 'lab_c3_seen'`, y en la misma sesión `Condition 'c3_check' passed=True next='c3_on'`. Sin segundo `Interactive start` ni segundo plan.
- Naranja, espera completa: `Wait (2s)` y después `SetFlag 'lab_c3_wait'`, luego `Session completed`.
- Blanco, primera pasada: un mark, el menú vuelve, **Done** cierra. Sin `automatic-cycle`.

Segundo log:

- Verde: menú `c3_ask` y `Session cancelled (user-cancel)` desde `PlayChoice`, sin `Consequence`. Después arranca el naranja, así que el lock se soltó.
- Naranja de ese log: el cancel sale de `PlayChoice` línea 922, con el menú abierto. No hay línea `Wait (2s)`. Es Esc antes de confirmar, no durante la espera.
- Blanco: dos marks seguidos. Cada uno trae un `Consequence [0] Log: c3 loop mark` y un `[EventLog] c3 loop mark`. El menú vuelve entre los dos. **Done** cierra con `Session completed`. Sin `automatic-cycle`.

Tercer log, naranja: sale `Consequence [0] Wait (2s)` y el cancel viene de `RunConsequences` línea 1015. No aparece `Consequence [1] SetFlag 'lab_c3_wait'`. La sesión queda `Cancelled`.

---

## 12. Límites

- `Runner` del contexto es null. No hay nombre de evento en el log de `SetFlag`; el rastro es la línea `[Dialogue] Consequence`.
- `StartDialogueAction` se rechaza. No hay cola para correr un evento al terminar la charla.
- Un `UnityEvent` puede llamar lo que el autor le ponga. Si llama a un diálogo o a un runner, no está cubierto.
- Acciones de puerta sirven si la referencia está puesta. Este paso no las prueba en el lab.
- Los pads de flags anteriores pueden disparar dos veces por el segundo collider. Estos fixtures no usan pads.
