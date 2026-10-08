---
name: arch-ifc-export
description: Validate hierarchy and export IFC for consultants.
---

# arch-ifc-export
## Steps
1. `get_building_model` — require levels + tagged elements.
2. `export_ifc` to Logs or project path.
3. Note: v1 IFC uses bounding boxes; communicate LOD limits to consultants.
4. Optional: `check_rhino_license` then `export_3dm` companion (only when `can_save` is true).

