# Inspect a solution without baking

Solve, poll `gh_get_job`, and retain its `result_id` and revision. `gh_read_output` returns bounded rows with explicit branch paths and item indices. Supply `expected_revision` when you need an additional revision guard. Empty branches and null items remain distinct.

Geometry rows include `valid`, `invalid_reason`, `geometry_type`, bounds, and `measurements`. Curves expose closedness, planarity, self-intersection, and length. Breps expose solid status, area, and volume for solids. Meshes expose closed/solid status; extrusions expose solid status. Measurements that cannot be computed are null and listed in `unsupported_measurements`. Null is not a successful check. Self-intersection is reported separately from Rhino's geometry validity flag.

Lengths and bounds use the bound Rhino document's model units. Area uses model units squared; volume uses model units cubed. The response includes the model unit name and absolute tolerance. Numeric graph outputs do not acquire units automatically: a number might be a count, angle, ratio, length, or another quantity. Give outputs meaningful names and document their units in the graph recipe.

For a report, call `gh_get_solution_summary`:

```json
{"session_id":"SESSION","expected_revision":4,"result_id":"RESULT","outputs":[{"name":"profile","node":"NODE","port":"out:0"}],"offset":0,"limit":50}
```

This returns `identity`, `session`, `units`, a page of graph controls/persistent-input samples/diagnostics, and selected output pages with total branch/item counts. Select 1–20 outputs with unique names. Each page contains at most 200 rows; `graph.next_offset` and each output's `data.next_offset` are independent. Fetch each page using the same revision/result guard; use `gh_read_output` to page a single output independently. Persistent inputs are bounded samples, not a complete dump of large trees.

Manual graph edits, undo/redo, source changes, document-unit changes, and another solve invalidate old results. Never combine pages or views from different result identities. Domain calculations belong in native graph components and appear as ordinary named outputs; there is no stair-specific measurement or compliance tool.
