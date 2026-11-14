# Venado Mathra V2.1 — más cuadros y arranque desde parado

## Qué cambió

- 32 sprites en total. Los 16 PNG originales se conservan byte por byte.
- Carrera: 16 cuadros en vez de 8, con el mismo ciclo de 0.72 segundos.
- Dos poses adicionales para los giros de cabeza al observar.
- Cuatro poses para levantar y bajar la cabeza.
- Dos poses de impulso para pasar de estar parado a correr.
- Venado_Huyendo empieza observando durante 1.2 segundos, se sobresalta, toma impulso y corre.

El mismo venado combina los comportamientos; no necesitas cambiar de prefab.

## Instalar o actualizar

1. Descomprime el ZIP fuera de Assets.
2. Importa Mathra_Venado_Animado_V2_1.unitypackage.
3. Si ya tienes V1/V2, permite actualizar sus archivos y espera a que termine la compilación.
4. Ejecuta Roque > Wildlife > Deer > Repair NONE keyframes and prefabs.
5. La consola debe confirmar: 7 clips validated, zero NONE sprite keyframes.
6. Usa los prefabs de Assets/RoqueWildlife/DeerV1/Prefabs.

La carpeta sigue llamándose DeerV1 por compatibilidad, pero su contenido es V2.
Se conservan los GUID originales. No dupliques la carpeta ni MathraDeer.cs.
Los valores sobrescritos en el Inspector de tus instancias siguen siendo tuyos.
Si editaste directamente los archivos del paquete, respalda esas modificaciones antes de importar.
La carpeta Assets del ZIP es una alternativa al .unitypackage: usa solo un método.

## Comportamientos

| Prefab | Secuencia |
| --- | --- |
| Venado_Bebiendo | Bebe → levanta cabeza → observa → baja cabeza → bebe. |
| Venado_Observando | Observa hasta que se asusta o cambias el comportamiento. |
| Venado_Huyendo | Observa → espera → sobresalto → impulso → carrera. |

En cualquiera, arrastra Ax y/o Jyn a Threats para activar el susto por proximidad.
Si ambos están activos pero solo uno es jugable, asigna únicamente al actual.
Con Threats vacío, Auto Find Player Tag busca objetos activos con tag Player.
Si se asusta mientras bebe, primero levanta la cabeza y después inicia la huida.

## Controles

| Campo | Función |
| --- | --- |
| Stand Before Running | Tiempo parado del prefab Huyendo antes del sobresalto; 1.2 segundos inicialmente. |
| Alert Seconds | Pausa del sobresalto antes del impulso; 0.25 segundos inicialmente. |
| Run Speed | Velocidad real; 4 unidades/segundo inicialmente. |
| Acceleration | Rapidez con la que alcanza la velocidad de huida. |
| Flee Distance | Distancia antes de detenerse; 9 unidades inicialmente. |
| Escape Target | Empty opcional para dirigir la huida. |
| Animation Speed | Velocidad de los cuadros; 1 conserva el ritmo preparado. |
| Run In Place | Reproduce la secuencia sin desplazamiento. |
| Alternate Drink And Look | Alternancia automática de beber y observar. |

Levantar/bajar la cabeza dura 0.48 segundos; el impulso dura 0.24 segundos, a Animation Speed = 1.
El controlador ajusta esos tiempos junto con el Animator. No usa fundidos ni desenfoque.
Al detenerse después de correr vuelve a observar; no se añadió una animación de frenado.

## Probar

Durante Play, selecciona el venado y pulsa Beber, Observar, Asustar o Volver al punto inicial en el Inspector.
Activa Run In Place para verlo sin que abandone el encuadre.
El menú Roque > Wildlife > Deer > Create isolated preview scene abre una escena adicional con cámara y venado.
Las demás escenas siguen cargadas y sus objetos no se modifican.
El GIF incluido compara V1/V2 y muestra beber → observar → huir. Es una previsualización externa, no una captura de Unity.

## Suelo, cámara y aspecto

Con Follow Ground activo, el suelo necesita Collider 3D. Ground Layers debe incluir suelo; Obstacle Layers, obstáculos.
Si tu toma es plana y no hay colliders, puedes desactivar Follow Ground para conservar su altura.
Se detiene ante obstáculos o caídas; no calcula rutas alrededor ni usa NavMesh.
Face Camera sigue View Camera o Camera.main. El cuerpo tiene vista de perfil y espejo izquierda/derecha.
Los sprites usan lienzo común de 384 × 384, filtrado Point, pivote compartido y sin compresión.
El material sigue siendo URP Unlit: no recibe luces ni sombras dinámicas.
Los cuadros nuevos están basados en el diseño original; pueden variar algunos detalles de manchas y astas.

## Código y eventos

Namespace: Roque.Wildlife. Usar durante Play:
DrinkNow(), LookNow(), RunNow(), ScareNow(), Scare(Transform source), ScareFromPosition(Vector3 sourcePosition), ResetDeer().
RunNow() ahora incluye sobresalto e impulso. ResetDeer() vuelve al punto inicial y reinicia el comportamiento elegido.

## Verificación

Se revisaron los PNG, referencias, GUID, curvas de sprites, empaquetado y duraciones compartidas con el controlador.
Los 16 PNG originales y sus identificadores se conservaron exactamente.
No hay Unity instalado en el entorno de preparación: la compilación C#/shader y la prueba en Play Mode siguen pendientes en tu proyecto.
Al importar V2 se reconstruyen una vez las curvas con los identificadores reales de los sprites importados por tu Unity.
También puedes hacerlo manualmente desde Roque > Wildlife > Deer > Rebuild animation bindings.
El paquete solo actualiza su carpeta RoqueWildlife. No cambia ProjectSettings, Global Volumes, materiales de Ax/Jyn ni escenas del juego.

Arte nuevo creado con la herramienta integrada image_gen. Hojas fuente e instrucciones incluidas en ArtSource.

## Corrección de keyframes None

La V1 podía importar curvas con referencias numéricas que no correspondían al Sprite real, dejando None en los keyframes. Esta versión resuelve los Sprite importados mediante AssetDatabase.LoadAllAssetsAtPath, reconstruye las siete curvas con objetos Sprite reales, reconstruye los estados del Animator y vuelve a enlazar los tres prefabs. Al terminar comprueba cada keyframe; no marca la reparación como completa si hay alguno vacío. La reparación se intenta una vez automáticamente al importar y también está disponible en el menú indicado arriba.

La protección en LateUpdate mantiene el último sprite válido si una animación externa vuelve a asignar None y muestra una advertencia: eso evita la desaparición por ese motivo, pero no sustituye la reparación de las curvas.

Si una instancia existente tiene overrides con None en Animator, SpriteRenderer o referencias del componente, el arreglo del prefab no elimina esos overrides. Para comprobar primero el paquete, arrastra un prefab nuevo después de reparar. No reviertas todos los overrides de tu instancia: conserva sus ajustes de escena y corrige solo esas referencias si hace falta.

Esta corrección no se ha ejecutado aquí dentro de Unity. El mensaje de validación lo produce tu Unity después de comprobar los objetos Sprite reales.
