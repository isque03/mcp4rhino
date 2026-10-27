# MCP4Rhino: Grasshopper capability specification for parametric stair studies

**Audience:** MCP4Rhino maintainers and Grasshopper integration developers  
**Baseline observed:** MCP4Rhino 0.6.2-alpha, Rhino 8 on macOS; Streamable HTTP endpoint `/mcp`  
**Purpose:** Enable an MCP client to create, inspect, vary, validate, visualize, and save an editable native Grasshopper definition for a detailed residential stair study.  
**Status:** Proposed requirements; not a claim that every underlying Grasshopper feature is unavailable.

## 1. Problem and desired outcome

An agent built a Grasshopper definition with sliders, arithmetic, series, domains, rectangles, vectors, extrusion, and transforms. It generated 15 tread objects but could not produce a continuously notched stringer through the server's supported Grasshopper component catalog. A more realistic fixed Rhino model was then created directly through Rhino tools; that model is **not parametrically linked** to the Grasshopper sliders and therefore does not meet the variation workflow.

The intended result is a native, visible, editable Grasshopper graph whose inputs drive all of its outputs. Changing finished floor-to-floor height, riser count, going, width, or material thickness must update the tread boards, riser boards, and stringer geometry together. The client must be able to verify the outputs, capture the Grasshopper canvas and Rhino viewport, and retrieve structured dimensions and diagnostic checks for a drawing/report workflow.

A screenshot or fixed Rhino geometry alone is not an acceptable substitute for an editable parametric definition. The example is a design study, not a request for the server to certify structural adequacy or code compliance.

## 2. Verified baseline and limits of the observations

On the stated installation, these capabilities were exercised successfully:

- `gh_help`, `gh_status`, `gh_search_components`, `gh_describe_components`, `gh_open_document`, `gh_apply_patch`, `gh_get_job`, `gh_inspect_document`, `gh_read_output`, `gh_save_document`, and `gh_bake`.
- Supported catalog entries included Number Slider, Addition, Subtraction, Multiplication, Division, Series, Construct Point, Construct Domain, Rectangle, Vector XYZ, Move, Extrude, and Loft. Availability and port identifiers must continue to be discovered on the installed version.
- Patch operations can add components, connect ports, set primitive or tagged persistent data, set sliders and component properties, and modify input mapping. Mutations use request IDs and expected revisions; solving is asynchronous.
- The original tread graph solved without diagnostics, returned 15 outputs, and was baked to 15 Rhino objects. `capture_viewport` captured Rhino's Perspective view; it is not a Grasshopper canvas capture.
- `gh_search_components` searches for `polyline`, `join`, and `shift list` returned no **supported** matches. This is a catalog observation for this installation, not proof that Grasshopper or RhinoCommon lacks these operations, nor proof that no alternate graph can achieve them.
- Attempting `set_value` on the Subtraction component's Generic Data input returned `unsupported_value` ("Persistent values for Generic Data are unsupported"). A constant slider connected to the input was a workable alternative.
- Geometry created directly with Rhino `create_polyline` and `extrude_curve` worked, including capped solid output. This does not make that geometry a live Grasshopper definition.
- `gh_open_document` created a definition successfully, but the editor was not initially in the foreground. Opening Grasshopper via the UI showed an onboarding dialog covering the canvas. We did not find a dedicated canvas capture or framing capability in the inspected MCP tool list.

Other capabilities may exist under different names, in a different server version, or via an untested workflow. The developer should confirm discovery results and underlying Rhino/Grasshopper APIs before implementation. The issue is principally support and exposure through this server, not a demonstrated limitation of Rhino or Grasshopper itself.

## 3. Priority 0: construct a parametric closed stringer profile

### 3.1 Native Grasshopper component support

Expose and support, through the existing discovery/describe/patch workflow, the installed native components needed to:

1. Construct a point for each ordered corner of a side-elevation profile.
2. Combine/interleave point lists while preserving a documented branch and item order.
3. Build an open or closed polyline from an ordered point list. Explicitly support closure and validate that the result is a planar closed curve when intended.
4. Create a planar face and/or a capped extrusion from that closed curve along a specified width vector.
5. Copy or locate several stringers across the stair width, with their positions driven by graph parameters.

The developer should identify the actual Grasshopper component GUIDs via runtime discovery and support those components; do not rely on names or hard-coded GUIDs supplied by the client. If multiple native components or combinations can perform the operation, document one supported recipe. Do not require a custom script component merely to work around a missing standard component.

**Acceptance:** A graph built entirely through `gh_apply_patch` produces a single closed, planar, non-self-intersecting stringer outline and a valid capped 3D stringer. It remains visible and editable in the Grasshopper canvas and updates on parameter changes. The resulting geometry must be graph-produced, not a snapshot imported from Rhino.

### 3.2 Data tree and point-list operations

Provide supported, discoverable components or equivalent graph operations for the actual list manipulation needed above: interleave/merge, append/prepend, list indexing/shift, and branch construction where applicable. A single equivalent standard workflow is acceptable; the exact component inventory is an implementation choice.

Specify semantics for:

- Branch paths, order and item order, including whether operations preserve, graft, flatten, simplify, reverse, or broadcast values.
- Matching input lists of different lengths and handling empty branches.
- Explicit point tags and numerical inputs, reasonable size limits, and error messages for invalid data.
- Ensuring that the closing point is handled deterministically without duplicated or zero-length segments.

**Acceptance:** A two-branch fixture produces two separate closed outlines with predictable point order; a bad or self-intersecting profile produces a clear diagnostic and does not silently create misleading geometry.

### 3.3 Native RhinoCommon implementation option

If native Grasshopper catalog support is not feasible initially, an alternative is an explicitly supported, discoverable **parametric Grasshopper component** that constructs the profile from typed inputs and updates during Grasshopper solutions. It must create Grasshopper geometry, remain part of the visible saved graph, support data trees, and report validation errors. A one-shot MCP command creating a Rhino object is insufficient.

RhinoCommon's `Rhino.Geometry.PolylineCurve(IEnumerable<Point3d>)` is one available underlying API for a polyline; a closed profile must explicitly close the point sequence and be validated. A suitable planar-surface/capped-extrusion API may then create the body. `GH_Document.AddObject` is one documented API for inserting a document object. These are starting points, not a prescriptive implementation or a guarantee that a particular Grasshopper version or hosting context exposes the same behavior. Avoid arbitrary code execution as a requirement for this feature.

## 4. Priority 1: model a coordinated staircase

The supported recipe or example definition should expose:

- Finished lower-to-upper floor height; integer riser count; tread going, measured consistently between successive nosings; overall tread width.
- Tread thickness, riser-board thickness, tread projection/nosing, stringer width and number/placement, and sufficient parameters for the lower and upper support conditions. Clearly identify independent inputs and derived values.
- Derived outputs for exact riser height, number of intermediate treads, total horizontal run, actual tread board depth, overall width, representative stringer bounds, and component counts.
- Separate graph outputs for tread boards, riser boards, stringer profiles/solids, and optionally landings/handrails/guards. They should be inspectable and selectable independently for preview and bake.
- Clear definitions of coordinate origin, run/rise direction, finished-floor datums, whether the upper floor counts as the final riser, and how tread thickness affects bottom/top stringer cuts. Do not conflate the 11-inch going, physical board depth, and nosing projection.
- Checks for invalid or unbuildable input combinations: zero/negative dimensions, too few risers, invalid profile, a stringer that self-intersects or has insufficient modeled material, and unsupported configuration. Structural design cannot be inferred from a geometry validity check.

A parametric handrail, guards, landings, building opening and headroom context are desirable subsequent features. Until modeled and evaluated, outputs and reports must identify them as unresolved. Provide labels or user-visible diagnostics explaining such omissions.

## 5. Priority 1: inspection, validation, and reporting data

### 5.1 Structured output

Extend existing read/inspect behavior as needed so a client can retrieve, without baking, paginated structured values and useful geometry summaries: branch paths, count, geometry type, validity, closed/solid status where applicable, bounding box, model units, and relevant diagnostic messages. For a selected set of outputs, expose named measurements and explicit units (mm/in as appropriate). Geometry should remain in Rhino/Grasshopper unless explicitly exported.

Provide a deterministic graph-level summary suitable for a report: input values and slider limits, derived values, output counts, definition/session/revision, solution/result ID, units, errors/warnings, and references to captured views. The report generator must not silently combine inputs, measurements, views, or geometry from different graph revisions.

### 5.2 Rule checks and code context

Consider a **separate, versioned, configurable** rule/check layer for common numerical checks; do not hard-code a claim of compliance into a geometry tool. Each check should return a rule identifier, applicability assumptions, source/code edition, measured value and units, threshold and units, pass/fail/unknown, and an explanation. Missing required context must be `unknown`, not `pass`. Record when the ruleset was verified and require jurisdiction/permit context.

For the illustrative Massachusetts residential example, the 10th-edition Massachusetts amendment to R311.7.5.1 specifies a maximum riser height of 8 1/4 in; R311.7.5.2 specifies a minimum tread depth of 9 in. These are **only two requirements**, not a complete code checklist. [Massachusetts 780 CMR amendment](https://www.law.cornell.edu/regulations/massachusetts/780-CMR-CHAPTER-51-ch-3-R301-301-1-311.7). Verify applicability, current edition, measurements, exceptions, and all other requirements with the authority having jurisdiction. Do not treat a passed numerical check as a legal or structural compliance determination.

## 6. Priority 1: capture and visualization

Add a dedicated Grasshopper canvas capture capability, for example `gh_capture_canvas`, separate from Rhino `capture_viewport`. Proposed contract:

- Input: session/document ID; optional expected revision; requested pixel dimensions and output destination; optional explicit fit-to-contents or a supplied canvas view rectangle.
- Output: actual dimensions, MIME type, path or MCP image block, captured session/document ID, revision, and framing details; report truncation or unsupported dimensions explicitly.
- Default to read-only capture; fitting, changing active document, and changing viewport state must be explicit options with documented effects. Do not switch or modify another user's document silently.
- Handle an uninitialized editor, hidden window, onboarding dialogs, offscreen content, empty document, large graphs, and platform differences with clear errors. Do not assume that a Rhino viewport screenshot includes Grasshopper.
- Preserve the user's canvas view unless explicitly asked to change it, or restore it afterwards.

Allow clients to request Rhino viewport views appropriate for a model study and associate captures with a solution/revision. Include support for shaded/illustrative display and view framing; `capture_viewport` already exists. A pixel capture is an illustration, not proof of dimensions or a photorealistic render.

**Acceptance:** An agent creates a graph while Grasshopper is initially hidden, requests a fitted canvas capture, and receives a legible image containing the expected sliders, components and wires without a tutorial dialog obscuring it. Verify behavior on supported operating systems.

## 7. Priority 2: graph ergonomics and safe lifecycle

- Make numeric/boolean/text constants practical on Generic Data inputs, either by supporting appropriately typed persistent data with clear coercion rules or by documenting a native constant parameter pattern. Reject incompatible types with actionable messages; avoid silently changing types.
- Provide clear APIs or recipes for named panels, annotations/groups, display labels, graph layout, fit-to-contents, and optional showing/activating the intended Grasshopper document. Avoid requiring direct UI automation for basic graph inspection.
- Preserve the current revision, request-ID idempotency, validation-before-commit, bounded pagination, asynchronous solve, and diagnostic conventions. A failed patch should not partially modify the graph.
- Define how manual canvas edits invalidate results/revisions and how clients recover. Never assume the revision remains unchanged after a user action.
- Preserve the distinction between transient preview and explicit bake. A bake is an additive snapshot and should report created object IDs; document how to replace a previous snapshot without accidentally deleting user geometry.
- Provide a clear, explicit save/open path and collision handling; avoid overwriting a user's document by default. Do not require background services or external network access to produce a local definition.

## 8. End-to-end acceptance scenario

Use a fresh document in millimeters. All inputs and values below are illustrative and not a construction specification.

1. Discover components and port IDs on the running installation; create a separate Grasshopper definition through MCP.
2. Set floor-to-floor height **2,880 mm**, riser count **16**, going **279.4 mm**, width **1,016 mm**, and tread thickness **38 mm**. Clearly state the modeling convention for tread board depth and nosing.
3. Solve and poll the job to completion. Verify no errors, **16 equal rises of 180 mm**, **15 intermediate treads**, a nominal going run of **15 x 279.4 = 4,191 mm**, and a 1,016 mm nominal stair width. Verify correct lower and upper datum relationships and document any deliberate top/bottom simplifications.
4. Inspect the ordered side profile and output three valid stringer solids and separately inspectable tread/riser outputs. Check closedness, geometry validity, branch counts, aggregate bounds, and units; do not assert load capacity from geometry alone.
5. Change floor height to **3,040 mm** while preserving 16 risers. Solve a new revision: verify each rise is **190 mm**, intermediate tread count and going run remain unchanged, stringers and riser boards update, and the top floor datum is 3,040 mm.
6. Change riser count to **17** at 3,040 mm. Verify rise is approximately **178.823529 mm**, there are **16 intermediate treads**, and the going run is **4,470.4 mm** when going is unchanged. All affected geometry and summaries must update; no stale fixed objects may masquerade as the new result.
7. Confirm that an intentionally invalid input produces a clear diagnostic, no misleading successful geometry, and no partial graph mutation. Verify data-tree fixtures, stale result/revision handling, and repeat request idempotency.
8. Capture a readable Grasshopper canvas and Rhino viewport linked to the same solved revision. Save the editable definition; reopen it and repeat at least one variation. Optionally bake a chosen output and verify reported object IDs and counts.
9. Export or return structured measurement/check data to a downstream drawing/report workflow, clearly identifying unmodeled handrails, guards, landings, headroom, connections, and structural engineering. The drawing/report must show the inputs and revision used and must not claim building-code approval.

## 9. Suggested deliverables

1. A list of newly supported native component IDs, input/output contracts, and supported data types by Rhino/Grasshopper version.
2. An MCP help topic and sample graph recipe for the parametric stair, including units, coordinate conventions, data trees, output verification, and failure cases.
3. Automated unit/integration tests for graph patches, values and list semantics, geometry validity, parameter variation, solve state, revision changes, and save/reopen; plus a documented live Rhino smoke test.
4. A dedicated canvas capture tool and its platform behavior, or a documented alternate mechanism that produces an actual Grasshopper canvas image through MCP.
5. Changelog and migration notes, including unsupported configurations and explicit non-goals.

## References

- [RhinoCommon `PolylineCurve` constructor](https://developer.rhino3d.com/api/rhinocommon/rhino.geometry.polylinecurve/polylinecurve)
- [McNeel: RhinoCommon geometry in Grasshopper C#](https://developer.rhino3d.com/guides/grasshopper/csharp-essentials/3-rhinocommon-geometry/)
- [Grasshopper `GH_Document.AddObject`](https://developer.rhino3d.com/api/grasshopper/html/M_Grasshopper_Kernel_GH_Document_AddObject.htm)
- [Massachusetts 780 CMR residential stair amendments](https://www.law.cornell.edu/regulations/massachusetts/780-CMR-CHAPTER-51-ch-3-R301-301-1-311.7)
- [Massachusetts 780 CMR tenth-edition table of contents](https://www.mass.gov/regulations/780-CMR-tenth-edition-table-of-contents)

