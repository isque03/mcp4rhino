---
name: arch-project-bootstrap
description: Bootstrap an architectural Rhino project (units, levels, layers, code context, types).
---

# arch-project-bootstrap
## When
Starting any residential/commercial project in MCP4Rhino.
## Steps
1. Confirm jurisdiction + amendments with the user (default US ICC: IRC or IBC edition).
2. `set_document_units` (Feet or Millimeters as appropriate).
3. `apply_layer_standard`.
4. `set_project_info` (name, address, code_edition, occupancy, construction_type, sprinklered).
5. `set_code_context` (family IRC|IBC, edition, occupancy, sprinklered, jurisdiction_notes).
6. `create_level` for each story (+ Roof); `set_active_level` to L1.
7. Optional: `create_grid`, `create_site`, `create_building`.
8. `upsert_element_type` for common wall/door/window types.
## Rules
Never claim legal compliance. Ask for local amendments before code checks.

