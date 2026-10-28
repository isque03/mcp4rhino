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

Slider-only `set_slider` batches prevalidate every operation and edit controls in place, including definitions containing untrusted third-party or restricted objects. Use `solve:false` for those definitions. Other patches validate in a staging document and require every graph object to be authorized before any serialization. Patches commit as one undo record. Invalid patches do not partially apply; undo/redo invalidates outputs without automatically solving. A solver error after a committed patch leaves the graph editable.

Discovery distinguishes `support_level: verified`, `available`, and `restricted`. Legacy `supported:true` is a verified compatibility badge, not the execution permission flag. Use `authorized:true` when selecting a component to create or solve. Built-in modeling components such as Interpolate, Pipe Variable, Boolean Toggle and Custom Preview work automatically, including when marked available.

For third-party plugins, ask the user to open **Tools → MCP4Rhino → Grasshopper plugins…** (or run `MCP4RhinoGrasshopper`), select the plugin, and click **Apply**. This is a normal Rhino preferences dialog; no environment variables or file edits are needed. Choices persist and changes apply immediately. Cancel leaves permissions unchanged. Agents cannot grant themselves permission through MCP tool arguments. Known script/cluster/timer and I/O restrictions remain in effect. A different plugin version requires a new choice. Permission covers available components in the selected assembly, including constructors, serialization, solving and callbacks; it is not a sandbox.

Type-level port discovery instantiates components and therefore checks authorization; existing-instance inspection uses already-loaded ports. Authorization does not add a custom persistent-value codec: unsupported special control/value assignment still fails explicitly, and existing native Boolean/Number/etc. parameters remain valid input sources.

For sliders, `value` in `set_slider` is raw; `settings.value` may include an expression and `settings.raw_value` exposes the underlying control. Omitted `integer` preserves the existing accuracy, including Even/Odd. Expressions and decimal places are preserved. Save/reopen of plugin definitions and private-state preservation still need native acceptance.

`gh_solve` returns a job ID. Poll `gh_get_job` until completed, cancelled, timed_out, or failed. Queue cancellation is immediate; running cancellation/timeout is cooperative and may wait for the current component. Rhino UI can block during expensive solves. Do not submit parallel canvas operations while a solve is pending.

Outputs expire on edits, manual solutions, reference changes, or another explicit solve. A result ID authorizes reading only that current solution. To bake, pass current session/revision/result, outputs [{node,port}], and optional existing layer/name. Create a missing layer through Rhino's `create_layer` tool first. Each distinct bake request creates an additive snapshot; prior baked objects remain unchanged.

Save with `gh_save_document` and an absolute .gh/.ghx path; overwriting requires overwrite:true. Closing an owned modified definition requires saving or discard:true. Closing an attached session only detaches MCP.
