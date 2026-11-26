VALCYR WAR ROOM — UNITY URP PBR MATERIAL
Resolution: 2048x2048, seamless procedural texture set

Files:
- Valcyr_WarRoom_Albedo.png
- Valcyr_WarRoom_Normal.png
- Valcyr_WarRoom_MetallicSmoothness_URP.png  (RGB = Metallic, A = Smoothness)
- Valcyr_WarRoom_AO.png
- Valcyr_WarRoom_Height.png
- Valcyr_WarRoom_Metallic.png
- Valcyr_WarRoom_Smoothness.png
- Valcyr_WarRoom_Roughness.png

Recommended Unity 6 / URP setup:
Shader: Universal Render Pipeline/Lit
Base Map: Albedo
Metallic Map: MetallicSmoothness_URP
Metallic slider: 1.0
Smoothness source: Metallic Alpha
Normal Map: Normal, strength 0.35–0.60
Occlusion Map: AO, strength 0.35–0.55
Tiling: start around 2x2 to 4x4 depending on UV scale.
Surface: Opaque.

Look target:
Dark graphite/blue-black polished stone-metal, subtle warm mineral streaks,
slight age/wear, restrained reflections. Intended to match the Valcyr war-room
platform without baking explicit gold rings into the texture.

IMPORTANT:
If you want actual gold/bronze rails or trim exactly on selected mesh edges,
use a second material slot or a mask/vertex-color workflow; a generic tiled PBR
material cannot know where those geometric accents are.
