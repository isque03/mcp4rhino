# Ordered profiles and capped extrusions

This generic recipe supplies two separate ordered point branches to native Polyline, extrudes each closed curve, and caps the results. It exercises reusable point-list, branch-preservation, curve, solid, and inspection capabilities.

Resolve the recipe's symbolic types using `gh_search_components`, then describe their real ports. In the port contract declared by the recipe, Polyline inputs are Vertices and Closed; the smoke runner checks that contract before applying it. IDs are installation-discovered, never copied from another machine.

The Point parameter stores `{0}` and `{3;0}` branches, each containing four distinct rectangle corners in order. Closure is supplied separately through a Boolean parameter; the starting point is not repeated. The closed profile feeds Extrude, followed by Cap Holes. A depth slider drives the extrusion vector.

Inspect both outlines for closedness, planarity, and self-intersection, then inspect two valid solid outputs with separate branch paths. Change depth from 120 to 200 and verify the Z extent changes. Coordinates and depth are document model units. Geometry is graph-produced and remains editable; baking is optional and additive.

For irregular outlines, use native Weave for patterned interleaving, Merge for concatenation, Insert Items for insertion, Shift List for index shifts, and Entwine for explicit branch construction. Discover each component's input access/defaults and inspect the resulting paths. Avoid flattening multiple outlines into one curve accidentally. A self-intersecting curve can be geometrically valid while unsuitable as a face; read both self-intersection and downstream component diagnostics.
