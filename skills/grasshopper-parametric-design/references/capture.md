# Capture and explicitly show a definition

`gh_capture_canvas` renders the native Grasshopper graph, including components, sliders, and wires. It is distinct from Rhino's `capture_viewport`. A detached canvas renders the requested definition without showing the editor, switching the active document, changing its pan/zoom, or capturing tutorial dialogs and other window overlays.

```json
{"session_id":"SESSION","expected_revision":4,"result_id":"RESULT","framing":"fit","width":1600,"height":1000}
```

The session is required. Revision and result guards are optional for inspecting an unsolved graph; provide both when associating a capture with solved outputs. Framing defaults to `current`, which requires this definition to be active. `fit` includes all graph objects with padding, including an empty graph's default region. `rectangle` requires `[x,y,width,height]` in canvas coordinates. Rectangles preserve aspect ratio and may letterbox. Pixel dimensions range from 64 to 4096 on each axis. Large graphs may require rectangle captures to keep labels legible. Metadata reports zoom, visible/requested rectangles, and any objects outside the frame through `objects_outside_frame` and `truncated`.

An optional absolute `.png` path selects the destination; otherwise a unique file is written under the system temporary directory's `MCP4Rhino` folder. Existing files require `overwrite:true`. Metadata includes actual/requested dimensions, framing, rectangle, MIME type, and session/document/revision/result identity. Native rendering failures return an explicit error; a platform without working native canvas rendering does not return a substitute viewport screenshot.

Text metadata is the first MCP content block. Valid PNGs up to 1 MiB also appear as an image block. Larger images remain on disk with `image_embedded:false` and `image_omitted_reason:"exceeds_max_bytes"`.

To deliberately bring a definition into view, use `gh_show_document` with `session_id`, `expected_revision`, `request_id`, and optional `fit:true`. This changes editor view state without changing graph revision. Reusing the same request ID returns its original receipt; use a new request ID when you intend another activation after a user changes the view. Component nicknames/positions remain editable through `set_properties`; newly added nodes without explicit positions use dependency-based layout.

For a Rhino preview image, explicitly activate the definition first, then call:

```json
{"grasshopper":{"session_id":"SESSION","expected_revision":4,"result_id":"RESULT"}}
```

as arguments to `capture_viewport`, alongside any existing view/path options. The host checks the active Rhino document, active preview definition, revision, and result before and after capture. Metadata includes `grasshopper` identity. This associates the image with a current preview; the image can also contain other visible Rhino geometry and is not dimensional proof. Canvas and viewport metadata must match the report's result identity.

Canvas capture uses a version-guarded native render-context binding to avoid the public canvas document setter's implicit solve/autosave behavior. An incompatible native canvas returns `canvas_capture_unavailable`. Explicit activation suppresses implicit solutions while switching documents. Canvas capture and explicit editor display change the persistent adapter/host and require a full package install and Rhino restart. Native rendering on each platform remains an alpha acceptance check.
