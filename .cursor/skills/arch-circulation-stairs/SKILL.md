---
name: arch-circulation-stairs
description: Corridors, stairs, ramps with IRC/IBC geometry awareness.
---

# arch-circulation-stairs
## Steps
1. Model corridors as spaces; check clear width with `measure_clear_width`.
2. `create_stair` with total_rise and tread_count; inspect mcp4:riser/tread tags.
3. `create_ramp` — warn if slope steeper than 1:12.
4. `create_railing` where required by guards heuristics.
5. Run residential or accessibility suites after.

