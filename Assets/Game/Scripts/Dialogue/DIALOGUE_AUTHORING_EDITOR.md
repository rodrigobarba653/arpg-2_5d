# Guía — Dialogue Editor (authoring)

Documento canónico del diálogo interactivo. Menú: **Tools → ARPG → Dialogue → Dialogue Editor**.

EventRunner solo arranca/espera/cancela con **Start Dialogue**; el contenido vive en el `DialogueAsset`.

Para cutscenes / flags / puertas: `Assets/Game/Scripts/Events/EVENT_RUNNER_AUTHORING_GUIDE.md`.

---

## 1. Speaker por línea

1. Abrí la escena con los NPCs.
2. **Tools → ARPG → Dialogue → Dialogue Editor** → abrí el asset.
3. En una Dialogue Line:
   - **Speaker = Active Player** → Jyn / Ax según quién controlás.
   - **Speaker = NPC (scene character)** → ObjectField del Transform en la escena.
4. El editor crea/usa `DialogueSpeakerBinding` (id estable en el asset; el Transform vive en la escena).

### Chatbox placement

Independiente del speaker:

- **Follow Speaker** — cola hacia el personaje.
- **Screen (no pointer / off-camera)** — banner arriba, sin cola; el speaker/portrait siguen siendo Player o NPC.

---

## 2. Retratos

| Modo | Qué hace |
| --- | --- |
| **Use Default** | Player → roster Neutral (jyn/ax). NPC → `DialogueSpeakerBinding.defaultPortrait` o roster. |
| **Custom** | Sprite(s) en la línea. |
| **Hidden** | Sin retrato. |

Ambient nunca muestra retratos.

---

## 3. Start Dialogue en EventRunner

```
Start Dialogue
- Dialogue Asset
- Wait Until Finished
- Allow Cancel
- Progression Mode
- Auto Advance Hold Sec
```

Lo demás (Npc Speaker Id, Npc Anchor, Dialogue By Character) queda bajo Advanced / legacy.

---

## 4. Choices / flags

| Concepto | UI | Significado |
| --- | --- | --- |
| Texto de opción | Option text | Solo label |
| Continúa a | Continue to | Siguiente nodo / fin |
| Recordar elección | Remember as | Escribe un flag en WorldState |
| Acción inmediata | Add action… | Door / item / SetFlag, etc. |

El flag se escribe al confirmar la opción (memoria de runtime). Persistencia a disco = `SaveManager` como el resto de WorldState.

---

## 5. Validación

- Inspector del `DialogueAsset` muestra issues.
- **Tools → ARPG → Dialogue → Run Validator Self-Check** prueba las reglas del validador.
