# Grasshopper and parametric design

**Stable in 0.7.3 on `main`.** Build and unit-test results do not by themselves prove every native Grasshopper edge case; report platform-specific issues with `get_support_info`.

MCP4Rhino exposes native, editable Grasshopper 1 definitions in Rhino 8 on Windows and macOS. The runtime initializes lazily when discovery or document tools are used. `gh_help` works without loading Grasshopper, and ordinary Rhino tools do not depend on it.

Ask an assistant: “Create an editable Grasshopper facade study. Make width, height, count and depth adjustable. Show the preview before baking.”

## Learn the workflow

Agents should start with `gh_help` (`topic: "index"`). Topics include `quickstart`, `design`, `data-trees`, `workflow`, `debugging`, `examples/array`, `examples/facade`, and `examples/loft`. Examples return both explanatory Markdown and machine-readable recipes with symbolic component types. Resolve the actual component GUIDs through discovery before applying recipes.

The same content is a portable [agent skill](../skills/grasshopper-parametric-design/SKILL.md). The Yak package includes the skill directory and releases include `MCP4Rhino-<version>-skills.zip`.

- **Codex:** extract `grasshopper-parametric-design` into your configured skills directory (normally `~/.codex/skills`), then refresh/reopen skill discovery as appropriate for your client.
- **Cursor:** copy that directory into the project's `.cursor/skills`. Repository developers can generate this copy using `python3 scripts/package-agent-skills.py --cursor`.
- **Other MCP clients:** use `gh_help`; no filesystem or skill installation is required.

MCP does not install local skills. Installing one is a separate client action. Edit only `skills/grasshopper-parametric-design`; embedded help, packages, and generated Cursor copies derive from it.

## Tools

For read-only context and reusable workflows, see [resources and prompts](MCP_RESOURCES_PROMPTS.md) or `gh_help` topic `resources`. Those interfaces complement tools and never initialize Grasshopper or execute graph changes during retrieval.

| Tools | Purpose |
|---|---|
| `gh_help`, `gh_status` | Help and runtime availability without opening the editor |
| `gh_search_components`, `gh_describe_components` | Installed catalog, native support flags, port types/access/default samples |
| `gh_list_documents`, `gh_open_document` | List documents, create/open a separate visible definition, explicitly attach an existing document |
| `gh_inspect_document`, `gh_apply_patch` | Inspect and batch-edit a revisioned graph |
| `gh_solve`, `gh_get_job`, `gh_cancel_job` | Queue incremental solves and poll/cancel jobs |
| `gh_read_output`, `gh_bake` | Page current outputs and explicitly bake additive Rhino snapshots |
| `gh_get_solution_summary` | Revision/result-bound graph controls, named output pages, units, and diagnostics |
| `gh_capture_canvas`, `gh_show_document` | Render a native canvas PNG or explicitly activate/frame a definition |
| `gh_save_document`, `gh_close_document` | Save .gh/.ghx, close owned definitions, detach attached definitions |

Version 0.7.3 adds [component categories, local authorization and existing-definition compatibility](GRASSHOPPER_EXTENSIBILITY.md). Install the matching 0.7.3 package and restart Rhino; an older running server does not exercise these changes.

All mutations require a unique request ID; exact retries return the original receipt. Session mutations require an expected revision. Inspect again after user edits or a conflict. Patch aliases identify nodes only inside that patch; retain the returned node GUIDs for later calls.

`gh_show_document` changes editor state only, requires a request ID and expected revision, and leaves graph revision unchanged. Capture is read-only with respect to graph/editor state and writes a PNG destination. It does not require a mutation request ID.

## General graph capabilities in 0.7.0

The native allowlist now includes Polyline, Join Curves, Weave, Merge, Shift List, Insert Items, Reverse List, List Length, Entwine, Boundary Surfaces, and Cap Holes. Discover their installed IDs and ports before use; names are not execution authority. Primitive constants on Generic Data inputs use connected native Number, Integer, Boolean or Text parameters.

`gh_help` topics `inspection` and `capture` describe geometry measurements, selected named output summaries, independent pagination, canvas framing, explicit editor activation, and guarded `capture_viewport` context. Numerical outputs keep the meaning defined by the graph; no physical unit is inferred from a number alone. Unsupported geometry measurements are explicit nulls.

The `profile` example exercises two separate closed outlines and capped extrusions. The `stair` example composes the same generic tools into a parameterized study and is only a regression fixture. All stair formulas live in native graph components; no staircase-specific tool, validation service, or compliance rule was added. The fixture intentionally omits riser boards and engineered bearing cuts.

Patches contain 1–100 operations and create one Grasshopper undo record. Set `solve:true` to queue one solve after commit; the default is false. A solve failure leaves the graph available for repair. In 0.7.3, slider-only patches with `solve:false` edit existing controls in place even when third-party or restricted objects are present, preserving those objects and their wires. Other mutations and solves require every graph object to be authorized. New/opened documents remain disabled until explicitly solved.

The verified manifest is [NativeComponents.json](../src/MCP4Rhino.Logic/Grasshopper/NativeComponents.json). Discovery adds `support_level`: **verified**, **available**, or **restricted**. Legacy `supported:true` still means manifest verified. Built-in modeling components work automatically, even when marked available. For third-party plugins, open **Tools → MCP4Rhino → Grasshopper plugins…** (or run `MCP4RhinoGrasshopper`), select trusted plugins and click Apply. Permissions are saved and apply immediately; no environment variables, file edits or additional restart are needed. Use `authorized:true` as execution permission; `supported` is only the verification badge. Known script, cluster, timer, external-I/O and other risky types remain restricted. Unknown plugin side effects cannot be detected reliably; discovery is not execution permission. See the [0.7.3 architecture review, configuration and acceptance matrix](GRASSHOPPER_EXTENSIBILITY.md).

## Execution and output lifetime

Solves run serially on Rhino's UI thread. A job ID keeps the HTTP request short; heavy components may still block Rhino's UI. Polling job status does not wait for that thread. Queued cancellation is immediate; running cancellation/timeouts are cooperative and cannot kill an unresponsive component. No second MCP solve starts until the first exits. Canvas operations return `busy` while an MCP solve is pending.

Outputs are references to the current solution, not archived geometry. Graph changes, manual solves and referenced Rhino object changes invalidate them. Read or bake with the current result ID. Tree rows preserve explicit paths and item indexes; empty branches have an explicit empty marker. Pages default to 50 rows and have a maximum of 200. Complex geometry is summarized with validity and bounds rather than sent as large JSON blobs.

Sessions bind to the Rhino document active when created/attached. Activate that document for geometry operations. Saving a GH definition does not save its referenced Rhino model. Close an owned modified definition only after saving or with explicit `discard:true`. Closing an attached session leaves the user's definition open.

Baking creates new Rhino objects, tags them with session/result/source provenance, and returns their IDs. Existing snapshots remain unchanged. An optional target layer must already exist; create it with the existing Rhino `create_layer` tool. Exact bake retries return the same IDs.

## Architecture and development

The host owns one lazy `IGrasshopperRuntime`. Its adapter assembly stays outside the collectible Tools context. Contracts cross that boundary without SDK types. Pure schema/tree/graph/receipt/job logic lives in Logic and is covered by the existing 80% gate. The adapter owns SDK document access, subscriptions, serialization and UI execution. Updating the host, contracts or persistent runtime requires a full install and Rhino restart; tool-facade hot reload preserves sessions.

The SDK download is pinned to Grasshopper 8.21.25188.17001. Explicit compile references avoid the SDK NuGet package's WindowsDesktop framework dependency on macOS. Rhino supplies runtime assemblies; packaging rejects bundled RhinoCommon, Grasshopper or GH_IO DLLs.

## Verification

Run `dotnet build MCP4Rhino.sln -c Release`, `bash scripts/test-coverage.sh`, and `bash scripts/pack-yak.sh` (or the PowerShell equivalents). Skill recipe tests check public schemas, supported types, tree shapes, and packaged help without loading Grasshopper. They do not establish SDK behavior.

With the updated plugin installed and Rhino running in a test document:

```bash
python3 scripts/grasshopper-smoke.py --example array
python3 scripts/grasshopper-smoke.py --example facade --bake
python3 scripts/grasshopper-smoke.py --example loft
python3 scripts/grasshopper-smoke.py --example profile
python3 scripts/grasshopper-smoke.py --example stair --capture
```

The smoke runner discovers real component IDs, builds a definition, checks solve diagnostics/counts, retries patches, rejects invalid edits, invalidates old outputs, varies a parameter (or repairs a tree mapping), and round-trips .gh/.ghx. `--bake` creates two additive snapshots and checks exact retry deduplication. It leaves its final definition open and prints temporary saved paths for inspection.

During implementation on October 16, 2026, the macOS solution build, unit coverage gate, skill validation, packaging and live `gh_help` passed. The local Rhino 8.35 installation reported an expired evaluation (`can_save:false`) and view-only mode; Grasshopper could not load. Native graph/solve/bake/round-trip checks and fresh-agent modeling trials remain unverified, as do Windows runtime checks. The alpha makes these checks available to licensed testers; run them before stable promotion.

On October 27, the local 0.6.2-alpha server again reported saving unavailable and returned `grasshopper_unavailable` during discovery. The new adapter could be compiled but could not be exercised in that process. Native geometry, detached-canvas rendering, hidden-editor behavior, view preservation, manual edits, undo/redo, baking, and save/reopen remain unverified for 0.6.5-alpha on both platforms. Use `--skip-save` only to run other checks while explicitly reporting persistence unverified. `--capture` captures the canvas before explicitly showing the definition for Rhino preview capture.

Also verify manually on both platforms: one native GH undo/redo step per patch, no side effects on other open definitions, slider edits causing revision conflicts, reference deletion/replacement, switching Rhino documents, queued/running cancellation, and facade dimensions/viewport. For skill trials, give a fresh agent only the installed skill—or only MCP help—and ask it to build an array, repair a loft tree mismatch and change a facade parameter while preserving relationships. Judge geometry, tree structure and tool behavior, not wording.

<a id="alpha-tester-checklist"></a>

## Tester checklist

Use a licensed Rhino 8 installation; the initial native target is Rhino 8.35 with bundled Grasshopper 1. Grasshopper 2 is outside this release. Use disposable Rhino documents and save any existing work first.

1. Open `PackageManager`, search **MCP4Rhino**, select stable `0.7.3` (prereleases can remain disabled), and install/update. Apply and restart Rhino completely.
2. Call `get_support_info` and confirm `server_version` is `0.7.3`. Call `gh_help` with `topic: "index"` and `gh_status`. Confirm an ordinary tool such as `get_document_info` still works. The endpoint remains `http://127.0.0.1:4010/mcp`.
3. Use an agent with only MCP help or the downloaded portable skill to create the array, facade, and loft examples. Inspect editable native components, preview, dimensions, tree structure and diagnostics. Change a parameter while preserving derived relationships; repair the loft's deliberate tree mismatch.
4. Optionally download `grasshopper-smoke.py` from the same GitHub release. For mixed-definition checks, also download `grasshopper-compatibility-smoke.py` into the same folder and follow the [acceptance matrix](GRASSHOPPER_EXTENSIBILITY.md). With Python 3, run `python grasshopper-smoke.py --example array`, `python grasshopper-smoke.py --example facade --bake`, and `python grasshopper-smoke.py --example loft`. The runner needs no checkout or Python packages. It creates definitions, temporary `.gh`/`.ghx` files, and, with `--bake`, two geometry snapshots. It leaves the final definition open for inspection.
5. Test manual edit conflicts, one undo/redo step per patch, deleted/replaced Rhino references, switching documents, queued/running cancellation, no overlapping solves, and ordinary tools when Grasshopper cannot load. Record heavy-solve UI blocking rather than treating a job ID as evidence that Rhino stays responsive.
6. Record **Windows and macOS separately**: OS/version, Rhino build, MCP4Rhino version, install/restart outcome, scenario results, expected versus actual geometry, and any errors. Attach relevant `get_support_info` JSON, job/component diagnostics, log excerpts and a minimal definition to a [GitHub issue](https://github.com/isque03/mcp4rhino/issues). Remove secrets and license keys. An untested platform remains unverified.
7. Verify Package Manager offers stable `0.7.3` with prereleases disabled, including when upgrading from `0.7.2-alpha.1`. If automatic package updates are enabled, record their actual behavior.

### Roll back to stable

To roll back, uninstall MCP4Rhino through Package Manager, explicitly reinstall **0.7.1**, disable prerelease selection, and restart Rhino completely. If a restart is requested between uninstall and reinstall, complete it. Confirm `get_support_info` reports `0.7.1`. **Clearing Include pre-releases alone does not downgrade the installed package.**
