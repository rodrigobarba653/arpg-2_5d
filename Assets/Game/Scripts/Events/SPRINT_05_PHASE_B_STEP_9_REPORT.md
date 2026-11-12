# Sprint 05 — Phase B Step 9 Report

**Fecha:** 2026-11-11  
**Estado:** P1 jugado en la carrera y en la cola. Se deja aquí, sin el confirm del rojo. Phase B no se declara cerrada.  
**Siguiente:** Parar. No empezar Phase C.

La auditoría no encontró P0. Encontró un P1: con A en curso, B en Queue y C en Interrupt, B arrancaba y C no. Esta pasada corrige solo eso.

El usuario lo jugó el 2026-11-11. Compiló en el editor. Una pasada pisó verde, dorado y rojo. Otra pisó solo verde y dorado. En ninguna confirmó la línea.

---

## 1. Causa

`NarrativeDialogueTrigger.TryActivate` cancelaba la charla activa y después llamaba a `StartDialogue`.

`Cancel` → `Finish` suelta la UI y el `GameplayLock`, marca la sesión terminada y entonces llama a `NotifyInteractiveFinished`. Ese aviso copia la lista de espera y la ejecuta en el acto, antes de volver al Interrupt.

B, que ya estaba en esa lista, veía `HasInteractive == false` y llamaba a `TryStartInteractive`. El hueco quedaba ocupado por B. C llegaba después y el servicio lo rechazaba (`Interactive already running`).

A salía `Cancelled`. Su Once no se consumía. El lock de A ya se había soltado. La charla que se perdía era la de C.

---

## 2. Archivos

- `Assets/Game/Scripts/Dialogue/Runtime/DialogueService.cs`
- `Assets/Game/Scripts/Dialogue/NPC/NarrativeDialogueTrigger.cs`
- `Assets/Game/Scripts/Editor/Events/EventSystemTestLabBuilder.cs`
- Este reporte.

No se tocó el solver de ramas, el filtro de choices, los guardias del paso 6, el placement del paso 7, ni assets de los pasos anteriores.

---

## 3. Corrección

`DialogueService.InterruptInteractive` reserva el hueco antes de cancelar.

Mientras la reserva está puesta, `HasInteractive` es true aunque la sesión ya se haya soltado. El callback de B ve el hueco ocupado y se vuelve a encolar. No llama a `TryStartInteractive`.

La reserva no bloquea al reemplazo: `allowReplacementStart` solo está activo alrededor de la acción que arranca C. Cualquier otro `TryStartInteractive` durante la reserva sigue rechazado.

Si C entra, la reserva se apaga en el `finally` y la sesión nueva mantiene el hueco. B sigue en la lista y arranca cuando C termina.

Si C no entra (asset null, u otra salida de `StartDialogue` sin sesión), el `finally` apaga la reserva y ejecuta la lista. B puede empezar. El servicio no se queda ocupado.

No hay espera de frames ni un scheduler nuevo. Ignore, Queue e Interrupt siguen en el mismo `TryActivate`. Cancelar por Esc, o por `CancelInteractive` suelto, no reserva: la cola sí puede entrar, que es lo que ya hacía.

---

## 4. Cancelación y lock

El outcome no cambió. `Cancel` escribe `Cancelled` solo si el outcome sigue en `None`. `Finish` no lo pisa.

Orden dentro del Interrupt:

1. Reserva.
2. `Finish` de A: outcome `Cancelled`, suelta burbujas, menú y `GameplayLock` si lo tenía, y avisa.
3. B se reencola. El callback de A no llama a `ConsumeOnce`.
4. C intenta empezar.
5. La reserva se apaga. Si no hay sesión, corre la cola.

`ConsumeOnce` sigue solo en `OnInteractiveSlotFree` cuando `Outcome == Completed`. Failed y Cancelled no marcan OncePerScene ni OncePersistent. El cooldown del que terminó sí se aplica, como antes.

---

## 5. Regresiones

| Caso | Resultado de esta pasada |
|------|----------------------------|
| A. Cola + Interrupt | Pasó la carrera al pisar los tres primeros. `lab_p1_a` cancelado, `lab_p1_c` arranca, y `lab_p1_b` no aparece. El rojo vuelve a entrar y reinicia C: es el segundo collider, no B. Falta confirmar C y ver el `Session completed` antes de B. |
| B. Interrupt sin cola | No se aisló. |
| C. Cola sin Interrupt | Pasó. `Session completed asset='lab_p1_a'.` y enseguida `Interactive start 'lab_p1_b'.` desde `OnInteractiveSlotFree`. Luego el rojo canceló esa B. |
| D. Interrupt que no arranca | Pasó. El gris canceló `lab_p1_c`, avisó `No DialogueAsset for 'Ax'`, soltó la reserva (`Interrupt did not start`) y entonces arrancó `lab_p1_b`. |
| E. Repetir | El rojo arrancó `lab_p1_c` dos veces seguidas, sin `already running` y sin perder a B. El segundo enter es el segundo collider del mismo pad: Interrupt se cancela a sí mismo y vuelve a empezar. No es la cola ganándole a C. |

El Once de A no se consume en A, B ni D, porque el outcome es `Cancelled`.

---

## 6. QA manual

Los pads de Phase A no sirven para esta secuencia: el Queue y el Interrupt no están en la misma fila, y A se acabaría o bloquearía el paso. El menú nuevo solo crea sus propios pads y tres assets nuevos.

1. Salir de Play Mode.
2. `Tools → ARPG → Events → Add Queue + Interrupt Fixture (Phase B Step 9)`.
3. Guardar la escena.
4. Play. Ir a z≈−76. De izquierda a derecha: verde A, dorado B, rojo C, gris sin asset.
5. No pulsar E, Enter ni Esc mientras A está en pantalla. Los tres primeros no bloquean el movimiento. B no se olvida al salir de su pad.

**A. Cola + Interrupt.** Pisar verde, luego dorado, luego rojo. No confirmar A.

Log, en este orden:

```
[Dialogue] Interactive start 'lab_p1_a'.
[Dialogue] Interrupt claimed the interactive slot.
[Dialogue] Session cancelled (narrative-interrupt) asset='lab_p1_a'.
[Dialogue] Interactive start 'lab_p1_c'.
```

No tiene que aparecer `Interactive start 'lab_p1_b'` entre medias, ni `already running`. La caja de A desaparece. El jugador se puede mover. Confirmar C (E o Enter). Entonces:

```
[Dialogue] Session completed asset='lab_p1_c'.
[Dialogue] Interactive start 'lab_p1_b'.
```

Confirmar B. `Session completed asset='lab_p1_b'.` Una sola vez cada asset.

**B. Solo Interrupt.** Pisar verde y luego el rojo, sin pisar el dorado. Mismo cancel de A y start de C. Al confirmar C no tiene que arrancar B.

**C. Solo cola.** Pisar verde y luego el dorado. No pisar el rojo. Confirmar A. Tiene que salir `Session completed asset='lab_p1_a'.` y después `Interactive start 'lab_p1_b'.`

**D. Interrupt que no arranca.** Pisar verde, dorado, y el gris (no el rojo).

```
[Dialogue] Interrupt claimed the interactive slot.
[Dialogue] Session cancelled (narrative-interrupt) asset='lab_p1_a'.
[NarrativeDialogueTrigger] No DialogueAsset for '…'
[Dialogue] Interrupt did not start. Queued listeners may continue.
[Dialogue] Interactive start 'lab_p1_b'.
```

Después de eso se puede pisar el rojo con el hueco libre y tiene que arrancar C. Si el gris dejara el servicio ocupado, C saldría `already running`.

**E. Repetir.** Esperar el cooldown (2.5 s) y salir del pad antes de volver a entrar. Cada pasada arranca una vez. No tiene que haber dos `Interactive start` del mismo id seguidos, ni un B que desaparezca.

El cooldown y el texto exacto del warning de asset null dependen del id del personaje activo. La frase estable es `No DialogueAsset for`.

---

## 7. Compilación

El Play Mode del usuario compiló estos scripts. El log sale de `DialogueService.InterruptInteractive` y de `NarrativeDialogueTrigger.TryActivate`.

---

## 8. Límites

Phase B no se declara cerrada, y aquí se para. La carrera y la cola sola ya se jugaron. No se pegó el tramo de pulsar E en el rojo con B todavía en espera. El mismo callback ya arrancó B al completar A y al fallar el pad gris, así que ese tramo no se persigue.

En esta pasada el gris se pisó con C todavía abierto, así que canceló C y soltó a B. El segundo disparo del gris canceló esa B y no dejó nada en cola. El warning suelto de `No DialogueAsset` que sigue es el mismo pad entrando ya con el hueco libre.

El aviso al cerrar la escena ya no tiene que crear otro `DialogueService`. `OnDestroy` suelta la cola antes de cancelar, y `Ensure()` no instancia un servicio nuevo mientras ese se destruye.

Siguen sin tocarse, a propósito:

- El clamp de ramas del paso 4.
- El segundo collider, que puede disparar un pad dos veces.
- NearPlayer no sigue al jugador. Lo pidió el paso 7.
- El comentario de `CancelDialogue` en `EnqueueAfterInteractive`. El método real sigue siendo `ClearDeferredAfterInteractive`.
- No hay fixture de rama exclusiva con `NearPlayerChatbox`.
- El reset del ciclo automático al confirmar un choice no tiene un NPC automático que vuelva a un id anterior.

La reserva es síncrona. No cubre un Interrupt que empiece desde dentro del callback de otro Interrupt. Los triggers de esta fase no hacen eso: el de la cola se reencola, no interrumpe.

No empecé Phase C.
