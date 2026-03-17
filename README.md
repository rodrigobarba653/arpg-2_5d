Ultima Actualización 13/marzo/26:

**GitHub Workflow (Terminal) – Instrucciones para el equipo**
Estas son las instrucciones básicas para trabajar con GitHub desde la terminal.
Sigue estos pasos en orden cada vez que vayas a trabajar.

-----------------------------------------------------------------------------------------------------------
**1. Ir al branch dev**

git checkout dev

Esto te lleva al branch principal de desarrollo.

-----------------------------------------------------------------------------------------------------------
**2. Actualizar dev**

git pull origin dev

Esto descarga los cambios más recientes del proyecto.
Siempre haz esto antes de empezar a trabajar.

-----------------------------------------------------------------------------------------------------------
**3. Ver en qué branch estás**

git branch

El branch actual aparecerá con un *.

-----------------------------------------------------------------------------------------------------------
**4. Crear tu branch de trabajo**

git checkout -b nombre-de-tu-branch

Los nombres de branch no pueden tener espacios. Usa guiones.

Convenciones de nombres:

feature/nombre  → función nueva
fix/nombre      → corrección de bug
update/nombre   → actualización de asset o sistema

Ejemplos:

git checkout -b feature/jump
git checkout -b fix/jump-timing
git checkout -b update/jump-sprite

-----------------------------------------------------------------------------------------------------------
**5. Trabajar en tu branch**

Reglas importantes:

A) No modificar cosas fuera de tu área
Si trabajas en gameplay, no modifiques cámara, color grading o escenarios.

B) Una función por branch
Cada branch debe enfocarse en una sola cosa.

C) Hablar antes de modificar scenes
Si vas a modificar una scene que otra persona está usando, avisa antes.

D) No cambiar estructura de carpetas
Evita renombrar o mover folders. Esto puede causar conflictos.

-----------------------------------------------------------------------------------------------------------
**6. Preparar cambios**

git add .

Esto prepara todos los cambios para el commit.

-----------------------------------------------------------------------------------------------------------
**7. Crear commit**

git commit -m "mensaje de commit"

Esto guarda los cambios localmente.

Ejemplo:
git commit -m "Add jump animation and sprite"

-----------------------------------------------------------------------------------------------------------
**8. Subir tu branch a GitHub**

git push origin nombre-de-tu-branch

Ejemplo:
git push origin feature/jump

-----------------------------------------------------------------------------------------------------------
**9. Crear Pull Request**

Ve al repositorio en GitHub y haz click en el botón:

Compare & pull request

-----------------------------------------------------------------------------------------------------------
**10. Verificar el Pull Request**

Base branch: dev

Compare branch: tu branch (ej: feature/jump)

Debe verse así:

base: dev ← compare: feature/jump

-----------------------------------------------------------------------------------------------------------
**11. Escribir el Pull Request**

Título:
Jump Function

Descripción (ejemplo):

Added jump animation and sprite.
Configured animator transitions.

-----------------------------------------------------------------------------------------------------------
**12. Crear el Pull Request**
Haz click en:

Create pull request

-----------------------------------------------------------------------------------------------------------
**13. Esperar revisión**
Tu Pull Request será revisado antes de integrarse a dev.

-----------------------------------------------------------------------------------------------------------
**Regla más importante:
Nunca hacer push directo a dev o main.**






---------------Controles---------------- 

Movimiento: WASD o Left Stick

Roll: Button East o L

Attack: Button West o J

Jump: South Button o Space



---------------Bugs Conocidos-----------

El movimiento se traba con algunos angulos de plataformas

En el salto si el jugador toca la orilla de una plataforma se puede trabar

Faltan animaciones de salto, para senitr el salto y ajusar script



-----------Siguientes Acciones--------
1. Nadar
2. Sprint (Vamos a pensar si aplica o no)
3. Interactuar (botones)
4. Enemigos
5. Vida y daño
6. Magia o Range ( o su equivalente)
