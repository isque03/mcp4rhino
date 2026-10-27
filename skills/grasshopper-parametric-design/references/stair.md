# Stair study: a regression example for generic tools

This example is a native graph recipe, not a staircase tool. All calculations, list operations, geometry, and relationships live in visible native Grasshopper components. The server contains no staircase entities, sizing service, or regulatory rules. Use the same tools for other ordered profiles and repeated assemblies.

The recipe intentionally exceeds one patch: apply batches of at most 100 operations, retain each returned alias-to-instance-GUID map, resolve references to earlier batches, and solve after the final batch. Component GUIDs must be discovered locally; describe ports before use. The smoke runner performs batching, alias resolution, schema checks, and exact-request retries.

Use a disposable millimeter Rhino document. Sliders control floor height, riser count, going, width, tread thickness, nosing, stringer depth/thickness/count. +X is run, +Y is width, +Z is rise. The upper floor is the final walking surface; N risers produce N−1 intermediate treads. Rise is height/N and nominal run is (N−1)×going. Board depth is going+nosing; boards project toward −X by the nosing amount.

Weave alternates two equal-length point streams to create the sawtooth top edge. Merge appends two graph-derived lower points, Polyline closes the outline, Extrude and Cap Holes form one stringer, and Move distributes copies across the width. Treads are independent native rectangular extrusions positioned by the same rise/going relationships. No Rhino snapshots or scripts supply the geometry.

This deliberately simplified study has a sloping lower stringer edge defined by a vertical depth below the first/last notches. It has no engineered bottom/top bearing cuts, riser boards, landings, rails, guards, headroom context, or connections. It does not establish buildability or compliance. Parameter combinations may produce intersecting outlines or overlapping solids; inspect generic geometry measurements and native diagnostics rather than treating slider limits as domain validation.

The initial inputs are height 2880, risers 16, going 279.4, width 1016, tread thickness 38, and three stringers. Verify rise 180, 15 treads, nominal run 4191, and three valid stringer solids. Change height to 3040: rise becomes 190 and geometry updates. Then change risers to 17: rise becomes approximately 178.823529, tread count becomes 16, and run becomes 4470.4. Inspect named graph outputs `rise`, `tread_count`, `run`, `outline`, `treads`, and `stringers` using `gh_get_solution_summary` and paginated output reads.

Capture canvas and viewport with the same result identity; save/reopen the editable graph and repeat a variation. `python3 grasshopper-smoke.py --example stair --capture` runs the native checks. This recipe requires licensed platform validation before its geometry can be described as runtime-verified.
