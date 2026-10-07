---
name: arch-commercial-program
description: Commercial/mixed-use program: occupancy, OL factors, tenancy, fixture inputs.
---

# arch-commercial-program
## Steps
1. Confirm IBC occupancy group(s) and sprinklered flag via `set_code_context` / `set_project_info`.
2. Create spaces with `mcp4:occupancy_factor` (sf/person) from IBC Table 1004.5 heuristics.
3. Separate core vs tenant areas with clear names/tags.
4. After modeling, `run_code_checks suite=commercial_egress` for OL/exit heuristics.

