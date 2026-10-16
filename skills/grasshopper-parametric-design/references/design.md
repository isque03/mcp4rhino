# From a brief to a parametric model

Use ordinary Rhino tools for a one-off edit with no useful dependency relationship. Use Grasshopper when the user needs a family of designs, reusable rules, or repeated adjustment.

Separate four concepts:

- Independent parameters are choices: count, overall width, height, and a panel fill ratio. Give each a unit, name, bounds and valid integer/continuous behavior.
- Derived values encode intent: spacing = width / count; panel width = spacing * fill ratio. Derive these in the graph instead of storing several numbers that can drift apart.
- Constraints define valid designs: count >= 1, 0 < fill ratio <= 1, positive depth. Reject invalid regions before expensive geometry.
- Objectives measure alternatives: area, material volume, clearance or deviation. Measurement alone is not optimization; v1 has no optimizer or bulk sweep.

Decide how data represents the design. One branch per facade row or loft family preserves a relationship that a flat list loses. Sketch dependencies and expected counts before choosing components.

Check Rhino's document units and tolerance. Lengths use document units; ratios and normalized fractions are dimensionless. Angles depend on the component's documented inputs, often radians. A normalized curve parameter is not an arc length. Confirm domains rather than assuming 0..1.

Construct geometry in a deliberate plane, then transform it. Check curve direction, ordering, seams, and surface normals before lofts or extrusions. Near-zero distances relative to document tolerance can produce degenerate geometry. A closed curve, open surface and closed solid have different area/volume meanings.

Validate the smallest useful model. Measure baseline outputs and vary one input, testing a boundary as well as a typical value. Check that counts, dimensions and associations change as intended. Preserve readable names and layout so the user can continue editing.

Example facade: width 20, count 5 and fill 0.8 imply spacing 4 and panel width 3.2. Changing width to 25 should produce spacing 5 and panel width 4 without independently editing either derived value.

Further reading: [McNeel algorithms and data structures](https://developer.rhino3d.com/guides/grasshopper/gh-algorithms-and-data-structures/).
