# Sprint 05 — Phase B Step 7 Report

**Fecha:** 2026-11-11  
**Estado:** Implementado. El naranja ya pasó. Faltan verde, azul y blanco.  
**Siguiente:** Parar. No empezar Step 8.

La base sigue siendo `SPRINT_05_PHASE_B_PLAN.md` y el reporte de Step 6. El plan decía que el menú siguiera al jugador cada frame y que, si no cabía arriba ni abajo, probara un lado. Esta autorización lo deja quieto durante la choice y solo usa abajo, arriba y el clamp. El solver de ramas no cambia.

---

## Archivos

Modificados:

- `Dialogue/Runtime/DialogueChoiceLayout.cs` — `LowerCenter` igual; `NearPlayerChatbox` abajo, arriba o el lado con menos solape
- `Dialogue/Runtime/DialogueChoicePresentation.cs` — el gap ya se usa
- `Dialogue/Runtime/DialogueLayoutSettings.cs` — el default sigue en `LowerCenter`
- `Dialogue/Presentation/DialogueChoiceView.cs` — el menú no se activa hasta tener pivote
- `Dialogue/Runtime/DialogueSession.cs` — lee la caja del jugador y coloca el menú una vez
- `Editor/Events/EventSystemTestLabBuilder.cs` — menú nuevo, sin regenerar fixtures viejos

Creado:

- `Events/SPRINT_05_PHASE_B_STEP_7_REPORT.md` (este archivo)

Sin cambios: `DialogueGraph`, el solver de ramas, el filtro de Step 5, el ciclo y el tope de Step 6, `GameplayLock`, el estilo del menú.

---

## Ancla

Al abrir el menú en `NearPlayerChatbox`:

1. El id es el `SessionActiveCharacterId` congelado al empezar. No se vuelve a leer `Party.Active`.
2. Se busca la burbuja ya creada cuya clave es ese id (`Ax` o `Jyn`, sin distinguir mayúsculas).
3. La posición es `GetScreenRect()` de esa burbuja: la caja que ya está en pantalla, no el mundo del personaje y no un slot recalculado.
4. Las otras burbujas visibles se miden solo para no taparlas. No se escribe su `RectTransform`.
5. Si no hay caja de jugador visible, esta apertura usa `LowerCenter`. No lanza excepción.

No se llama a `BuildOrRebuildConversationPlan`. El plan de Step 4 sigue mandando en las cajas.

---

## Colocación

El menú se alinea en horizontal con el centro de la caja del jugador. El hueco es `gapFromBubbleRef` (24 en los fixtures) por la escala del canvas. `offsetRef` sigue siendo un empujón del diseñador.

1. Cada lado se recorta primero al área segura.
2. Debajo de la caja, si ese rect no pisa la caja del jugador ni otra burbuja visible.
3. Si no, arriba, con la misma prueba.
4. Si ninguno cabe, se queda el lado con menos solape. El recorte normal no escribe warning.

`LowerCenter` sigue en el borde inferior del safe, con `marginRef` y `offsetRef`, y se vuelve a aplicar cada frame mientras el menú está abierto. Un asset en `NearPlayerChatbox` se coloca una sola vez, también si esa vez cayó al fallback.

Subir o bajar la selección no mueve el menú. Confirmar o cancelar lo cierra como antes. Volver a una choice más tarde mide otra vez.

---

## Medida

El alto sale de `DialogueChoiceView.PreferredSize`: prompt, filas de 64 y el tope `maxHeightRef`. El ancho es `maxWidthRef`. Esos números están en unidades de referencia del canvas (1920×1080). Se pasan a píxeles con `canvasScaleFactor`, el mismo factor que ya usa el solver. El pivote del menú sigue siendo el centro del borde inferior. No hay coordenadas fijas de 1920×1080.

---

## Primer frame

`Show` arma las filas y deja el objeto inactivo. `PlaceChoiceMenu` escribe `sizeDelta` y el pivote, y solo entonces llama a `Reveal`. El primer frame pintado ya está en el sitio. No hay un menú un frame en el origen.

---

## LowerCenter

El default del servicio no cambió. Un asset sin `overrideChoicePresentation` sigue ahí. La fórmula es la de Step 3. Esos menús se recolocan cada frame, como antes. Los NPC de Steps 3 a 6 no traen override, así que no se mueven a la caja del jugador.

---

## Fixtures

**Tools → ARPG → Events → Add Choice Placement Fixtures (Phase B Step 7)**

No toca Phase A ni los NPC de Steps 1 a 6. Están en **z ≈ −68**. El ancla `villager_a` va desplazada para que el lado no dependa de por dónde te acercas. La cámara sigue al jugador, así que el borde se prueba con la caja ya recortada al safe, no caminando hasta el marco.

| x   | Objeto                   | Asset             | Qué es                                                                                                         |
| --- | ------------------------ | ----------------- | -------------------------------------------------------------------------------------------------------------- |
| 0   | `NPC_Place_Left` verde   | `lab_place_left`  | Ancla del aldeano a +8. La caja del jugador queda a la izquierda. `NearPlayerChatbox`. "Ax only." solo con Ax. |
| 12  | `NPC_Place_Right` azul   | `lab_place_right` | Ancla a −8. La caja del jugador queda a la derecha.                                                            |
| 24  | `NPC_Place_Edge` naranja | `lab_place_edge`  | Cajas empujadas al borde inferior. El menú de 6 opciones no cabe debajo y sube.                                |
| 36  | `NPC_Place_Lower` blanco | `lab_place_lower` | Sin override. Menú abajo al centro.                                                                            |

Manual, lock puesto, Esc cancela.

---

## QA

1. Sal de Play Mode. Abre `EventSystem_TestLab`.
2. Corre el menú de arriba. Guarda. Entra en Play.
3. Hazlo a 1920×1080 y otra vez con el Game view más estrecho.
4. **E** en cada NPC. Avanza las dos líneas. Mira el menú antes de pulsar nada.
5. Sube y baja la selección. El menú no debe saltar. Las cajas no deben moverse. En la consola el plan sale una vez, no otra al abrir el menú.

| NPC     | Consola                                                                         | Pantalla                                                                                                                                          |
| ------- | ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| Verde   | `requested=NearPlayerChatbox used=Below fallback=false asset='lab_place_left'`  | El menú queda debajo de la caja del jugador, a la izquierda. Con Ax, `visible=3 authored=3`. Con Jyn, `visible=2 authored=3` y no sale "Ax only." |
| Azul    | `used=Below fallback=false asset='lab_place_right'`                             | El menú sigue la caja de la derecha, no un sitio fijo a la izquierda.                                                                             |
| Naranja | `used=Above fallback=false asset='lab_place_edge'`                              | Las cajas quedan bajas. El menú queda encima de la caja del jugador, dentro del safe, sin taparla.                                                |
| Blanco  | `requested=LowerCenter used=LowerCenter fallback=false asset='lab_place_lower'` | El menú queda abajo al centro, como los de Step 3.                                                                                                |

En el verde y el azul, `used=Below` es lo esperado a 1080 de alto. Si el Game view es tan bajo que el menú no cabe bajo la caja, `used=Above` sigue siendo correcto: el menú queda pegado a esa caja, no abajo al centro. `fallback` sigue en false.

En los tres primeros, "Leave." termina con `Session completed`. Esc en cualquiera escribe `Session cancelled (user-cancel)` y suelta el lock. No debe salir `Session failed` ni un segundo `Conversation layout plan built` al abrir el menú.

### Naranja

Pasó. Un solo plan, `core=2`. Luego `requested=NearPlayerChatbox used=Above fallback=false asset='lab_place_edge'` y `Choice 'pe_choice' visible=6 authored=6`. El menú quedó encima de la caja, sin taparla.

---

## Límites

El menú no sigue al jugador si la charla no bloquea el movimiento. Se calcula al abrir y se queda ahí. El plan hablaba de seguirlo cada frame; esta autorización lo deja quieto.

No hay un puesto a la izquierda o a la derecha de la caja. Solo debajo, encima, y el clamp si ninguno entra.

Si los dos lados pisan una caja, el clamp puede dejar algo de solape. Eso no avisa.

Sin caja de jugador visible, esa apertura cae a `LowerCenter` (`fallback=true`). Estos cuatro NPC siempre dicen una línea del jugador antes del menú, así que ese camino no se ve aquí.

El naranja no pone al personaje en el borde de la pantalla. La cámara lo mantiene al centro. El offset del fixture baja las cajas hasta el safe, y el menú de 420 ya no cabe debajo. El lado se elige con el rect ya recortado: un menú que solo cabía fuera de pantalla no gana y luego se arrastra encima de la caja.
