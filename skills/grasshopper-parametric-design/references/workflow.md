# MCP graph workflow and value contract

Every mutation requires a `request_id`. Reuse it only for an exact retry with identical arguments; use a new ID for a changed operation. Receipts live in the runtime until Rhino exits. Mutations against sessions also require `expected_revision` from the latest response/inspection. Never guess the next revision after manual canvas edits.

`gh_apply_patch` arguments are `session_id`, `expected_revision`, `request_id`, `operations`, and optional `solve` (default false). Maximum 100 operations. The returned `patch.aliases` maps temporary aliases to placed node IDs; retain those IDs for later edits. A requested solve returns a job under `job`.

Operations:

- `add`: alias, component_id, optional name and position [x,y].
- `remove`: node (placed ID or current-patch alias).
- `connect` / `disconnect`: source and target, each {node,port}. Use discovered `out:N` and `in:N` ports. Source ordering is preserved.
- `set_slider`: node, value, optional minimum/maximum/integer. Values must lie within bounds; integer sliders require integral values.
- `set_value`: node, input port, tree [{path,items}]. Disconnect any sources first.
- `set_properties`: node, optional name/position/preview/enabled.
- `set_mapping`: node, input port, optional mapping (none/flatten/graft), simplify, reverse.
- `layout`: arrange the entire graph by dependency with no node argument. Place it last in a batch to frame a multi-batch graph coherently. It is revision-checked and part of the same undo record as the rest of the patch.

Primitive persistent values are JSON numbers, integer numbers, booleans, strings, or null. Geometric values are tagged:

```json
{"type":"point","value":[0,0,0]}
```

Use type vector with three coordinates; type interval with two numbers. A plane has type plane and tagged point `origin`, tagged vector `x_axis`, and tagged vector `y_axis`. Curve/surface/Brep/mesh parameters accept {"type":"rhino_ref","id":"<Rhino object GUID>"}. These are live references; replacing/deleting a source invalidates outputs.

Patches validate in a staging document and commit as one undo record. Invalid patches do not partially apply. A solver error after a committed patch leaves the graph editable. V1 supports the native types marked `supported:true`; it cannot execute arbitrary plugin/script/cluster/timer graphs.

`gh_solve` returns a job ID. Poll `gh_get_job` until completed, cancelled, timed_out, or failed. Queue cancellation is immediate; running cancellation/timeout is cooperative and may wait for the current component. Rhino UI can block during expensive solves. Do not submit parallel canvas operations while a solve is pending.

Outputs expire on edits, manual solutions, reference changes, or another explicit solve. A result ID authorizes reading only that current solution. To bake, pass current session/revision/result, outputs [{node,port}], and optional existing layer/name. Create a missing layer through Rhino's `create_layer` tool first. Each distinct bake request creates an additive snapshot; prior baked objects remain unchanged.

Save with `gh_save_document` and an absolute .gh/.ghx path; overwriting requires overwrite:true. Closing an owned modified definition requires saving or discard:true. Closing an attached session only detaches MCP.
