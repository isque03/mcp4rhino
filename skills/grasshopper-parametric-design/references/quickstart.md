# Quickstart

Call tools with JSON objects (the examples below show tool arguments).

1. `gh_status({})`, `get_document_units({})`. If Grasshopper is not initialized, discovery/open will load its editor. Help does not load it.
2. `gh_open_document({"mode":"create","name":"Study","request_id":"study-open-1"})`. Keep the returned `session_id` and `revision`.
3. `gh_search_components({"query":"Series"})`. Choose an entry with `supported:true`, then `gh_describe_components({"component_ids":["<returned component_id>"]})`.
4. Read input/output port descriptions and defaults; input access may be item, list, or tree. Type GUIDs create components; placed node GUIDs identify instances.
5. Submit a `gh_apply_patch` using the session and exact revision. New nodes can be referenced by aliases inside the same patch. See workflow or `gh_help({"topic":"examples/array"})`.
6. Use the returned revision for `gh_solve` with a new request ID. Poll `gh_get_job({"job_id":"<returned job_id>"})` until terminal. `completed` means the solver returned: inspect component diagnostics too.
7. Use the returned `result_id`, placed node ID and output port with `gh_read_output`. Follow `next_offset` until null for all rows.

To edit an existing definition: list documents, explicitly attach its returned `document_id`, and inspect it. Do not assume the active canvas is the user's intended target. A session binds to the Rhino document active when it was opened; activate that document for geometry operations.

New/opened definitions initially remain disabled to avoid accidental file execution. An explicit supported solve enables their previews and solver events. Unsupported definitions can be inspected but cannot be patched or solved through v1.
