# Tears of Mathra — Velvet Jewel Bushes

Three bright color variants of the existing velvet bush family:

- Rosepink Spreading Bush: vivid pink, 17 leaves, approximately 3.4 m wide and 1.2 m tall.
- Azure Dense Mound: bright blue, 23 leaves, approximately 2.8 m wide and 2 m tall.
- Violet Fountain Bush: bright purple, 13 leaves, approximately 3.2 m maximum spread and 3.3 m tall.

These are color variants of the three previous bush silhouettes. They preserve the same geometry, painted radial veins and soft, matte velvet finish, with plum stems. They do not replace or recolor your existing green bushes.

## Import
Import MATHRA_Velvet_Jewel_Bushes_3_Prefabs.unitypackage. Drag prefabs from Assets/Mathra/VelvetJewelBushes/Prefabs into your scene. All meshes, textures, materials and the shader are included; the previous packs are not required. Root pivots are at ground level, with leaves and stems as separate children. Meshes are visible in Edit mode without a generation script.

## Color and surface
Each bush has its own leaf material and full texture file. The same original painted texture is reused unchanged, with shader-based colorization. Pure color palette defaults to 1: it removes the original teal hue before applying the chosen color, preserving the veins and mottling without muddy green contamination. Lower it to mix the original botanical hue back in.

Use Leaf color, Velvet softness, Velvet edge color, Light through leaf and Wind controls on the leaf material. Duplicate the material before customizing just one scene instance. The velvet is a shading effect, not physical fur. Color is bright but not emissive; lighting and exposure affect its appearance. The stems share a separate plum material.

The opaque two-sided URP shader includes main/additional lighting, shadows, fog, depth and depth-normal passes. Wind is a small shader displacement; vertex normals remain static. Set wind displacement to zero for still plants.

## Scope and checks
Decorative foreground/detail bushes; no colliders, NavMesh obstacles, camera fading or automatic LODs. Leaf triangles: 44,880 pink, 60,720 blue, 34,320 purple, plus stems. Profile heavy repetition in your scene.

Validated 3 prefab assets, 6 nonempty meshes, finite vertices, unit normals, nondegenerate triangles, UV ranges, index buffers, texture/material references and package integrity. Unity Editor shader compilation and playback remain unverified in this environment.

Previews show the actual delivered meshes rendered in Blender with approximate matte materials. They are not Unity screenshots, and each view is framed independently rather than shown at a shared scale.
