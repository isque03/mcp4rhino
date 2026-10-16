# Adjustable array

Intent: five circles of radius 1, spaced 3 model units along X. Count is an independent integer slider. Series derives positions; vector construction expresses translation; Move retains editable source geometry.

Discover and describe the types in the accompanying recipe. Substitute each `$name` component token with its returned component_id. Verify input/output order against the descriptions, then submit the recipe's operations in one `gh_apply_patch` with your session, revision and request_id. Solve once and inspect `move` out:0 using the returned alias map.

Expected: five circles in a single branch, centers X = 0,3,6,9,12; aggregate bounds X = -1..13, Y = -1..1, Z = 0. The native solver may add path prefixes; preserve and inspect the returned paths.

Variation: change count to 8 with set_slider. Expect eight circles, last center X=21, bounds X=-1..22. No rewiring or independent placement edits should be necessary.

Failure exercise: disconnect Series from the vector X input and solve. The default vector can repeat circles at one location. Repair that connection, solve again and verify distinct centers/counts. A valid curve count alone does not prove correct placement.

The recipe is returned by gh_help and lives at `../assets/array.json`. Live executable check: `python3 scripts/grasshopper-smoke.py --example array` from the repository. It creates its own definition and does not bake by default.
