---
name: arch-code-check-residential
description: Run IRC residential pre-checks and fix loop.
---

# arch-code-check-residential
## Steps
1. Confirm jurisdiction; `set_code_context family=IRC`.
2. `run_code_checks suite=residential_planning`.
3. Fix fails (space sizes, stair geometry, doors) then re-run.
4. `get_code_report` + `capture_viewport` evidence.
5. Never say the design is code compliant — deliver findings for RA review.

