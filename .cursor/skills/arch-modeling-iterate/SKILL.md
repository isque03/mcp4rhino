---
name: arch-modeling-iterate
description: >-
  Orchestrates build-and-improve loops for MCP4Rhino architecture models until
  they match the user brief: bootstrap, envelope, openings, spaces, then
  coherence and visual review. Use when building or refining a house/fit-out
  until it looks right and is semantically consistent.
---

# arch-modeling-iterate

End-to-end modeling quality loop for agents. MCP: `http://127.0.0.1:4010/mcp`.

## Rules

- Confirm goal + jurisdiction/amendments once up front (default US ICC IRC or IBC).
- Stop only when `arch-model-visual-review` is `pass` **or** the user accepts residual findings.
- Never claim legal code compliance or RA stamp.

## Loop

1. **Goal** — Restate brief (program, stories, footprint, style). Ask if missing.
2. **Bootstrap** — Follow `arch-project-bootstrap` (units, layers, levels, code context, types).
3. **Program** — `arch-residential-program` or `arch-commercial-program` as appropriate.
4. **Massing / envelope** — `arch-massing-layout` then `arch-wall-slab-roof` (respect roof bearing rules).
5. **Openings & spaces** — `arch-openings-library`, `arch-spaces-tags`, `arch-circulation-stairs` as needed.
6. **Coherence** — Run `arch-model-coherence-pass`. Fix hard fails (levels, floating roof, hosts).
7. **Visual review** — Run `arch-model-visual-review` (captures + Read + measures).
8. **Iterate** — On `needs_fix`, apply MCP fixes and return to step 6 (cap visual fix loops per that skill).
9. **Handoff** — Optional `arch-lod-gate` then `arch-design-review-pack` for human RA.

## Done criteria

- Coherence checklist pass (or only user-accepted warnings).
- Visual review `pass`, or user explicitly accepts `needs_fix` leftovers.
- Brief restated and matched in the final verdict.
