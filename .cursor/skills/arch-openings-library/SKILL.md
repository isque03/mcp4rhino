---
name: arch-openings-library
description: Place doors/windows with host cuts and type library.
---

# arch-openings-library
## Steps
1. `upsert_element_type` for door/window types (width, height, clear_width, fire_rating).
2. `create_door` / `create_window` with host_wall_id, width, height, sill, type_id.
3. Verify openings with `capture_viewport` / `get_geometry`.
4. Tag egress doors `mcp4:egress=true` when they are exits.

