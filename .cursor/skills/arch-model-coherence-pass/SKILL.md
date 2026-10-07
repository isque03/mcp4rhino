---
name: arch-model-coherence-pass
description: >-
  Non-visual semantic gate for MCP4Rhino models: levels, tags, roof vs wall
  heights, hosted openings, orphans, and optional clash checks. Use before
  screenshots, visual review, or claiming the building graph is consistent.
---

# arch-model-coherence-pass

Run this **before** `arch-model-visual-review`. MCP: `http://127.0.0.1:4010/mcp`.

## Rules

- Fail closed: any hard fail → do not call the model “coherent”; send findings into visual review / fix.
- Never claim legal code compliance.

## Procedure

1. `get_document_units` — record unit system (Feet vs Millimeters matters for heuristics).
2. `get_building_model` — levels sorted by elevation; note `element_counts` per level.
3. `query_elements` (all or by kind) — build a working set of tagged objects.

## Checklist

Mark each `[x]` / `[ ]`. Hard fails block a pass.

### Levels and envelope

- [ ] Every wall / slab / roof / space has `mcp4:level` (and level exists in the level list).
- [ ] Levels are sorted by elevation; story heights are plausible (no negative gaps).
- [ ] **Roof bearing:** for each roof, roof level elevation ≤ max wall top on the story below **or** intermediate story walls exist. (Wall top ≈ level elevation + `mcp4:height` or wall bbox max Z.)
- [ ] If Roof elevation ≫ wall tops with empty upper levels → **hard fail** (classic floating roof).

### Spaces and hosts

- [ ] Spaces have names + `mcp4:area` (or measurable boundary); no duplicate accidental copies of the same program outside the site.
- [ ] Doors/windows have host wall linkage (`host_wall_id` in tool result and/or openings listed on the host wall tags).
- [ ] Stairs/ramps have level + rise/run tags when present; note if no slab/ceiling above the run.

### Leftovers and clashes

- [ ] No obvious smoke-test / orphan objects far outside the primary building bbox (flag for delete or layer hide).
- [ ] When wall + major element IDs are known, optional `clash_detect` (hard or soft clearance); record hits as warnings unless clearly wrong.

## Output

```markdown
## Coherence: <pass|fail>
**Units:** …
**Levels:** …

| Check | Status | Notes |
|-------|--------|-------|
| Roof bearing | pass/fail | … |
| … | … | … |

**Next:** arch-model-visual-review (even if fail — screenshots help confirm)
```
