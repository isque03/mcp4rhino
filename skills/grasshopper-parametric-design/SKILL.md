---
name: grasshopper-parametric-design
description: Create, edit, inspect, and debug native Grasshopper definitions through MCP4Rhino. Use for reusable parametric geometry, parameter relationships, and Grasshopper data trees; ordinary one-off Rhino edits can use the existing geometry tools.
---

# Grasshopper parametric design

Build a definition that preserves the user's design relationships and remains editable in Grasshopper. Start with `gh_status` and `get_document_units`. MCP help is available as `gh_help({"topic":"index"})` even without this skill installed.

1. Identify independent parameters, units, derived quantities, constraints, and measurable outputs. Read [design](references/design.md) when translating a brief into a graph.
2. Use a separate visible definition by default. Inspect before explicitly attaching an existing document. Read [quickstart](references/quickstart.md) for discovery and session setup.
3. Search the actual installed component catalog and describe selected types. Use returned type GUIDs and ports; names are not identities. Third-party, scripting, timer, and cluster execution are outside v1.
4. Build a small working graph in one patch with temporary aliases. Keep meaningful names and parameter bounds. Read [workflow](references/workflow.md) for the wire contract and mutation rules.
5. Solve once, poll its job, and inspect errors, output counts, branch structure, bounds, and user-unit dimensions. Read [data trees](references/data-trees.md) before changing mappings or connecting lists.
6. Change one independent parameter and verify that the intended relationships hold. Use `capture_viewport` and inspect the embedded image when available. A green component or attractive image alone does not establish dimensional correctness.
7. Save or bake when requested. Preview is transient; baking creates additive Rhino objects and does not update old snapshots.

When a stage fails, find the first failing component and inspect its inputs before rebuilding anything. See [debugging](references/debugging.md).

## Worked examples

- [Adjustable array](references/array.md): scalar controls, a series, vectors, and transformed geometry.
- [Parametric facade](references/facade.md): derived panel spacing/width, orientation, dimensions, and baking.
- [Branch-preserving loft](references/loft.md): paired trees, curve ordering, and deliberate flatten/repair.

The JSON recipes in `assets/` are templates. Resolve `$component` tokens through discovery before submitting their patch. `gh_help` serves the same references and recipes to clients without filesystem skills. The server never installs a client skill automatically.
