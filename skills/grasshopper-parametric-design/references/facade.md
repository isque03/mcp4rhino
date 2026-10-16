# Parametric facade

Intent: upright panels across overall width 20, height 6, count 5, fill ratio 0.8, depth 0.4 (lengths in model units). Spacing = width/count; panel width = spacing*fill. A rectangle in the XZ plane produces each upright profile; a series and X vectors place panels; a Y vector extrudes their depth.

Resolve and describe all recipe component types before applying its batched operations. The slot/domain/multiply components encode relationships rather than storing independently editable derived sizes. Inspect panel output `move` out:0 and solid/surface output `extrude` out:0.

Expected: five panels, each width 3.2 and height 6, origins X=0,4,8,12,16, depth 0.4. Aggregate extents X=0..19.2, Y=0..0.4, Z=0..6. Rectangle/extrusion output alone is not proof of a closed solid; do not claim a volume without checking closure.

Variation: width 25 yields spacing 5, panel width 4, origins X=0,5,10,15,20, final X=24. Heights/count/depth stay fixed. Verify both bounds and viewport.

Failure exercise: set the slot divisor's count input to zero in a test copy. Inspect division/component diagnostics, then restore the count connection and a count >=1. Do not bake while required outputs have errors.

For an explicit snapshot, create or choose a Rhino layer, then gh_bake with current session/revision/result, the placed extrude node's out:0, a new request_id and that layer. Keep the returned object IDs. A subsequent design edit changes preview, not the old snapshot.

The recipe is returned by gh_help and lives at `../assets/facade.json`. Run `python3 scripts/grasshopper-smoke.py --example facade`; add --bake only when you want snapshot objects created.
