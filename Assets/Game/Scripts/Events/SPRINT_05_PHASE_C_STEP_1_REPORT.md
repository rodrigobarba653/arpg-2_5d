# Sprint 05 — Phase C Step 1 Report

**Fecha:** 2026-11-11  
**Estado:** QA de los tres casos, jugada. Paso 1 cerrado en este alcance.  
**Siguiente:** Parar. No implementar consecuencias ni los pasos 2–4.

---

## 1. Arquitectura del EventRunner

El runner ejecuta `actions` en orden. Cada `EventAction.Execute` es un `IEnumerator`. `WaitAction` ya espera cediendo cada frame y sale si `ctx.IsCancelled`. No hacía falta un scheduler nuevo.

Cancelar el runner (`Cancel`, `OnDisable`, `OnDestroy`) cancela el token, llama `currentAction.Cancel`, para la coroutine y suelta el `GameplayLock` del runner si él lo tomó. Un run cancelado no marca Once ni `WorldState`. Las acciones que ya corrieron no se revierten.

Antes de este paso una acción no podía fallar la secuencia. El runner marcaba completado si el token no se cancelaba, aunque el diálogo hubiera salido `Cancelled` o `Failed`. Se añadió `EventContext.RequestStop`. El runner corta el resto de la lista, no marca completado y sí suelta su lock.

`GameplayLock` es por dueño. El runner y la sesión son dueños distintos. `StartDialogueAction` no toma lock. La sesión lo toma solo si el asset tiene `lockGameplay`. Si el runner también tiene `seizesPlayerControl`, los dos lo sueltan por su lado. El fixture deja el runner en false, que es lo que pide el plan maestro.

`TryStartInteractive` rechaza si ya hay una sesión o si un Interrupt tiene el hueco reservado. Esta acción no encola y no interrumpe. Si la rechazan, el evento se detiene.

La sesión que arrancó esta acción se guarda en `DialogueService.LastInteractiveSession` antes de `Start`, porque un fallo inmediato termina dentro de `Start` y `InteractiveSession` ya queda en null. El wait compara esa instancia, no “el hueco libre”.

`DialogueCharacterAssetResolver.ResolveForActivePartyMember` elige el asset. No hay otro resolver.

El plan maestro, sección 8 y Phase C, también pide consecuencias por opción, seen/cooldown, skip y la regresión de puertas. Esta autorización los deja fuera. El snippet del plan solo cancela la sesión si cancelan el evento. Aquí, además, un diálogo `Cancelled` o `Failed` no cuenta como evento completado.

La reserva de Queue + Interrupt no se tocó. Tampoco el layout, los choices, las condiciones ni los guardias del paso 6.

---

## 2. Archivos

- `Assets/Game/Scripts/Events/Actions/StartDialogueAction.cs`
- `Assets/Game/Scripts/Events/EventContext.cs`
- `Assets/Game/Scripts/Events/EventRunner.cs`
- `Assets/Game/Scripts/Dialogue/Runtime/DialogueService.cs`
- `Assets/Game/Scripts/Editor/Events/EventRunnerEditor.cs`
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs`
- Este reporte.

---

## 3. Integración

`StartDialogueAction` resuelve el asset, llama a `TryStartInteractive` y se queda con `LastInteractiveSession`.

Con `waitUntilFinished` (el default):

1. Cede un frame mientras esa sesión no haya terminado.
2. Lee `Outcome` de esa sesión.
3. `Completed`: la secuencia sigue.
4. `Cancelled` o `Failed`: `RequestStop`. No corre lo que haya después. El evento no queda Once ni en `WorldState`.

Si cancelan el evento mientras espera, `Cancel` llama `session.Cancel("event-cancel")`. `Finish` suelta la UI y el lock de la sesión. El runner suelta el suyo si lo tenía.

Si `waitUntilFinished` es false, arranca y sigue. Un cancel posterior del runner ya no tiene esta acción como actual, así que no cancela ese diálogo. El default espera, para no dejar la sesión suelta.

Un diálogo ya activo hace que `TryStartInteractive` devuelva false. La acción para el evento. No usa la cola ni el Interrupt.

---

## 4. QA

Jugada el 2026-11-11. Un solo arranque por entrada. `after dialogue` solo salió después de `Session completed`. Esc paró el evento sin `after dialogue` y sin `Completed`. El naranja falló con `null-entry`, paró en la acción 0, y no escribió `after failed dialogue`. Se pudo volver a entrar después de completar, de cancelar y de fallar, así que el lock no se quedó puesto. `playerControl=False` en los tres.

El log `Interactive start 'lab_event_dialogue_fail'` sale después del `Session failed` porque `TryStartInteractive` lo escribe al volver de `Start`, cuando la sesión ya terminó. El evento igual leyó `Failed` y se detuvo.

Cola + Interrupt no se volvió a jugar en esta pasada. El código de esa reserva no se tocó.

---

## 5. Compilación

El Play Mode del usuario compiló estos scripts. El log sale de `StartDialogueAction.Execute` y de `EventRunner.RunSequence`.

---

## 6. Límites

- No hay consecuencias de opción, nodos condicionales, seen/cooldown ni skip.
- Un diálogo `Cancelled` o `Failed` corta el resto del evento. No sigue hacia un `SetFlag` posterior. Las acciones anteriores se quedan.
- No arranca un diálogo si otro interactivo está abierto. No lo interrumpe.
- `waitUntilFinished` false no cancela el diálogo si más tarde cancelan el runner.
- Si el runner y el asset toman lock a la vez, son dos dueños. La acción no añade un tercero.
- El segundo collider de los pads narrativos de Phase B sigue reiniciando un Interrupt. El trigger de estos eventos nuevos no.

No empecé el paso 2.
