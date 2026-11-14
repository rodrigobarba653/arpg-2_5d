MATHRA MAGAZINE SHIMMER — UNITY 6 URP (PROXIMITY VERSION)

1. Copy MathraMagazineShimmer.cs and MathraMagazineShimmer.shader into Assets/.
2. On the magazine GameObject with MeshFilter and MeshRenderer, Add Component > Mathra Magazine Shimmer.
3. Drag the active AX/Jyn player's MOVING Transform into Player. For a party system, assign the currently controlled player's Transform (you can update the reference at runtime). If that object has the Player tag, auto-find also works at Start.
4. Set Activation Distance (e.g. 6) and Exit Distance Buffer (e.g. 1). The shimmer stops immediately after leaving 7 units and restarts on coming within 6 units.
5. Distance Check Interval = 0.25 seconds is recommended (uses squared distance, no Physics).
6. Press Play. When near, sweep runs intermittently; when far, overlay renderer OFF and animation OFF.

PERFORMANCE: No extra light, physics triggers, particles, texture allocations, or per-frame distance tests. Extra overlay draw(s) only while the player is nearby AND a sweep is active. One low-cost Update per object checks the next scheduled time.

NOTE: This version replaces the PREVIOUS script, not added alongside it. Keep the same shader.
If the magazines contain multiple child MeshRenderer objects, attach the script to each mesh child, or combine the meshes. Do not put the script on a parent without MeshFilter.
No Player found? Set the Transform manually or tag the active player 'Player'.
