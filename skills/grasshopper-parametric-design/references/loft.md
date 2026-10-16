# Branch-preserving loft study

Intent: loft two separate families of three circles, not one surface through six unrelated circles. Tree {0} has planes centered at X=0, Z=0,2,4, with radii 1,1.2,0.8. Tree {1} is centered at X=10 with radii 2,2.2,1.8. Consistent XY planes keep circle direction coherent.

Resolve the Circle and Loft types in the recipe and inspect ports. Apply both trees to Circle, connect its curve output to Loft, and solve. Inspect the Circle output tree before inspecting Loft.

Expected: two circle branches with three profiles each and two loft results. Profiles are ordered by Z. The lofts are open surfaces unless separately capped; do not interpret an open loft as a solid volume.

Variation: raise the last profile plane in each branch from Z=4 to Z=6. Expect both loft heights to increase while family separation and output count remain two.

Failure exercise: set_mapping on Loft's curve input to flatten. The tree now describes one list of six profiles and may produce one connecting/invalid surface. Inspect it, restore mapping:none, then solve and verify two families. Grafting every circle would instead give single-profile branches and cannot repair a loft requiring multiple profiles per family.

The recipe is returned by gh_help and lives at `../assets/loft.json`. Run `python3 scripts/grasshopper-smoke.py --example loft` for the live check.
