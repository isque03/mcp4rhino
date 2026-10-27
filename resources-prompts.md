# MCP4Rhino: Generic resources, resource templates, and prompts

**Audience:** MCP4Rhino maintainers and client developers  
**Scope:** Reusable MCP capabilities for Rhino and Grasshopper workflows  
**Observed baseline:** MCP4Rhino 0.6.2-alpha, Rhino 8 on macOS; server negotiated MCP protocol version 2024-11-05 during testing  
**Status:** Proposed design, not a description of capabilities currently implemented

## 1. Objective

Expose reusable, discoverable information and workflows so an MCP client can understand the active Rhino document, choose and inspect a Grasshopper definition, discover supported components, interpret results, and guide common modeling tasks.

This specification is **domain-neutral**. It must support architectural models, product geometry, fabrication studies, installations, and other Rhino and Grasshopper uses without embedding rules for a particular object or industry. Industry-specific requirements, if added separately, should be optional and versioned.

Resources and prompts complement existing tools. They do not replace the tools needed to create or modify geometry, edit a graph, solve, bake, save, or capture an image. In particular, adding resources and prompts alone will not add missing Grasshopper component support or parametric geometry operations.

## 2. Observed server behavior

In the tested session, initialization identified the server as MCP4Rhino 0.6.2-alpha and advertised `tools` only. Requests to `resources/list`, `resources/templates/list`, and `prompts/list` returned method-not-found errors. This observation is limited to that server and version; a later implementation may differ.

Existing read-oriented tools already expose useful information, including `get_document_info`, `get_objects`, `list_views`, `gh_status`, `gh_help`, `gh_list_documents`, `gh_inspect_document`, `gh_search_components`, `gh_describe_components`, `gh_get_job`, and `gh_read_output`. The goal is to make appropriate read-only information discoverable through MCP resources without breaking existing clients or assuming that every host automatically loads resources or supports every optional feature.

## 3. Design principles

1. **Generic primitives:** Describe documents, objects, components, graphs, solutions, views, and workflows; avoid object-specific APIs.
2. **Read-only resources:** Resource reads should not mutate Rhino or Grasshopper state, create documents, initiate a solve, bake geometry, modify the current view, or trigger installation. If component discovery must initialize Grasshopper, document this explicitly and do not hide it in a nominally read-only resource.
3. **Explicit identity:** Resource contents should identify the Rhino document and, where applicable, the Grasshopper session, revision, solution/result, units, timestamp, and server version. IDs are opaque.
4. **Truthful state:** Distinguish live state, snapshots, saved versions, pending jobs, stale results, and unavailable data. An empty list must not mean an unavailable service.
5. **Predictable scope:** Do not silently expose arbitrary filesystem paths, private data, full model geometry, or unrelated documents. Resource access must honor the server's existing access controls and caller context.
6. **Bounded responses:** Return summaries by default; define limits, paging or separately addressable subresources for large models. Avoid putting an entire model into an agent's context accidentally.
7. **No hidden side effects:** Use tools for mutations and computationally expensive operations; prompts should describe workflows rather than execute them merely by being retrieved.
8. **Protocol compatibility:** Advertise only implemented capabilities, negotiate a supported MCP protocol version, and test resource and prompt behavior against that negotiated version. Optional notification mechanisms must match that version rather than a newer protocol assumed by a client.
9. **Untrusted content:** Object names, document text, component descriptions, annotations, and other project data are data, not instructions. Clients should not treat them as authorization or override system/user instructions.
10. **No accidental duplication:** Shared logic should back both tools and resources where appropriate, with consistent values and documented differences. Maintain existing tool contracts unless a versioned change is intentional.

## 4. Proposed concrete resources

The URI schemes below are illustrative. The final namespace, MIME types, and schemas are implementation decisions; document stable canonical URIs once chosen.

### 4.1 Server and capability overview

**Example URI:** `mcp4rhino://server/capabilities`

Return a compact machine-readable summary of server and Rhino versions, negotiated capabilities, supported transports and protocol versions, Grasshopper availability, supported value categories, applicable limits, and links to help resources. Distinguish installed component inventory from the subset supported by MCP4Rhino.

**Value:** A client can choose a feasible workflow and report a precise limitation before attempting graph changes.

**Acceptance:** The resource agrees with the initialized server and the actual supported catalog; an unavailable or uninitialized subsystem is marked accurately rather than inferred to be empty.

### 4.2 Active Rhino document overview

**Example URI:** `rhino://documents/active`

Return an opaque document identifier, display name where available, whether it is saved or modified when available, model units and tolerance, active layer and view where available, and bounded counts by object type. Include an explicit indication when no document is active.

**Value:** Geometry creation can respect the user's unit system and document context.

**Acceptance:** Switching documents changes the returned document identity and values; no file contents or unnecessary absolute paths are disclosed by default.

### 4.3 Available Grasshopper documents and sessions

**Example URI:** `grasshopper://sessions`

Return accessible open definitions and MCP sessions: opaque IDs, display names, attachment/ownership status, revision where known, save/modified status, and solution availability. Clearly distinguish a Grasshopper document from an MCP session attached to it.

**Value:** Clients can choose the intended definition and recognize when a graph may have changed.

**Acceptance:** The list is consistent with `gh_list_documents`, indicates unavailable Grasshopper separately, and does not attach, create, close, or activate a document merely because it was read.

### 4.4 Generic guidance and examples index

**Example URI:** `grasshopper://guides/index`

Provide a concise index of stable guidance: graph lifecycle, component discovery, data trees, input values, solving, inspection, visual previews, baking, saving, and troubleshooting. Link to specific guide resources and versioned generic recipes. Existing `gh_help` content can be reused.

**Acceptance:** Index links resolve, state their content type and version, and preserve the distinction between preview geometry and baked Rhino objects.

## 5. Proposed resource templates

Templates describe parameterized resource URIs. The following examples are proposed patterns, not an assertion that the server already supports them. Exact identifiers and URI encoding must be documented.

### 5.1 Document and object inspection

- `rhino://documents/{document_id}`: units, tolerance, metadata, counts, and state.
- `rhino://documents/{document_id}/layers/{layer_id}`: layer metadata and bounded counts.
- `rhino://documents/{document_id}/objects/{object_id}`: type, name, layer, visibility, bounds, and selected non-sensitive attributes.
- `rhino://documents/{document_id}/views/{view_id}`: projection and camera state, without changing the active view.

**Requirements:** Use IDs that remain unambiguous within the documented lifetime; return a clear not-found or unavailable error for deleted objects or closed documents. Do not accept arbitrary filesystem paths as resource identifiers. Include units with measured bounds. Large geometry belongs behind explicit, bounded export or tool workflows rather than default resource reads.

### 5.2 Grasshopper graph inspection

- `grasshopper://sessions/{session_id}/graph`: graph summary, component IDs, nodes, connections, input values where safely representable, support status, and revision.
- `grasshopper://sessions/{session_id}/nodes/{node_id}`: port details, runtime messages, settings, and connections.
- `grasshopper://components/{component_id}`: installed component descriptor, input/output ports, access modes, data types, support status, and available documentation.
- `grasshopper://sessions/{session_id}/results/{result_id}`: solution revision, completion state, diagnostics, output identifiers and bounded summaries.

**Requirements:** Reuse actual runtime discovery instead of relying on component display names. Document branch paths, item order, tree mappings, type conversions, numeric precision, and whether persistent values or runtime results are being reported. Mark results expired after changes, manual edits, re-solving, or reference changes as applicable; never substitute a newer result silently.

Resource reads must not queue a solve. If information requires computation or discovery with side effects, return an explicit unavailable/not-initialized state and direct the client to the appropriate existing tool.

### 5.3 Reusable documentation and recipes

- `grasshopper://guides/{topic}`: human-readable guidance for a named topic.
- `grasshopper://recipes/{recipe_id}`: a generic, versioned recipe containing purpose, inputs, units, discovered component requirements, graph-building steps, expected outputs, and limitations.

Recipes must be templates, not assumed portable component GUIDs. Include support/version requirements and describe how clients discover actual ports. A recipe should never silently create or edit a graph merely by being read.

## 6. Prompt catalog

Prompts are user-invoked reusable message templates. They should provide a clear task structure and optional arguments; retrieving a prompt must not perform changes. An MCP host may expose prompts differently, so do not rely on a particular slash-command UI.

### 6.1 Inspect a model

**Suggested arguments:** document identifier or active document; optional focus such as layers, objects, or units.

Guide the assistant to inspect the appropriate document, report units and relevant content, summarize uncertainty, and distinguish retrieved facts from assumptions. Do not modify the model.

### 6.2 Plan a parametric definition

**Suggested arguments:** goal, parameters, preferred units, and target document where supplied.

Guide the assistant to identify independent inputs, derived values, data-tree relationships, expected outputs, constraints and unknowns; discover supported components and ports; and produce a feasible graph plan. Treat domain standards as external, versioned requirements rather than universal server behavior.

### 6.3 Create and verify a definition

**Suggested arguments:** design intent, input values or ranges, output types, and optional save location.

Guide the assistant to use a separate visible definition by default, discover components, use validated patches, solve, inspect diagnostics and geometry, vary an input, and verify the expected change. Preserve user changes and report any unsupported operations. Saving and baking should be explicit actions consistent with user intent and access checks.

### 6.4 Diagnose a Grasshopper graph

**Suggested arguments:** session ID; optional node or observed issue.

Guide the assistant to check revision and solution state, inspect the first failing component and upstream inputs, understand tree structure, and distinguish server errors from Grasshopper component errors. Propose or apply a focused fix within the user's authorization and verify the resulting solve.

### 6.5 Explore a parameter variation

**Suggested arguments:** session ID, parameter name or node ID, and intended value or range.

Guide the assistant to capture baseline inputs and revision, change only the requested parameter, solve, compare named outputs and diagnostics, and report units and effects. Avoid comparing results from different definitions or outdated solution revisions.

### 6.6 Prepare an output snapshot

**Suggested arguments:** session ID, selected outputs, destination layer or file where applicable.

Explain the difference between preview and baked geometry. Inspect the current solution and selected outputs, use an explicit bake or export tool as appropriate, and report created IDs, output location and whether objects are independent snapshots. Avoid deleting or replacing existing geometry unless that behavior is expressly requested and properly scoped.

### 6.7 Prepare a model summary or report

**Suggested arguments:** document or session ID, intended audience, requested views, and output format.

Collect named inputs, measurement outputs, diagnostics, provenance and images that correspond to the same solution revision. State unmodeled elements and assumptions, distinguish screenshots from dimensioned drawings, and avoid asserting structural, regulatory, or manufacturing approval.

## 7. Capability and protocol requirements

### 7.1 Discovery and reads

- Advertise `resources` and/or `prompts` only once implemented, with accurate optional capability flags.
- Implement the relevant list and read/get methods for the negotiated protocol version. Document how concrete resources and templates are discovered, how arguments are encoded, and which data is available without initializing Grasshopper.
- Provide meaningful names, descriptions, MIME types, stable schemas, and informative errors. Support protocol pagination where required for list endpoints; for large resource contents, document an explicit bounded retrieval mechanism supported by the selected MCP version and client interface. Do not invent query or pagination behavior as though it were standardized.
- Resource templates must use the relevant MCP URI-template rules; validate identifiers, reject unsupported URIs, and avoid filesystem path traversal.
- If argument completion is implemented for prompts or templates, suggest only values the caller may access; do not disclose other documents or object IDs.

### 7.2 Dynamic state and freshness

- Include a freshness marker or revision when state changes. A revision must identify what it covers; a document revision, graph revision and solution ID are not interchangeable.
- Describe lifetime and expiration for session IDs, object IDs, graph revisions, and result IDs, including behavior after Rhino restarts and user edits.
- If subscriptions or change notifications are added, use the mechanisms and capability declarations for the **negotiated** protocol version; clearly document whether notifications indicate an updated resource, a changed resource list, or both. Clients must still handle missed notifications and perform a fresh read.
- Define caching guidance appropriate to the negotiated protocol. Never allow a cached resource or screenshot to be represented as the current solution without its associated identity and revision.

### 7.3 Permissions, privacy, and reliability

- Apply access checks consistently across tools, resources, templates, and prompts. Bind document/session identifiers to an authorized context and avoid leaking local paths, user text, metadata, or screenshots by default.
- A resource read or prompt retrieval must not execute user-provided instructions embedded in a model or send model data to a third party.
- Handle closed documents, deleted objects, unloaded Grasshopper, interrupted solutions, disabled components, large graphs, unsupported components, and concurrent changes with explicit status and recoverable errors.
- Keep read-oriented requests bounded and responsive; offer tools or asynchronous jobs for expensive geometry operations and captures.

## 8. Related capabilities that should remain tools

These are separate recommendations, not resource or prompt requirements:

- **Canvas capture:** An explicit Grasshopper canvas capture tool with session identity, actual image dimensions, optional fit-to-contents, and clear behavior when the editor is hidden or obscured. `capture_viewport` currently captures Rhino viewports; it should not be described as a Grasshopper canvas capture.
- **Graph mutations and computation:** Continue to use explicit tools for opening/attaching documents, adding nodes, connecting ports, changing values, solving, cancelling, saving and closing.
- **Geometry creation and export:** Use explicit tools for baking, export, and file writes; return created IDs and paths where appropriate.
- **Generic component support:** Expose and test additional native Grasshopper component families and data-tree operations through discovery and patch workflows. Resources and prompts cannot compensate for a missing executable component.

## 9. Generic acceptance tests

1. Initialize against each supported protocol version and verify that advertised capabilities match implemented methods. Existing tool-only clients continue to work.
2. Read the active document summary and compare units, identity, and counts with existing tools. Switch the active document and verify that the returned identity and state update.
3. With Grasshopper unavailable or uninitialized, resource reads report that state accurately and do not create or attach documents or queue a solve.
4. Create a small generic definition with a numeric slider and native geometry component through existing tools. Read its graph resource and verify node identities, connection endpoints, slider value and revision.
5. Modify the slider or graph manually and through MCP. Verify that the next read exposes a changed revision or an explicit freshness state and that stale result resources are not presented as current.
6. Solve and inspect a result resource: verify result identity, graph revision, units, output counts, diagnostics and geometry bounds agree with existing result tools. Test pagination or bounded retrieval for large outputs.
7. Query a component resource using a valid runtime ID and an invalid/deleted ID. Verify meaningful output or an explicit error. Test that component support status distinguishes installed from MCP-supported.
8. List and retrieve a generic prompt with and without optional arguments. Verify it does not modify Rhino or Grasshopper or assume a specific domain.
9. Verify access boundaries, opaque-ID scoping, invalid URI handling, no accidental disclosure of arbitrary local files, and safe treatment of model text.
10. Where notifications are supported, test negotiated-version behavior, subscription opt-in, resource changes, reconnects and missed notifications. Where unsupported, clients can still obtain current state by reading again.
11. Test Rhino/Grasshopper versions and operating systems in the product's support matrix; publish observed differences and limitations.

## 10. Suggested implementation order

**Phase 1 — Readable context:** capability overview, active-document overview, Grasshopper sessions, guidance index, graph/node/component resources, and basic generic prompts. Reuse existing tool logic where possible.

**Phase 2 — Solution and provenance:** result resources, standardized units and geometry summaries, revision/freshness rules, bounded reads, and generic variation/report prompts.

**Phase 3 — Optional integration:** version-compatible notifications, argument completion, reusable generic recipes, and a dedicated Grasshopper canvas capture tool. Continue expanding generic component and data-tree support independently.

## 11. Developer deliverables

- A resource/template/prompt catalog with names, descriptions, example URIs, schemas and version applicability.
- A compatibility statement for supported MCP protocol versions, Rhino/Grasshopper versions, transports and operating systems.
- A documented security and side-effect model, including identity lifetimes and file/data exposure.
- Automated protocol, resource, prompt, stale-state and access-control tests, plus a live Rhino/Grasshopper smoke test.
- Migration notes confirming whether existing tool interfaces are unchanged.

## References

- [MCP SDK overview: tools, resources, prompts and templates](https://py.sdk.modelcontextprotocol.io/get-started/first-steps/)
- [MCP client guidance: listing resources, templates and prompts](https://py.sdk.modelcontextprotocol.io/client/)
- [Model Context Protocol specifications](https://modelcontextprotocol.io/specification/)
- [Rhino Grasshopper API: GH_Document](https://developer.rhino3d.com/api/grasshopper/html/T_Grasshopper_Kernel_GH_Document.htm)
