---
name: arch-design-review-pack
description: >-
  Bundles viewport captures, code pre-check report, schedules, and IFC for a
  licensed architect. Use when packaging a design-review folder after modeling
  and visual review, never as a permit seal.
---

# arch-design-review-pack

## Rules

Agent output is **design assistance only**. A licensed design professional must review before permit submission. Never claim legal code compliance.

## Steps

0. **Gate:** Run `arch-model-coherence-pass` and `arch-model-visual-review`. Proceed only on pass or explicit user acceptance of residuals.
1. `zoom_extents` / framed `set_view` + `capture_viewport` for key views (reuse visual-review paths when fresh).
2. `run_code_checks` `suite=all` → keep JSON/MD paths under Logs.
3. `create_schedule` for doors/windows/rooms as available; `export_images`; `export_ifc`.
4. Hand the folder under Logs/MCP4Rhino (captures, code report, IFC) to the licensed architect.
5. State explicitly: pre-check findings only — not an RA stamp.
