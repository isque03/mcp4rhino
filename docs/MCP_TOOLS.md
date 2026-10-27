# MCP tools reference

Complete inventory for agents and developers. End-user overview: [README.md](../README.md). Architecture roadmap: [ARCHITECTURE_AGENT.md](ARCHITECTURE_AGENT.md).

Default endpoint: `http://127.0.0.1:4010/mcp`.

MCP resources and prompts complement tools in 0.6.5-alpha. See [resources, prompts and protocol compatibility](MCP_RESOURCES_PROMPTS.md) for the catalog, URI templates, active-document boundaries, privacy and three supported protocol profiles. Reading resources or retrieving prompts never invokes a modeling tool.

**Document units:** length coordinates on create/transform tools are in the **current document unit system**. If the user says “32 inches” and the file is in Feet, call `convert_length` first, then create, then verify with `measure_size` or `measure_distance`.

## Core document and view

| Tool | Description |
|------|-------------|
| `get_document_info` | Units, layers, counts, selection |
| `get_document_units` | Unit system, tolerances, conversion factors |
| `convert_length` | Convert a named unit into document model units |
| `measure_size` | Bounding-box size plus mm/in/ft/m |
| `get_objects` | List/filter by tag, group, layer, or name |
| `create_box` / `create_sphere` / `create_cylinder` | Solids (document model units) |
| `delete_objects` | Delete by GUID list |
| `set_user_text` / `get_user_text` | Object key/value tags |
| `set_object_name` / `set_object_layer` | Rename or change layer |
| `list_groups` / `create_group` / `add_to_group` | Group membership |
| `list_views` / `get_view` / `set_active_view` | Viewports |
| `set_illustration_style` | Pale architectural display preset |
| `set_view` / `orbit_view` / `pan_view` / `zoom_view` / `zoom_extents` | Camera |
| `capture_viewport` | PNG screenshot (+ MCP image when ≤ 1 MiB) |
| `run_rhino_command` | Scripted Rhino command escape hatch |
| `get_support_info` | Version, endpoint, log path, GitHub Issues URLs, filing tips |
| `mcp4rhino_reload` | Hot-reload Tools and Logic |

`initialize` also returns MCP `instructions` with a capability-lane map (getting started, document, geometry, architecture, drawings, analysis, code checks, Grasshopper), clear-English writing guidance, plus [GitHub Issues](https://github.com/isque03/mcp4rhino/issues) and `get_support_info` before filing bugs or enhancements. New users can follow the `getting_started` prompt or `mcp4rhino://server/getting-started`.

## Curves and solids

- **Create:** `create_point`, `create_line`, `create_polyline`, `create_circle`, `create_arc`, `create_ellipse`, `create_polygon`, `create_rectangle`, `create_curve`
- **Edit:** `join_curves`, `trim_curve`, `split_curve`, `explode_objects`, `offset_curve`, `fillet_curve`, `extrude_curve`
- **Boolean:** `boolean_difference`, `boolean_union`, `boolean_intersection`
- **Layers:** `list_layers`, `create_layer`, `set_layer_props`
- **Blocks:** `create_block_definition`, `insert_block`, `list_blocks`
- **Other:** `transform_objects`, `set_document_units`, `get_geometry`, `select_objects`, `clear_selection`

## Surfaces

- **Create:** `create_plane_surface`, `create_srf_pt`, `create_planar_surface`, `create_edge_surface`, `loft_surface`, `sweep1_surface`, `sweep2_surface`, `revolve_surface`, `rail_revolve_surface`, `network_surface`, `patch_surface`, `pipe_surface`, `extrude_curve_along_curve`
- **Edit:** `fillet_surfaces`, `blend_surfaces`, `chamfer_surfaces`, `offset_surface`, `trim_surface`, `split_surface`, `join_surfaces`, `cap_planar_holes`
- **Edges:** `list_surface_edges`, `dup_border`, `dup_edge`, `extract_isocurve`

Surface face pick for fillet/chamfer: `face_index_*` or `pick_point_*`. Edge pick for blend/dup_edge: `edge_index` or `pick_point` from `list_surface_edges`.

## Architecture

`set_project_info`, `get_project_info`, `create_level`, `list_levels`, `set_active_level`, `create_grid`, `create_site`, `create_building`, `create_wall`, `create_slab`, `create_roof`, `create_column`, `create_beam`, `create_door`, `create_window`, `create_stair`, `create_ramp`, `create_railing`, `create_curtain_wall`, `create_space`, `update_space`, `list_spaces`, `apply_opening`, `list_element_types`, `upsert_element_type`, `get_building_model`, `query_elements`, `measure_distance`, `measure_area`, `measure_clear_width`.

Semantic tags use `mcp4:` user-text keys on model objects (see project skills under [`.cursor/skills/`](../.cursor/skills/)).

## Drawings and documentation

`create_section` (cutting plane + named view looking at the cut), `create_elevation` (baseline + view direction), `create_detail_view` (named camera/target), `create_dimension`, `create_text`, `create_leader`, `create_hatch`, `create_room_tag`, `create_door_tag`, `create_window_tag`, `create_schedule`, `create_sheet` / `list_sheets` / `place_view_on_sheet` (document sheet registry — not layout geometry), `export_pdf` (best-effort print/script), `export_dwg`, `export_images`, `apply_layer_standard`.

## Interop and analysis

`check_rhino_license`, `export_3dm` (requires valid license / active eval — call `check_rhino_license` first), `export_ifc` (minimal IFC2X3: tagged `mcp4` elements as bounding boxes only), `import_ifc` (metadata-only — storey GUIDs / path note; no geometry import), `export_obj`, `link_external_model`, `list_links`, `clash_detect`, `quantity_takeoff`.

## Code checks

`set_code_context`, `run_code_checks`, `get_code_report` — **findings only**. Never claim legal “code compliant” status or an RA stamp.
# Grasshopper tools

Native parametric design tools and help are documented in [GRASSHOPPER.md](GRASSHOPPER.md).
Start with `gh_help` or `gh_status`. Component discovery and definition operations initialize Grasshopper lazily.
The fixed catalog includes discovery, document lifecycle, batched graph editing, queued solves, paginated output and explicit additive baking.

`gh_get_solution_summary` returns guarded graph controls, named output pages, units, and diagnostics. `gh_capture_canvas` renders the actual native graph with explicit framing; `gh_show_document` deliberately activates its editor view. `capture_viewport` accepts optional `grasshopper:{session_id,expected_revision,result_id}` context to associate a Rhino preview image with one current solution. See `gh_help` topics `inspection` and `capture` for contracts and limitations.
