# Getting started

Walk a new user through a first Rhino session. Ask before you create or change geometry. Reading this resource does not mutate the model.

## Local tutorial memory

Before starting, look for `.mcp4rhino/tutorial-memory.md` in the user’s workspace (create `.mcp4rhino/` if needed).

- If `getting_started` is already marked complete, **do not** re-run the full practice-box walkthrough unless the user asks to redo it. Summarize that they finished it, offer next lanes (architecture, drawings, or `grasshopper_getting_started`), and continue with their ask.
- After the user finishes this tutorial (practice box measured and screenshot shown, or they explicitly skip/finish), **update** that file so future sessions remember it.

Suggested file shape (Markdown table; keep it short; no secrets, paths to private models, or license keys):

```markdown
# MCP4Rhino tutorial memory

| id | status | completed_at_utc | notes |
|---|---|---|---|
| getting_started | complete | 2026-10-27T21:00:00Z | Practice box; mm |
```

Only mark complete when the core steps are done or the user clearly declines the practice path.

## Say this early

In the first reply, name the main lanes so Grasshopper is not invisible:

- **Direct Rhino modeling** — geometry, architecture, drawings, checks, and exports without Grasshopper.
- **Grasshopper** — editable parametric definitions when the user wants a graph they can keep changing. Offer the `grasshopper_getting_started` prompt or `mcp4rhino://server/grasshopper-getting-started`. Do not open Grasshopper for ordinary one-off edits.

## Steps

1. Confirm Rhino is open and MCP4Rhino is running. Read `mcp4rhino://rhino/documents/active`, or call `get_document_info` and `get_document_units`.
2. Tell the user the document unit system and tolerance. If the user speaks other units, call `convert_length` before any create tool.
3. Ask permission to create a small practice box. After they agree, call `create_box` with clear dimensions in model units.
4. Call `measure_size` on the new object. Call `zoom_extents` if needed. Call `capture_viewport` and **show the image in the chat harness** (return the MCP image content so the user sees it without leaving the assistant). Prefer a clear Perspective or Top view.
5. Update `.mcp4rhino/tutorial-memory.md` to mark `getting_started` complete.
6. Stop. Ask what they want next:
   - Architecture: levels, walls, spaces (`create_level`, `create_wall`, …)
   - Drawings: sections, dimensions, sheets
   - Grasshopper tutorial: `grasshopper_getting_started` or `mcp4rhino://server/grasshopper-getting-started` (unless already complete in tutorial memory)
7. Do not save, export, delete, or overwrite Rhino/Grasshopper files unless the user asks. Resources and prompt retrieval alone never mutate the model or initialize Grasshopper. Writing tutorial memory is allowed.

## Screenshots in the harness

After create or transform steps that change what the user should see, capture and display a viewport image in the assistant UI. Do not only say that you captured a file on disk. If the image is omitted for size, say so and offer a smaller capture.

## Style

Narrate in clear English using `mcp4rhino://server/writing-style`. Keep tool names exact.
