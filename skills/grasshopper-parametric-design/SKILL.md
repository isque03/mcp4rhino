---
name: grasshopper-parametric-design
description: Create, edit, inspect, and debug native Grasshopper definitions through MCP4Rhino. Use for reusable parametric geometry, parameter relationships, and Grasshopper data trees; ordinary one-off Rhino edits can use the existing geometry tools.
---

# Grasshopper parametric design

Build a definition that preserves the user's design relationships and remains editable in Grasshopper. Start with `gh_status` and `get_document_units`. MCP help is available as `gh_help({"topic":"index"})` even without this skill installed.

Clients supporting MCP resources and prompts can discover read-only context and reusable workflows. Read [resources](references/resources.md) for canonical URIs, active-document scope, bounded reads, privacy, freshness and prompt behavior. Resources never initialize or mutate a definition merely by being read.

1. Identify independent parameters, units, derived quantities, constraints, and measurable outputs. Read [design](references/design.md) when translating a brief into a graph.
2. Use a separate visible definition by default. Inspect before explicitly attaching an existing document. Read [quickstart](references/quickstart.md) for discovery and session setup.
3. Search the actual installed component catalog and describe selected types. Use returned type GUIDs and ports; names are not identities. Third-party, scripting, timer, and cluster execution are outside v1.
4. Build a small working graph with temporary aliases. Keep patches within 100 operations; for larger recipes, retain returned GUIDs between batches and solve after the final batch. Keep meaningful names and parameter bounds. Read [workflow](references/workflow.md) for the wire contract and mutation rules.
5. Solve once, poll its job, and inspect errors, output counts, branch structure, bounds, and user-unit dimensions. Read [data trees](references/data-trees.md) before changing mappings or connecting lists.
6. Change one independent parameter and verify that the intended relationships hold. Read [inspection](references/inspection.md) for structured solution summaries and [capture](references/capture.md) for native canvas images and revision-associated Rhino views. A green component or attractive image alone does not establish dimensional correctness.
7. Save or bake when requested. Preview is transient; baking creates additive Rhino objects and does not update old snapshots.

When a stage fails, find the first failing component and inspect its inputs before rebuilding anything. See [debugging](references/debugging.md).

## Worked examples

- [Adjustable array](references/array.md): scalar controls, a series, vectors, and transformed geometry.
- [Parametric facade](references/facade.md): derived panel spacing/width, orientation, dimensions, and baking.
- [Branch-preserving loft](references/loft.md): paired trees, curve ordering, and deliberate flatten/repair.
- [Ordered profiles](references/profile.md): separate point branches, closed polylines, extrusion, and capping.
- [Stair study](references/stair.md): a regression example composing generic native components; no staircase-specific server tool.

The JSON recipes in `assets/` are templates. Resolve `$component` tokens through discovery before submitting their patch. `gh_help` serves the same references and recipes to clients without filesystem skills. The server never installs a client skill automatically.
