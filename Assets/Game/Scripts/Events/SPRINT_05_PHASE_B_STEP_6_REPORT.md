# Sprint 05 — Phase B Step 6 Report

**Fecha:** 2026-11-10  
**Estado:** Cerrado. QA de los cuatro NPC en TestLab.  
**Siguiente:** Parar. No empezar Step 7.

La base sigue siendo `SPRINT_05_PHASE_B_PLAN.md` y los reportes de Steps 3 a 5. El layout y el filtro de opciones no cambian. El aviso de clamp de Step 4 queda igual.

---

## Archivos

Modificados:

- `Dialogue/Runtime/DialogueSession.cs` — ciclo automático y tope de 32 entradas por frame
- `Dialogue/NPC/NpcDialogueHost.cs` — hold opcional en 0 para los fixtures inmediatos
- `Editor/Events/EventSystemTestLabBuilder.cs` — menú nuevo, sin regenerar fixtures viejos

Creado:

- `Events/SPRINT_05_PHASE_B_STEP_6_REPORT.md` (este archivo)

Sin cambios: `DialogueGraph`, el solver, `GameplayLock`, el input, las condiciones de Step 5.

---

## Ciclo automático

Solo corre en `DialogueProgressionMode.Automatic`.

`AdmitNode` se llama antes de ejecutar cada línea o choice. Si el `nodeId` ya está en `automaticVisited`, la sesión hace `Fail("automatic-cycle", ...)`. El warning queda:

```text
[Dialogue] Session failed (automatic-cycle) asset='…' node='…' revisited without a choice confirmation.
```

El conjunto no es de toda la vida de la sesión. Confirmar una opción lo vacía. A partir de ahí los ids anteriores pueden volver a salir.

No lo vacían:

- El `yield return null` del typewriter o del hold.
- Confirmar para saltar el typewriter o el hold. Eso sigue siendo avance automático.
- El yield del tope de 32. Si se vaciara ahí, un ciclo largo seguiría para siempre.

Un fallback sin menú tampoco confirma. Si ese camino vuelve a un nodo ya visitado en el tramo automático, falla igual.

En Manual el conjunto no se llena. Repetir un nodo después de cada Confirm no es un ciclo automático.

Una charla automática de 500 líneas distintas pasa. `A → B → A` sin un Confirm de choice falla en la segunda entrada a A, aunque cada línea haya esperado su hold.

---

## Tope de 32

Una transición inmediata es una entrada a nodo que ocurre en el mismo `Time.frameCount` que las anteriores, sin que la corrutina haya devuelto el control al frame del jugador.

`AdmitNode` lleva la cuenta. Al llegar a 32, no entra en ese nodo, escribe una vez:

```text
[Dialogue] Frame fuse yielded after 32 transitions. asset='…'.
```

y `Run` hace `yield return null`. El frame siguiente pone la cuenta en 0 y sigue por el mismo nodo. No es `Failed`. No hay un máximo de 32 nodos por charla.

El propio tope no puede girar en el mismo frame: cuando se pasa, sale de `AdmitNode` y cede el frame antes de volver a preguntar.

Una línea con texto o con hold ya cede el frame, así que el tope no se nota. Se nota en líneas vacías con hold 0, que no esperan.

El ciclo y el tope se miran en ese orden: primero el cupo del frame, luego el id repetido. Ceder el frame no borra los ids. Un ciclo de más de 32 nodos falla cuando el id vuelve, no se reinicia.

---

## Cierre

`automatic-cycle` usa el `Fail` que ya existía. `Finish` suelta las burbujas, el menú y el `GameplayLock`, y avisa a `DialogueService`. El outcome es `Failed`, así que Once no se consume.

Un yield del tope deja el outcome en `None`. Al terminar de verdad, si nadie canceló ni falló, `Finish` pone `Completed` y, con `debugLog`, escribe `Session completed`.

Cancelar y `Completed` no cambian de significado.

---

## Compilación

No se abrió otra instancia de Unity. Sal de Play Mode y deja que el editor recompile. El menú nuevo aparece después.

---

## QA en TestLab

1. Sal de Play Mode. Abre `EventSystem_TestLab`.
2. **Tools → ARPG → Events → Add Dialogue Safety Fixtures (Phase B Step 6)**. Guarda. No toca Phase A ni los NPC de Steps 1 a 5.
3. Play. Están en **z ≈ −60**. **E** habla. **Esc** cancela.

| x | Objeto | Qué debe salir |
|---|--------|----------------|
| 0 | `NPC_Safety_AutoCycle` | Al momento, `Session failed (automatic-cycle)` en `a1`. No se traba. Puedes volver a hablar. El lock se suelta. |
| 8 | `NPC_Safety_ManualLoop` | "Say it again?" y "Still manual." se alternan con E. No hay `automatic-cycle`. Esc cierra. |
| 16 | `NPC_Safety_ChoiceLoop` | "Again." vuelve al menú. No hay `automatic-cycle`. "Leave." termina. Esc cierra. |
| 24 | `NPC_Safety_LongChain` | Una línea `Frame fuse yielded after 32 transitions` y después `Session completed`. No hay `Session failed`. Puedes moverte. |

Cada charla sigue armando el plan una sola vez. El guardia no lo reconstruye.

Los NPC de Steps 1 a 5 quedan donde estaban. El morado de Step 5 sigue pudiendo cambiar de personaje. Los demás siguen con lock.

### Resultado

| NPC | Consola |
|-----|---------|
| AutoCycle | `Session failed (automatic-cycle) asset='lab_safety_autocycle' node='a1'`. La siguiente charla arrancó, así que el lock se soltó. |
| ManualLoop | `Interactive start` y después `Session cancelled (user-cancel)`. No salió `automatic-cycle`. |
| ChoiceLoop | `Choice 'k_choice' visible=2 authored=2` tres veces y después `Session completed`. Un solo plan. |
| LongChain | Dos charlas. Cada una escribió una vez `Frame fuse yielded after 32 transitions. asset='lab_safety_long'.` y después `Session completed`. No hubo `Session failed`. El plan se armó una vez por charla. |

En la primera charla larga el tope salió antes de `Interactive start`: las 32 entradas ocurrieron dentro de `Start`, y ese log se escribe al volver. En la segunda el tope salió en un frame posterior, con `InvokeMoveNext`. Las dos son el mismo guardia.

---

## Límites

El hold en 0 solo lo piden los dos NPC automáticos de este paso. El resto sigue en 1.75 s. La sesión ya no cambia un 0 por 1.75; un valor negativo sí vuelve al default.

`Session completed` ahora se escribe en todas las charlas que terminan bien, no solo en la cadena larga.

Una línea automática vacía, con hold 0, ya no cuenta como un frame nuevo. Antes `yield return PlayLine` le devolvía el turno a Unity aunque la línea no esperara, y 40 líneas nunca llegaban a 32 en el mismo frame.
