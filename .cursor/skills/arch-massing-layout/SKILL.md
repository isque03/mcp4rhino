---
name: arch-massing-layout
description: Massing, setbacks, story stacking, grid.
---

# arch-massing-layout
## Steps
1. `create_site` boundary; note setbacks as tags or offset curves (`offset_curve`).
2. `create_grid` for structure.
3. Stack levels; footprint polylines per level; `create_slab` at each level.
4. Keep massing on A-SITE / A-FLOR until walls are detailed.

