# Resources, prompts, and protocol compatibility

MCP4Rhino 0.6.5-alpha continues read-only resources and reusable prompts alongside the existing tools, with balanced lane guidance so Grasshopper is not the default path. The endpoint remains `http://127.0.0.1:4010/mcp`. These capabilities require the complete matching package and a Rhino restart; Tools-only hot reload cannot update host or shared contracts.

## Protocol profiles

| Version | Entry point | HTTP behavior |
|---|---|---|
| `2026-07-28` | `server/discover`, or a request with modern metadata | Stateless Streamable HTTP, JSON response mode; required metadata and mirrored headers |
| `2025-11-25` | `initialize` | Handshake-based, stateless Streamable HTTP, JSON response mode; subsequent requests identify the version in `MCP-Protocol-Version` |
| `2024-11-05` | Existing `initialize` and headerless POST calls | Existing custom local POST compatibility, including batches; this is not a new implementation of deprecated HTTP+SSE |

All profiles expose tools, resources, and prompts. Optional subscriptions, change notifications, completion, SSE streams, protocol session IDs, and server-initiated requests are not advertised. Existing tool names, arguments, and content payloads are preserved. Modern result envelopes additionally contain `resultType:"complete"` and server identity metadata. Cacheable modern results contain `ttlMs:0` and `cacheScope:"private"`.

Modern POST requests use `Content-Type: application/json` and `Accept: application/json, text/event-stream`. Version `2026-07-28` requires `params._meta["io.modelcontextprotocol/protocolVersion"]` and `params._meta["io.modelcontextprotocol/clientCapabilities"]`. Client identity is optional; if supplied, it must contain name and version. The protocol header must agree with metadata. `Mcp-Method` must match the method; `Mcp-Name` must match `params.name` for tool/prompt calls or `params.uri` for resource reads. Header names are case-insensitive; values are case-sensitive. Unsafe names use the protocol's UTF-8 Base64 sentinel representation.

```json
{
  "jsonrpc":"2.0","id":1,"method":"server/discover",
  "params":{"_meta":{
    "io.modelcontextprotocol/protocolVersion":"2026-07-28",
    "io.modelcontextprotocol/clientCapabilities":{},
    "io.modelcontextprotocol/clientInfo":{"name":"Example client","version":"1"}
  }}
}
```

Send the example with `MCP-Protocol-Version: 2026-07-28` and `Mcp-Method: server/discover`. A modern request missing headers is rejected rather than silently treated as legacy. Modern batches are rejected; accepted legacy notifications receive HTTP 202 with no body. Unsupported modern versions return HTTP 400 / `-32022`, header mismatches 400 / `-32020`, and unknown modern methods 404 / `-32601`. Invalid parameters use `-32602`; missing resources use `-32002`. Expired graph results and revision conflicts are explicit resource errors, never replacements with a newer result.

`GET /health` is the health endpoint. The existing plain, headerless `GET /mcp` probe remains available. Versioned GET, SSE GET, DELETE, and other unsupported MCP methods return 405. Request bodies are limited to 4 MiB. The listener stays bound to `127.0.0.1`; non-local Host values and foreign present Origin headers are rejected. Native clients may omit Origin. This preserves the local tools' trust model; opaque document IDs and resource filtering are not user authentication or remote multi-tenant isolation.

## Concrete resources

Discover resources with `resources/list` and templates with `resources/templates/list`. Lists use protocol `cursor`/`nextCursor` pagination. Cursors are tied to the catalog and package version.

| URI | MIME type | Contents |
|---|---|---|
| `mcp4rhino://server/capabilities` | `application/json` | Protocol/server/Rhino versions, implemented capabilities, subsystem state, capability `lanes` map, supported native type manifest, value categories, limits |
| `mcp4rhino://server/getting-started` | `text/markdown` | Opt-in first Rhino session walkthrough; reading it does not mutate the model |
| `mcp4rhino://server/writing-style` | `text/markdown` | Clear-English (STE-inspired) rules for agent replies; not full ASD-STE100 compliance |
| `mcp4rhino://rhino/documents/active` | `application/json` | Active document identity, units/tolerances, saved/modified state, counts, and bounded object/layer/view indexes |
| `mcp4rhino://grasshopper/sessions` | `application/json` | Active-document MCP sessions and the current visible unattached definition, separately identified |
| `mcp4rhino://grasshopper/guides/index` | `text/markdown` | Packaged guidance with resolvable resource links and versioned recipe links |

Read a resource using `resources/read` with `{"uri":"..."}`. The result contains `contents`, whose first entry has URI, MIME type, and text. JSON resource text includes schema/server version, observation time, live-read freshness, and applicable identities. No active Rhino document, unavailable native bridge, uninitialized Grasshopper, and busy native reads are explicit states; absent data is not disguised as an empty collection.

## Templates and identity

All paths below are relative to the `mcp4rhino://` namespace:

| Template | Description |
|---|---|
| `rhino/documents/{document_id}{?offset,limit}` | Active document overview and indexes |
| `rhino/documents/{document_id}/layers/{layer_id}` | Layer metadata and bounded object count |
| `rhino/documents/{document_id}/objects/{object_id}` | Type/name/layer/visibility/locking and bounds |
| `rhino/documents/{document_id}/views/{view_id}` | View identity, projection and camera state |
| `grasshopper/sessions{?offset,limit}` | Accessible sessions |
| `grasshopper/sessions/{session_id}/graph{?offset,limit,expected_revision}` | Nodes, connections, ports, mappings, persistent samples, diagnostics, support status |
| `grasshopper/sessions/{session_id}/nodes/{node_id}{?expected_revision}` | One existing node |
| `grasshopper/components/{component_id}` | Installed descriptor and cached port contract |
| `grasshopper/sessions/{session_id}/results/{result_id}{?offset,limit,expected_revision}` | Current solution identity, diagnostics, output IDs/counts and output-resource links |
| `grasshopper/sessions/{session_id}/results/{result_id}/nodes/{node_id}/outputs/{output_index}{?offset,limit,expected_revision}` | Bounded output rows and geometry summaries |
| `grasshopper/guides/{topic}` | Packaged Markdown guidance; allowlisted compound topics such as `examples/profile` are supported |
| `grasshopper/recipes/{version}/{recipe_id}` | Packaged JSON recipe at an explicit package version |

Document IDs are opaque process-lifetime GUIDs shared across host and Tools hot reload. They do not expose Rhino serial numbers or paths. A Rhino ID is readable only while that document is active; switching documents does not authorize reads of the prior document. Closing or restarting invalidates its access. Object/layer/view GUIDs retain their native lifetimes. Grasshopper document IDs, MCP session IDs, graph revisions, and result IDs identify different things and must not be interchanged.

Only sessions bound to the active Rhino document are listed/readable. The current visible unattached definition can be identified without creating a session; unrelated inactive definitions are not enumerated. Component descriptors are installation-wide non-model metadata. Only explicit discovery tools initialize Grasshopper. `gh_describe_components` populates port-contract caches; a component resource with `ports_status:"not_described"` returns its descriptor and a next-tool hint without instantiating it.

## Paging, freshness, and privacy

Resource content paging is a server-defined URI convention, advertised using RFC 6570 templates. It is separate from protocol catalog cursors. Default page size is 50, maximum 200. Each page also has a byte budget and may return fewer items. Collection pages return `next_offset` and a fully formed `next_uri`; independent indexes may have different continuations. Entire resource responses are limited to 256 KiB. Oversized individual items fail explicitly with an inspection-tool recovery message.

Node resources show at most 200 ports per side and 200 sources per port. They include actual port counts, limits, and `truncated_sources` entries with the actual source count. Result summaries likewise expose output-port count/limit. Use explicit inspection tools for larger connection details. Output paging skips branch/item positions before formatting rows, so later pages do not recompute summaries for earlier items.

Graph continuations require the first page's `expected_revision`. Every result URI binds to one exact current result; edits, undo/redo, source changes, unit changes and re-solving can invalidate it. A graph continuation never substitutes current nodes for an older revision. Rhino observation timestamps mark live reads, not a comprehensive document revision or a cross-page snapshot transaction. Re-read current context after changes. Live responses are immediately stale for caching purposes. Versioned recipes are immutable within their packaged version; only the installed recipe version is served.

Tree paths, item order, nulls and empty branches are preserved. Resource geometry summaries expose validity and bounds in model units; expensive area, volume, length and intersection calculations remain explicit `gh_read_output`/summary tool work. Numeric outputs retain graph-defined meaning; no dimension or approval is inferred.

Filesystem paths and arbitrary object user text are omitted. Persistent string values are redacted; path-bearing diagnostic strings are sanitized and long text is marked truncated. Names, component descriptions and diagnostics are untrusted project data. Resources neither export full geometry nor accept arbitrary file/network URIs. Their read interfaces cannot open/attach/activate/solve/bake/save/capture models. Help/recipes use embedded package assets rather than runtime filesystem reads or network fetches.

Native reads have one pending UI request and a 500 ms HTTP wait budget; a blocked Rhino UI returns explicit `busy`. This timeout does not terminate already-running native work. Per-type and per-layer counts sample at most 10,000 non-deleted objects and expose completeness/counting metadata. Large-model counts must not be treated as complete when `counts_complete:false`.

## Prompts

`prompts/list` advertises each prompt's declared string arguments; `prompts/get` returns reusable messages only. Unknown, missing required, non-string, or oversized arguments fail before native access. Argument values are serialized as untrusted JSON data. Retrieval is not authorization to execute the workflow. General prompts start from `server/capabilities` and `rhino/documents/active`. Grasshopper prompts start from `server/capabilities` and `grasshopper/guides/index` instead of the active-document URI.

| Prompt | Required arguments |
|---|---|
| `getting_started` | None; optional `goal` |
| `inspect_model` | None |
| `plan_parametric_definition` | `goal` |
| `create_and_verify_definition` | `design_intent` |
| `diagnose_graph` | `session_id` |
| `explore_parameter_variation` | `session_id`, `parameter`, `value_or_range` |
| `prepare_output_snapshot` | `session_id`, `outputs` |
| `prepare_model_report` | None; supply document/session and intended audience as appropriate |

Prompts preserve user changes, require supported-component discovery for Grasshopper workflows, distinguish preview from snapshots, and require explicit save/bake/export intent. Reports must associate measurements and images with the same document or solution identity. No staircase service or regulatory/manufacturing approval is added.

## Validation and migration

Automated tests cover all three profiles, current envelopes/headers/errors, real local HTTP calls, concurrent clients, catalog and URI validation, bounded paging, privacy, prompt argument handling, and no native gateway/tool access for static discovery/help/prompts. These do not establish native Rhino behavior.

After full package install and restart on licensed Rhino, run:

```bash
python3 grasshopper-smoke.py --context-only --protocol 2026-07-28
python3 grasshopper-smoke.py --example profile --resources --protocol 2026-07-28
python3 grasshopper-smoke.py --example array --resources --protocol 2025-11-25
python3 grasshopper-smoke.py --example array --resources --protocol 2024-11-05
```

The context-only run must leave Grasshopper uninitialized if it began that way. Graph runs compare resource/tool units, IDs and counts, verify descriptor cache behavior, reject stale results, and check that reads leave revision/result unchanged. Save/reopen remains enabled unless `--skip-save` explicitly records it unverified.

On Windows and macOS separately, manually switch active Rhino documents and test that old document/session IDs cannot be read until explicitly reactivated; delete objects; manually change sliders/undo/redo; test unavailable Grasshopper and a blocked native UI; inspect redaction and paging on a large graph. Record OS, Rhino build, package version, protocol profile and observed differences. The current workstation's unavailable Grasshopper/license state prevents native acceptance; stable promotion still requires licensed native results.

References: [2026 protocol schema](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/schema/2026-07-28/schema.ts), [2026 Streamable HTTP](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/streamable-http), [2025 transport](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports).
