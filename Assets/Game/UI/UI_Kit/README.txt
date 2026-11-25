TOM UI KIT v2 — production geometry, transparent PNG and SVG

IMPORTANT: The assets are mathematically constructed (no AI raster extraction, no blur, no baked glow). PNGs are rendered at 2x SVG coordinate resolution.

CHATBOX 9-SLICE (PNG 1280x360): Left=264 Right=264 Top=100 Bottom=100 (all in PNG pixels). Use identical slicing on dialogue_background_9slice.png.
NOTIFICATION 9-SLICE (PNG 1120x200): Left=230 Right=100 Top=60 Bottom=60. Use identical slicing on notification_background_9slice.png.

The 8 corner/edge PNGs are also supplied separately. For Unity you may use the assembled 9-slice frame, OR assemble the 8 pieces manually; do not render both at once. The 9-slice preserves the left portrait-related contour, top/bottom details and right dots. The center stretch zones are straight.

TAIL: tail_bottom_right_angle.png is a RIGHT triangle. Its attached HORIZONTAL TOP EDGE HAS NO OUTLINE; only its vertical leg and diagonal have coral stroke. Place tail_connection_mask behind the tail and above the frame at the join, to erase the underlying coral border. This is mandatory for the seamless look. Keep the mask the same dark background color and update its color if the background is changed. Rotate or use supplied directional variants. Tail must remain unsliced and unscaled nonuniformly.

PORTRAIT: portrait_frame and portrait_background are separate, do not slice; clip portrait to matching polygon via custom mask or use a rectangular transparent portrait positioned inside. Do not stretch portraits.

UNITY: Texture Type Sprite (2D and UI), Sprite Mode Single, Mesh Type Full Rect, Filter Mode Bilinear for high-res smooth or Point for pixel-perfect look, Compression None, Alpha Is Transparency enabled. UI Image Type Sliced for frame/background; Simple for corners, tails, portrait, indicators.

Cursor: integrate with existing dialogue layout solver and Event Runner, do not rewrite. Stable box positions; pointer follows speaker screen-space anchor, clamps away from corners/portrait. Item notifications have no pointer.

Palette: coral #FF6877, cyan #61D9D2, background #211925.
