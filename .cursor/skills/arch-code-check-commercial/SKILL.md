---
name: arch-code-check-commercial
description: Run IBC egress/OL/fixture pre-checks.
---

# arch-code-check-commercial
## Steps
1. `set_code_context family=IBC` with occupancy + sprinklered.
2. Tag exit doors `mcp4:egress=true`.
3. `run_code_checks suite=commercial_egress`.
4. Address exit count / OL issues; travel distance remains manual.
5. Package report for licensed review — no compliance claims.

