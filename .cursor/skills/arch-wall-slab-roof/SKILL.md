---
name: arch-wall-slab-roof
description: >-
  Composes building envelope with create_wall, create_slab, and create_roof,
  including roof-bearing elevation rules. Use when modeling exterior/interior
  walls, floors, ceilings, or roofs in MCP4Rhino.
---

# arch-wall-slab-roof

## When

Building or revising the envelope after levels exist (`arch-project-bootstrap`).

## Roof bearing rule (required)

- Set **Roof** level elevation to the **top of the bearing walls** (typically L1 elevation + wall height for a 1-story; or top of L2 walls for 2-story).
- Do **not** place Roof at a vacant upper story elevation unless you also create walls on that story.
- After create: `query_elements` kind `wall`/`roof` and compare bboxes — roof min Z should meet wall max Z (small eave gap OK; empty-story gaps are not).

## Steps

1. `set_active_level` for the story you are building.
2. `create_wall` along `path` with thickness / height / `wall_type_id` / `fire_rating` / level.
3. `create_slab` from closed `boundary` on the floor level (and ceiling slab on the level above if needed).
4. Ensure Roof level elevation matches wall tops; then `create_roof` from boundary (+ optional `slope_deg`).
5. Tag fire ratings; `query_elements` to audit kinds, levels, and bboxes.

## Next

`arch-model-coherence-pass` → `arch-model-visual-review`.
