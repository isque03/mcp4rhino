---
name: arch-residential-program
description: Turn a residential program brief into tagged spaces with IRC size heuristics.
---

# arch-residential-program
## Steps
1. Collect program (beds, baths, kitchen, living, garage, etc.).
2. For each room `create_space` with closed boundary, name, height, level, program, occupancy_factor.
3. Target IRC heuristics: bedrooms ≥70 sf and ≥7 ft min dimension; ceiling ≥7 ft (`mcp4:height`).
4. Use `measure_area` / `query_elements kind=space` to verify.
5. Do not assert code compliance — flag shortfalls for human review.

