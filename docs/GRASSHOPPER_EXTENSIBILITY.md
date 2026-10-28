# Grasshopper extensibility in 0.7.2-alpha.1

## Architecture review

The old manifest served two roles: a tested native compatibility list and the execution allowlist. `ComponentCatalog.Create` rejected types outside it, `DescribeType` omitted their ports, and both `GraphEditor.Patch` and `GrasshopperRuntime.Solve` rejected an entire definition containing any such object. Inspection already traversed existing objects and ports without the whole-document check. These restrictions simplified the first implementation and bounded execution, but a third-party object anywhere in a definition also prevented an unrelated slider edit.

Patches formerly serialized and cloned the whole definition before validating operations. Removing only the allowlist check would invoke arbitrary plugin constructors and deserializers during staging, and whole-document undo would replace unrelated object instances. A slider edit therefore needs a separate transaction path before loosening creation or execution.

Grasshopper's installed catalog already supplies GUIDs, library IDs, assembly/type identities and proxy metadata. `GH_ComponentServer.ObjectProxies` and `EmitObjectProxy` support discovery; `EmitObject` creates native objects. Creation runs plugin constructors, even when used only to discover ports. Actual instances expose input/output parameters, parameter and goo types, access modes, defaults and source connections. Type names alone cannot prove wire compatibility: Grasshopper's runtime casts, dynamic parameters and plugin implementations determine the result. Inspect diagnostics and outputs after solving. Persistent-value assignment still uses the existing codecs; arbitrary custom goo assignment is outside this change.

For comparison, [rhinomcp's catalog](https://github.com/jingcheng-chen/rhinomcp/blob/main/plugin/Functions/GrasshopperCatalog.cs) searches installed proxies and calls `CreateInstance` for port introspection. Its [component implementation](https://github.com/jingcheng-chen/rhinomcp/blob/main/plugin/Functions/GrasshopperComponents.cs) creates installed objects from proxies, and its [solution implementation](https://github.com/jingcheng-chen/rhinomcp/blob/main/plugin/Functions/GrasshopperSolution.cs) invokes the native solver. These inspected paths demonstrate dynamic SDK use; they do not establish that arbitrary components execute safely. MCP4Rhino retains its own revision, receipt and job contracts.

The [online SDK reference](https://developer.rhino3d.com/api/grasshopper/html/Methods_T_Grasshopper_Kernel_GH_ComponentServer.htm) currently describes Rhino 9. Implementation is compiled against **8.21.25188.17001**, using that package's API/XML documentation; compilation and metadata tests are separate from runtime acceptance.

## Reviewed incremental plan

This prerelease is for separate-machine testing; stable 0.7.2 publication waits for explicit user confirmation.

An independent read-only subagent reviewed this plan before implementation for SDK compatibility, authorization, compatibility and complexity. Its corrections are reflected here: use raw slider values, preserve the full accuracy enum, disable automatic solving after undo, retain preview behavior, check archive warnings and verify cloned identities.

1. Keep the manifest and legacy `supported` field as the verified compatibility baseline. Add a small pure `ComponentPolicy`, consumed by the existing SDK catalog gateway. No new execution engine or public tool is introduced.
2. Add `SliderState` as the raw control value object and `SliderPatch` as a focused transaction script. Prevalidate every slider operation before mutation, including repeated targets. Commit in place under solution suppression, with one targeted undo record and slider-only rollback. Leave other object instances, parameters, wires and positions untouched. Existing cycles do not block this control-only edit.
3. Classify installed types, automatically authorize built-in modeling libraries, and use Rhino plugin preferences for third-party library permissions. Discovery never calls constructors. Type-level port introspection and graph creation require authorization. Existing instance inspection continues to expose actual ports.
4. Keep staged archive transactions for structural/non-slider patches, requiring authorization for all existing objects before serialization. Check node/type, parameter/type/access and ordered source identities after cloning. Fail on archive warnings/errors rather than patching an incomplete graph. Trusted plugin state serialization still needs native round-trip validation.
5. Check all graph objects at queued solve execution, including locked nodes. For `solve:true` patches, also check before commit and on the staged graph. A denied authorization cannot first commit a slider change. Solve failures after a successful commit retain the existing repair workflow.
6. Load `.gh`/`.ghx` with the existing GH_IO archive SDK under solution suppression, dispose failed loads and reject archive warnings/errors. This conservatively rejects some files Grasshopper could repair interactively. Open those manually, resolve dependencies, and explicitly attach. Do not delete or replace unresolved objects to make a file pass.

The application service remains `GrasshopperRuntime`; `DefinitionSession` owns revision/result identity, `RequestLedger` owns retry receipts, `SolveQueue` owns serialized jobs, and `ComponentCatalog`/`DefinitionArchive` remain the SDK gateways. All SDK work stays local on Rhino's UI thread. There is no Repository or separate Domain Model layer to add to this small transaction-oriented adapter.

## Component categories and authorization

| Category | Discovery | Creation/type port introspection | Existing slider edits | MCP solve |
|---|---|---|---|---|
| Verified | Installed proxy metadata | Allowed | Allowed | Allowed if every graph object is authorized |
| Available | Installed compiled type outside manifest | Built-in modeling libraries allowed automatically; third-party plugins require user selection in Rhino preferences | Allowed without trusting unrelated nodes | Every object must be authorized |
| Restricted | Metadata where available | Denied | Allowed on verified sliders elsewhere in the graph | Denied while restricted objects are present |

`supported` keeps its original meaning: manifest verified. New `support_level`, `authorized`, `permission_required` and `restriction_reason` fields explain the distinction. `assembly_identity` is the exact .NET assembly full name; `library_id` and `component_id` remain distinct from placed instance GUIDs. `gh_describe_components` returns `ports_status: authorization_required` or `restricted` without instantiating denied types. Port descriptions add `data_type` and `parameter_type`; existing access/default/mapping/source fields remain.

Rhino's built-in modeling libraries work automatically. A component outside the manifest still reports `available`, but can have `authorized:true`: the manifest records tested compatibility, not the complete executable catalog. The adapter identifies built-in modeling libraries by known assembly name, matching public-key token and installation under the loaded Grasshopper runtime directory. This is provenance selection, not signature validation or a sandbox. Known-risk restrictions are checked before this automatic grant.

For third-party plugins:

1. In Rhino, open **Tools → MCP4Rhino → Grasshopper plugins…**, use the **Grasshopper plugins…** button in the Status dialog, or run `MCP4RhinoGrasshopper`.
2. Select the installed plugins you trust and click **Apply**. Restricted-only libraries cannot be enabled.
3. Continue the agent's work. Changes apply immediately and persist for future Rhino sessions; Cancel changes nothing. No environment variables or file edits are needed.

Choices are saved through Rhino's plugin settings, keyed by exact assembly identity and version. Updating a third-party plugin requires a new choice. A revoked grant blocks subsequent MCP creation, type instantiation during introspection, queued solves and baking; existing-instance inspection and cached component metadata remain readable; it does not terminate code already executing or govern manual Grasshopper solutions. The grant covers available components in that library, including constructors, serialization, callbacks, solving and custom data operations. MCP tool arguments cannot grant permission. The alpha.1 implementation no longer reads the alpha's environment-variable configuration.

Known scripting assemblies/types, Python/CSharp types, clusters, timers, external file/stream/network types, Hops/Compute/optimization types, obsolete components and proxies without compiled metadata remain restricted. Known script/cluster base types are also checked for renamed subclasses. This conservative identity-based filter is **not a complete side-effect detector**. A plugin with an innocuous name can execute arbitrary code. Assembly full names are selection keys, not signatures or a sandbox. Install and trust only reviewed dependencies.

Opening a file already deserializes its installed plugins; explicit open/attach is not a way to inspect hostile files safely. Existing-object inspection and saving may also invoke plugin code. Solution suppression prevents ordinary implicit Grasshopper solutions during loading and patches; it cannot contain malicious callbacks or independently scheduled plugin work. The policy governs MCP actions, not manual user solves or other plugins. Authorized solves retain native preview behavior and leave documents enabled as before. Cancellation is cooperative and cannot terminate arbitrary in-process code.

## Compatibility limits

- Existing public tool inputs remain unchanged. Graphs containing untrusted or restricted objects can be inspected and receive **slider-only** `set_slider` batches with `solve:false`. This path performs no whole-document archive, clone or restore. Other mutations require all graph objects to be authorized; untrusted graph reconstruction is deliberately deferred.
- Slider snapshots use `Slider.Value`, not expression-evaluated `CurrentValue`; the inspection response adds `settings.raw_value`. Expressions and decimal places are not modified. Omitted `integer` preserves Float/Integer/Even/Odd accuracy; explicit true/false selects Integer/Float. Invalid values fail before mutation.
- Slider undo/redo expires downstream results without automatically solving the definition. Explicitly solve again when authorized. Revisions and current result IDs still invalidate on edits/undo/redo. Structural undo also avoids an implicit solution.
- Existing graph-cycle restrictions remain for structural edits and MCP solves. Cycles do not prevent slider-only patches. Existing third-party variable-port definitions require native fixture testing; unsupported value codecs fail explicitly.
- Archive warning/error rejection is stricter than the old loader. The file remains untouched on failure. Missing-dependency recovery belongs in Grasshopper's UI; do not save a partial replacement through MCP.
- The manifest means verified compatibility coverage, not proof of every native behavior on every supported Rhino/OS version. No Grasshopper 2, Compute execution, remote processing or automatic plugin installation is added.

## Verification and remaining acceptance

Pure tests cover category/grant behavior, exact version identity matching, malformed configuration, known restrictions, schema rejection of authorization overrides, slider range/accuracy rules, repeated-target batch planning, invalid-batch nonmutation and request retries. Existing tests cover graph/tree validation, queue/cancellation and revision/result/receipt behavior. SDK metadata checks and the adapter build establish API compatibility only. They do not run plugin constructors, canvas operations, archive extraction, geometry, undo or the solver.

Download both smoke scripts from the prerelease into the same folder. Use `grasshopper-compatibility-smoke.py` against a **disposable already-open definition**, supplying its document and slider GUIDs, for example:

```bash
python3 grasshopper-compatibility-smoke.py --document-id <document-guid> --slider-id <slider-instance-guid> --value <new-raw-value> --expect-solve-denied
```

Use `--expect-solve-denied` for a definition containing untrusted/restricted nodes; omit it for an already-authorized definition. It attaches, tests a repeated-target slider patch and retry, checks preservation of all reported node/port/wire identities, rejects an invalid later operation, checks solve authorization when requested, then restores the original raw slider value. It never infers preservation of opaque plugin-private state from inspection alone.

Record the following matrix separately for **licensed Windows and macOS Rhino 8**, with the 0.7.2-alpha.1 package installed and Rhino fully restarted:

| Scenario | Required evidence |
|---|---|
| Discovery/instantiation | Verified, available native, installed third-party, restricted script/timer/cluster, null-type user object; search causes no construction; denied describe causes no construction; enable a plugin through the native dialog and repeat; Cancel/no change, revoke/immediate denial and persisted choices after restart |
| Create/connect | Build an authorized dynamic component graph across multiple patches; inspect real ports/access/defaults; reject incompatible or invalid edits without partial graph changes |
| Existing definition | Slider → third-party → native fixture; preserve object references, instance/port GUIDs, source ordering, custom settings and untouched positions; repeat without plugin trust |
| Slider transactions | Expression slider, Even/Odd modes, decimal places, disjoint ranges, repeated targets, invalid second control, injected write failure/rollback and exact request retry |
| Solve/output | Deny untrusted/restricted graph even with locked objects and `solve:true`; prove rejection leaves sliders unchanged; enable plugin in preferences and check native diagnostics, preview, trees and dimensions |
| Bake | Existing smoke `--bake` checks two additive geometry snapshots and receipt deduplication from current authorized outputs |
| Persistence | `.gh` and `.ghx` save/reopen, custom plugin state and ordered connections; missing dependency must fail explicitly without a partial editable document |
| Undo/redo | One native GH step per patch; slider undo/redo retains unrelated object references/settings and expressions; no automatic solve, current outputs invalidated; explicit solve gives expected result |
| Lifecycle/platforms | Manual edits/revision conflict, active Rhino document switch, hidden editor, preview, other open definitions unaffected, queued/running cancel, full restart and plugin load differences |

Run the existing array/facade/loft/profile/stair smoke recipes for native regressions. An expired evaluation or unavailable Grasshopper blocks these checks; record the actual version/license result instead of treating a successful build or mocked smoke test as acceptance. Native 0.7.2 behavior remains **unverified until these runs succeed**.

### Local verification record — 2026-10-28

The Release solution build, source-version check, 0.7.2 Yak packaging/contents validation, 371 .NET tests, 33 release/smoke orchestration tests and 19 demo tests passed. Logic/Contracts line coverage was 95.47%; the new policy and slider-state classes each had 100% line coverage. These numbers exclude native adapter execution.

Read-only calls to the running Rhino server returned `server_version: 0.7.1`, `can_save: false`, installation type Evaluation and zero evaluation days remaining. `gh_status` reported `initialized: false`. The 0.7.2 package was built locally but was not installed or used for native acceptance. No 0.7.2 Windows or macOS native acceptance result is claimed.

For alpha.1 native acceptance, repeat the reported Interpolate / Pipe Variable / Periodic input / white Custom Preview graph without configuring any environment variables. Check actual port/value support separately from permission: special controls and custom goo do not gain new codecs just because a component is authorized. Also exercise the plugin-preferences command concurrently with first MCP discovery and verify Rhino stays responsive.
