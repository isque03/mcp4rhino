---
name: arch-spaces-tags
description: Close spaces, names, finishes, fire/egress tags.
---

# arch-spaces-tags
## Steps
1. Ensure every room has `create_space` with name/program/area/height.
2. `update_space` for occupancy_factor and program.
3. `set_user_text` for finishes / fire / egress metadata.
4. `get_building_model` to verify graph.

