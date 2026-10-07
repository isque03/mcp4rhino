---
name: arch-lod-gate
description: >-
  LOD 200/300 readiness checklist for MCP4Rhino models before advancing phase
  or packaging for review. Use before design-review pack, consultant handoff,
  or claiming schematic/DD geometry is ready.
---

# arch-lod-gate

## Checklist before calling the phase “LOD 300 ready”

- [ ] Levels + project info set
- [ ] Walls/slabs/roofs typed and leveled (roof bearing vs wall tops OK)
- [ ] Doors/windows hosted with sizes
- [ ] Spaces named with areas
- [ ] Stairs/ramps parameterized
- [ ] **`arch-model-coherence-pass` = pass**
- [ ] **`arch-model-visual-review` = pass** (or user-accepted residuals)
- [ ] Sheets or named views for plan/section/elevation
- [ ] Code pre-check run (findings reviewed — not “compliant”)

Fail the gate if any box is unchecked. Do not claim BIMForum LOD certification.

## Next

`arch-design-review-pack` for human RA handoff.
