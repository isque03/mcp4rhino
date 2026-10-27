# Getting started

Walk a new user through a first Rhino session. Ask before you create or change geometry. Reading this resource does not mutate the model.

## Steps

1. Confirm Rhino is open and MCP4Rhino is running. Read `mcp4rhino://rhino/documents/active`, or call `get_document_info` and `get_document_units`.
2. Tell the user the document unit system and tolerance. If the user speaks other units, call `convert_length` before any create tool.
3. Ask permission to create a small practice box. After they agree, call `create_box` with clear dimensions in model units.
4. Call `measure_size` on the new object. Call `zoom_extents` if needed. Call `capture_viewport` so they can see the result.
5. Stop. Ask what they want next:
   - Architecture: levels, walls, spaces (`create_level`, `create_wall`, …)
   - Drawings: sections, dimensions, sheets
   - Grasshopper: only if they want an editable parametric definition — then `gh_help(topic: index)` or `mcp4rhino://grasshopper/guides/index`
6. Do not save, export, delete, or overwrite unless the user asks. Resources and prompt retrieval alone never mutate the model or initialize Grasshopper.

## Style

Narrate in clear English using `mcp4rhino://server/writing-style`. Keep tool names exact.
