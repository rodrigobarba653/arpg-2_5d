# Sprint 05 — Phase B Step 2 Report

**Fecha:** 2026-11-10  
**Estado:** Cerrado. Completed, Failed y Cancelled cuadran, y el guardado del 2026-11-10 15:44 solo tiene la flag del pad verde.  
**Siguiente:** Parar. No empezar Step 3.

La base sigue siendo `SPRINT_05_PHASE_B_PLAN.md` y `SPRINT_05_PHASE_B_STEP_1_REPORT.md`. No se tocaron las variantes de texto ni el lock apagado de `NPC_LineVariants`.

---

## Archivos

Creados:

- `Dialogue/Runtime/DialogueSessionOutcome.cs`
- `Events/SPRINT_05_PHASE_B_STEP_2_REPORT.md` (este archivo)

Modificados para el outcome:

- `Dialogue/Runtime/DialogueSession.cs`
- `Dialogue/NPC/NarrativeDialogueTrigger.cs` — solo la condición con la que se consume Once
- `Dialogue/Runtime/DialogueService.cs` — comentario de la cola diferida
- `Editor/Events/EventSystemTestLabBuilder.cs` — menú nuevo, sin regenerar fixtures viejos

Modificados después, solo presentación:

- `Dialogue/Presentation/DialogueUIController.cs` — tamaño de caja, retrato y letra
- `Dialogue/Presentation/DialogueChatboxView.cs` — la caja crece con el texto
- `Dialogue/Runtime/DialogueLayoutSettings.cs` — defaults 760×340 y 540×210
- `Dialogue/Runtime/DialogueLayoutSolver.cs` — la altura medida entra al clamp
- `Dialogue/Runtime/AmbientDialogueHandle.cs` — mide el texto ambient
- `Dialogue/Data/CharacterPortraitRoster.cs` — búsqueda sin distinguir mayúsculas
- `Scenes/EventSystem_TestLab.unity` — `layoutSettings` del `DialogueService`
- `Assets/Game/Dialogue/TestLab/Lab_PortraitRoster.asset` — sprites Neutral de Ax y Jyn

Sin cambios: `DialogueTextResolver`, `textVariants`, reglas de layout, `GameplayLock`, `WorldState`, save/load, `NPC_LineVariants`.

---

## Resultados de la sesión

`Outcome` queda en `None` mientras el diálogo sigue.

| Resultado | Quién lo escribe | Qué pasó |
|-----------|------------------|----------|
| `Completed` | `Finish()`, solo si todavía está en `None` | Llegó a un `DialogueEndNode`, o la línea tiene `nextNodeId` vacío |
| `Cancelled` | Solo `Cancel()`, y solo si todavía está en `None` | Esc, Interrupt (`CancelInteractive`), o destroy del servicio |
| `Failed` | `Fail()`, antes de `Finish()` | No hay entrada, un `nextNodeId` no vacío que no existe, o un tipo de nodo que este runner no reproduce |

`WasCancelled` es true solo cuando el resultado es `Cancelled`. `Finish()` no pisa un resultado que ya estaba puesto. Los tres finales sueltan las cajas, sueltan el lock si esta sesión lo tomó, y avisan a `DialogueService` una sola vez.

El warning de fallo es:

```text
[Dialogue] Session failed (missing-node|null-entry|unsupported-node) asset='…' node='…' …
```

---

## OncePersistent

`NarrativeDialogueTrigger` consume OncePerScene y OncePersistent solo si `Outcome == Completed`.

Un `Failed` no consume el flag. El cooldown sí corre, igual que en un cancel, para que el volumen no se dispare en cada frame. Pasado el cooldown se puede volver a entrar.

Un final vacío o un `DialogueEndNode` siguen siendo `Completed`. Un asset válido de Phase A no cambia de comportamiento.

La cola diferida corre en cualquier final. El comentario dice que Phase C debe mirar `Completed` antes de otorgar consecuencias. Este paso no otorga nada.

---

## Compilación

El editor de Unity abierto no había reimportado estos scripts (el log seguía en las 13:15, antes de guardarlos). Una compilación aparte del assembly de juego no marcó errores en el diálogo. Los únicos errores de esa corrida fueron tres referencias viejas a `CartoonFX` que faltaban en ese set manual, no en este paso. El assembly de editor compiló con 0 errores contra el DLL de runtime que Unity ya tenía.

Hay que salir de Play Mode y dejar que Unity recompile. El menú nuevo aparece después de eso.

---

## QA manual (consola del 2026-11-10)

El usuario pegó la consola y dio el pase por cerrado en lo que el log muestra. No hace falta repetir los pads salvo para mirar las flags.

| Qué se vio | Lectura |
|------------|---------|
| `Interactive start 'lab_outcome_complete'` y después otro diálogo, sin `Session failed` ni `Session cancelled` | Completed. Ese final no escribe una línea propia. |
| `Session failed (missing-node) asset='lab_outcome_fail' node='of1' next='missing_outcome_node'`, y el mismo asset volvió a arrancar más tarde | Failed. Once no se consumió. |
| `Session cancelled (user-cancel) asset='lab_outcome_hold'`, y el hold volvió a arrancar | Cancelled. Once no se consumió. Confirmar no lo completa: la línea se apunta a sí misma. |
| `Session cancelled (narrative-interrupt)` y enseguida otro `Interactive start` | Interrupt cancela la sesión activa y abre la siguiente. La cancelada en este log es `lab_outcome_interrupt`, no el diálogo del pad azul. El segundo collider del grupo entró al mismo trigger morado y lo reinició. |
| `[Party] SetActive` junto a `lab_outcome_interrupt` | El usuario confirmó que Q no cambia de personaje mientras la caja está en pantalla. El cambio del log fue con el candado ya suelto. El NPC verde de Step 1 sigue sin lock a propósito. |

Guardado `slot0.json` a las 15:44, después de fallar el rojo, cancelar el naranja y reiniciar el morado. `flags=1` en cada F5 posterior.

- `dialogue.narrative.Trigger_Outcome_OnceComplete.seen` = true
- `dialogue.narrative.Trigger_Outcome_OnceFail.seen` no está
- `dialogue.narrative.Trigger_Outcome_OnceCancel.seen` no está
- `dialogue.narrative.Trigger_Outcome_InterruptVictim.seen` no está

## Presentación (no es Step 3)

Después de Step 2 se agrandaron retratos y letra, y la caja crece con el texto. No cambia outcomes, variantes ni locks.

Retratos placeholder, no iconos de menú:

- `Assets/Game/Dialogue/Portraits/Placeholders/Ax_Placeholder.jpg` — Ax, el niño
- `Assets/Game/Dialogue/Portraits/Placeholders/Jyn_Placeholder.jpg` — Jyn, la niña rosa
- `Lab_PortraitRoster.asset`: expresión Neutral de `ax` y `jyn`. Villager A/B siguen sin sprite.
- `CharacterPortraitRoster.FindCharacter` compara con `OrdinalIgnoreCase`.
- `EnsureTestRoster` vuelve a cargar esos jpg. No regenerar los assets de Phase A: `EnsureDialogueAsset` les pisa los nodos.

Tamaños en unidades de referencia del canvas (1920×1080). La escena `EventSystem_TestLab` guarda los suyos en `DialogueService.layoutSettings`; hay que cambiar código y escena juntos o Play Mode sigue con los viejos.

| | Ambient | Interactivo |
|--|---------|-------------|
| Caja | 540×210 | 760×340 |
| Retrato | no se muestra | 280×280, margen izquierdo 20. El texto empieza en x=320 |
| Nombre | 34 | 40 |
| Cuerpo | 44 | 50 |

`DialogueChatboxView.PrepareBodyFit` guarda la línea completa. `FitHeightToBody` mide ese texto con `GetPreferredValues` y sube `sizeDelta.y`. El typewriter sigue pintando solo el trozo visible. La altura mínima cubre el retrato más 48 si está activo. Tope 780; si se pasa, el cuerpo usa puntos suspensivos. `ApplyInteractiveWidth` vuelve a medir cada frame y `UseFittedHeight` le pasa esa altura al clamp. Ambient mide al mostrarse y en cada `TickLayout`.

Salir de Play Mode y volver a entrar. El canvas de diálogo se construye al vuelo y un Play Mode ya abierto no recoge el cambio.

## QA en TestLab

1. Sal de Play Mode. Abre `EventSystem_TestLab`.
2. **Tools → ARPG → Events → Add Dialogue Outcome Fixtures (Phase B Step 2)**. Guarda. No regenera Phase A ni el NPC verde.
3. Play. Los pads nuevos están en **z ≈ −32**.
4. **E / Enter / South** avanza. El primer E termina el typewriter. El segundo sale de la línea. **Esc** cancela. **F6** borra WorldState si hay que repetir el pad verde. El cooldown de estos pads es 2.5 s. Un flag consumido sobrevive a salir de Play Mode hasta F6 o un domain reload.

| x | Objeto | Color | Qué hacer | Qué debe pasar |
|---|--------|-------|-----------|----------------|
| 0 | `Trigger_Outcome_OnceComplete` | Verde | Entra y confirma la línea | Bloquea el movimiento. No sale `Session failed`. Al salir, espera 3 s y vuelve a entrar: no habla. Flag `dialogue.narrative.Trigger_Outcome_OnceComplete.seen`. |
| 6 | `Trigger_Outcome_OnceFail` | Rojo | Entra y confirma | Warning `Session failed (missing-node) asset='lab_outcome_fail' node='of1'`. El movimiento vuelve. A los 3 s la línea se puede repetir. El flag no se escribe. |
| 12 | `Trigger_Outcome_OnceCancel` | Naranja | Entra y pulsa Esc | `Session cancelled (user-cancel)`. Puedes moverte. Confirmar no lo termina: repite la línea. A los 3 s, al entrar, todavía habla. |
| 18 | `Trigger_Outcome_InterruptVictim` | Azul | Entra. No confirmes ni pulses Esc. Camina a la derecha | No bloquea el movimiento. Es OncePersistent. |
| 23 | `Trigger_Outcome_InterruptIncoming` | Morado | Písalo mientras el azul sigue en pantalla | El azul se cancela con `narrative-interrupt` y no guarda su flag. El morado bloquea. Confírmalo para salir. A los 3 s el azul vuelve a hablar. |

Flags, por si hay que mirarlos en consola:

- `dialogue.narrative.Trigger_Outcome_OnceComplete.seen`
- `dialogue.narrative.Trigger_Outcome_OnceFail.seen`
- `dialogue.narrative.Trigger_Outcome_OnceCancel.seen`
- `dialogue.narrative.Trigger_Outcome_InterruptVictim.seen`

El NPC verde de Step 1 sigue sin lock. Los NPC interactivos de Phase A siguen bloqueando el cambio de personaje.

---

## Fuera de este paso

Choices, navegación, ramas, layout, guarda de ciclos, fuse de frames y `StartDialogueAction`. La línea de los pads naranja y azul se apunta a sí misma para poder cancelar o interrumpir sin completarla sin querer.
