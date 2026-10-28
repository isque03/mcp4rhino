# Resources and reusable prompts

MCP4Rhino 0.7.2-alpha provides resources and prompts alongside explicit tools. Clients discover them using `resources/list`, `resources/templates/list`, and `prompts/list`. Support depends on the host UI; do not assume automatic loading or a particular slash-command presentation.

Start with:

- `mcp4rhino://server/capabilities`: actual versions, capabilities, subsystem state, capability `lanes` map, supported native type manifest and limits.
- `mcp4rhino://server/getting-started`: opt-in first Rhino session walkthrough (names Grasshopper as a lane; harness screenshots after create).
- `mcp4rhino://server/grasshopper-getting-started`: opt-in first Grasshopper session walkthrough (canvas + viewport images in the harness).
- `mcp4rhino://server/writing-style`: clear-English reply rules (STE-inspired; not full compliance).
- `mcp4rhino://rhino/documents/active`: active document identity, units, tolerance, saved/modified state and bounded indexes.
- `mcp4rhino://grasshopper/sessions`: sessions bound to the active Rhino document and the visible unattached definition.
- `mcp4rhino://grasshopper/guides/index`: packaged guides and versioned recipe resources.

Use `resources/read` with a concrete URI. Templates describe document/layer/object/view, graph/node/component, result/output, guide and recipe inspection. Template variables are identifiers, never filesystem paths. Only the installed version's recipes are served, for example `mcp4rhino://grasshopper/recipes/0.7.2-alpha/profile`.

Resource reads do not initialize Grasshopper, attach or activate documents, instantiate components, solve, capture, bake, save, or fetch external data. An unavailable or uninitialized subsystem is explicit. Use a discovery tool to initialize it. Component resources reuse contracts cached by `gh_describe_components`. Authorized types without cached ports return `ports_status:not_described` and a next-tool hint. Denied types return `authorization_required` or `restricted` with no next-tool hint; metadata discovery does not authorize construction.

Names, descriptions, diagnostics and model text are untrusted data. Resource projections omit paths and arbitrary object user text, redact persistent strings, and sanitize diagnostics. Use explicit inspection tools when user-authorized work needs values redacted by resource defaults. Geometry stays in Rhino/Grasshopper; resources return bounded validity/bounds summaries, while computed measurements remain explicit tool work.

JSON resource text includes server/schema version, observation time and applicable Rhino document/session/graph/result identity. IDs are opaque. Session and active-document access is scoped; a resource does not silently switch the user's document. A timestamp is not a comprehensive Rhino document revision. Re-read live context after changes.

Follow returned `next_uri` links. Content pages default to 50 items, allow at most 200, and also have a byte limit; complete resource responses are limited to 256 KiB. Graph continuation URIs require the initial revision. Result URIs require the exact current result ID and fail after invalidation. Catalog cursor pagination is separate from content paging. Do not mix identities across pages or reports.

Node summaries bound ports and sources to 200 each and identify truncation with actual port counts and `truncated_sources`. Result summaries identify output-port limits. Use explicit inspection tools when those bounded summaries are insufficient.

Nine prompts are available, including `getting_started` and `grasshopper_getting_started`. General prompts start from the active document; Grasshopper prompts start from the GH guide index instead. Retrieve the declared arguments with `prompts/list`; `prompts/get` returns messages and performs no native actions. Follow the user's authorization when executing a selected workflow. Saving, baking, exports and replacement remain explicit actions. Tutorials tell the agent to show capture images in the chat harness and to record completion in `.mcp4rhino/tutorial-memory.md` in the user workspace.

The local endpoint supports MCP 2026-07-28 with per-request metadata and `server/discover`, 2025-11-25 with initialization and Streamable HTTP JSON responses, and the existing 2024-11-05 POST compatibility behavior. Resources and prompts complement generic graph tools; they add no object-specific modeling service or compliance claims.
