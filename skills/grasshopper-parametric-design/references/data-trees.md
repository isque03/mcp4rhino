# Data trees and matching

A tree maps explicit integer paths to ordered items. It is not safely represented by an arbitrary nested list: paths can be sparse or have different depths. Null items and empty branches have distinct meanings.

Persistent input format:

```json
[{"path":[0],"items":[1,2]},{"path":[3,0],"items":[]},{"path":[7],"items":[null,4]}]
```

Item access processes individual values, list access processes branch lists, and tree access receives the whole tree. Grasshopper performs matching and conversions according to the component and its parameter settings; mismatched input lengths need inspection, not an assumed zip. Check the actual output paths/counts after connecting lists.

For two loft families, organize input curves as `{0}: [A0,A1,A2]` and `{1}: [B0,B1,B2]`. A list-access loft receives two families. Flattening changes this to `{0}: [A0,A1,A2,B0,B1,B2]`, which can join unrelated families.

- Flatten removes branch separation: use it when all items intentionally form one collection.
- Graft makes each item its own branch: `{0}:[A,B]` becomes branches such as `{0;0}:[A]` and `{0;1}:[B]`. This can create many matched combinations downstream.
- Simplify removes redundant shared path prefixes. It does not mean sort, flatten, or merge arbitrary branches.
- Reverse changes item ordering, which matters for profiles and transformations.

Use `set_mapping` on an input port for none/flatten/graft, simplify and reverse. Inspect both source trees first. Connected inputs ignore local persistent values, so `set_value` rejects them until disconnected.

`gh_read_output` pages rows. Each ordinary row has `path`, `item_index` and `value`; an empty-branch row has `path`, `item_index:null`, `empty:true`. A null item instead has an integer index and `value:null`. Pagination may split a branch; reassemble by path and index. Complex geometry is summarized, not serialized into chat.

[McNeel data tree guide](https://developer.rhino3d.com/guides/rhinopython/grasshopper-datatrees-and-python/).
