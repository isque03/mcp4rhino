# Debugging a parametric definition

Start with the first component with an error, inspect its upstream outputs and input ports, and repair that stage. Do not flatten every input or replace the whole graph as a default response.

| Symptom/code | Next action |
|---|---|
| revision_conflict | Inspect the definition; reconcile user changes; retry with the new revision and a new request ID. |
| request_conflict | The same request ID has different arguments. Use a new ID for the changed intent. |
| stale_result | Solve the current revision and use its new result ID. Never bake an old result. |
| invalid_port / node_not_found | Describe the component or inspect the graph; use returned identities. |
| connected_input | Disconnect sources before assigning persistent values, or change the upstream parameter instead. |
| component_authorization_required | Inspect the exact assembly identity and explain local trust requirements. Existing sliders can still be edited with a slider-only patch and solve:false. Preserve third-party nodes and connections. |
| restricted_component | Preserve and inspect the definition; do not replace or delete its restricted objects to force execution. Slider-only edits elsewhere remain available with solve:false. |
| missing_reference | Check the Rhino object and bound document. Restore or explicitly replace the reference. |
| busy | Poll the job. Avoid accumulating UI requests behind a long-running solve. |
| solver_disabled_or_busy | Wait for the current solution or let the user enable Grasshopper's solver; do not change the global solver flag. |
| document_mismatch | Activate the bound Rhino document; do not silently operate on another model. |
| session_faulted | Stop edits, preserve recovery information, and reopen the definition. |

For empty output, inspect missing required values, disabled nodes, nulls and expired references. For unexpected counts, compare input branch paths, access modes and list lengths. For twisted lofts, check profile ordering, direction and seam alignment before changing geometry. For invalid solids, check closure, orientation, tolerance and self-intersections.

For slow graphs, reduce sample counts first, avoid unnecessary cross products and repeated expensive geometry, batch changes and solve once, and read summaries before paging all results. Use reported component timings to locate the expensive stage. A solver returning successfully does not prove that every component succeeded.
