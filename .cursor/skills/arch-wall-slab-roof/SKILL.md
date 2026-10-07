---
name: arch-wall-slab-roof
description: Compose envelope with create_wall / create_slab / create_roof.
---

# arch-wall-slab-roof
## Steps
1. `set_active_level`.
2. `create_wall` along path with thickness/height/wall_type_id/fire_rating.
3. `create_slab` / `create_roof` from closed boundaries.
4. Tag fire ratings; use `query_elements` to audit.

