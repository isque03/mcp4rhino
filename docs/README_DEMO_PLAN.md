# Architectural README animation

The README demo is a 21-second time-lapse of actual Rhino viewport captures:
site and parking, office floors, curtain-wall glazing, trees, and people. It uses
a fixed street-level construction camera followed by a rising orbit, pale glass,
dark outlines, and a blue background inspired by the supplied architectural illustration. The sky is a simple gradient;
there is no photographic backdrop. Captions and timing are added during export.

## Outputs

- `docs/media/mcp4rhino-office-park.gif`: looping, 800 × 592, 210 frames, 21 seconds,
  with a rising fly-around finale.
- `docs/media/mcp4rhino-office-park.mp4`: H.264 master with the same timeline
  and camera path.
- Each capture run retains original PNGs, `calls.jsonl`, `timeline.json`, captioned
  frames, and both exports under a unique `artifacts/office-demo-*` directory.
  These working files are ignored by Git.

## Reproduce

Requires local Rhino 8 with MCP4Rhino running at `http://127.0.0.1:4010/mcp`,
Python 3 with Pillow 10.1 or newer, and FFmpeg with PNG, GIF, and H.264 support.
The encoder does not require FFmpeg's optional `drawtext` filter.

Install the current tools build using `bash scripts/hot-reload-tools.sh`, then run
`MCP4RhinoReload` in Rhino or call `mcp4rhino_reload`. This version adds the
`set_illustration_style` MCP tool. It creates or updates a separate
**MCP4Rhino Illustration** display preset, hides grid/axes in the selected view,
and leaves built-in modes unchanged. Switch back to Shaded to leave the preset.

Use a dedicated, empty Rhino document, then run:

```sh
python3 -m venv artifacts/demo-env
artifacts/demo-env/bin/pip install 'Pillow>=10.1'
artifacts/demo-env/bin/python scripts/demo_office_park_gif.py
```

To deliberately replace the entire active scene, add `--reset-scene`. **This deletes
all current model objects** and sets document units to metres. Without the flag,
the script refuses to build in a nonempty document. It checks that the illustration
tool is available before deleting anything.

The script prints its new run directory. Inspect `capture_17.png` for composition
and play `demo.gif` to review the completed timeline. Outputs remain in the run
directory until published. To encode existing captures and replace README media:

```sh
python3 scripts/demo_office_park_gif.py --encode-only artifacts/office-demo-RUN --publish
```

Replace `office-demo-RUN` with the printed directory name. `--skip-encode` builds
and captures without encoding. `--encode-only` never modifies Rhino. Encoding
rejects GIFs over 5 MiB before publication.

## Construction and timing

| Time | Action |
| --- | --- |
| 0–1.5 s | Finished-scene preview |
| 1.5–3.5 s | Site, sidewalk, parking stripes and cars |
| 3.5–6 s | Context building and four office floors |
| 6–8.5 s | Curtain walls by story, then entrance |
| 8.5–11 s | Trees and people |
| 11–12 s | Finished street view held still |
| 12–19 s | Rising 75-degree fly-around |
| 19–21 s | Elevated finished view held still |

This is a time-lapse, not a claim of real-time construction speed. Frames are
captured after successful MCP construction batches. The first and last frames
match. Trees use rounded, irregular foliage clusters and people use distinct standing
and walking profiles. These flat silhouettes turn toward the camera at every
flight step. The flight rises 23 metres while easing around a 75-degree arc;
it is a partial orbit, not a full 360-degree tour.

## Implementation

- `scripts/demo_office_park_gif.py`: MCP gateway, error handling, call log,
  scene-reset guard, capture sequence, and publication.
- `scripts/office_demo_scene.py`: scene geometry and palette. Curtain-wall frame
  and glass IDs are explicitly reassigned to separate demo layers.
- `scripts/office_demo_entourage.py`: rounded foliage, human profiles, and the
  eased rising camera path.
- `scripts/office_demo_video.py`: caption layout, 10 fps timing, palette-based GIF
  encoding, and MP4 export. The complete viewport is resized without distortion and given a separate
  caption strip. A 96-color palette with no dithering keeps the longer GIF below
  5 MiB.
- `src/MCP4Rhino.Tools/IllustrationDisplay.cs`: Rhino display preset. Direct API
  settings replace the old background command strings that Rhino rejected.

The current macOS viewport capture is 1310 × 838; capture dimensions depend on
Rhino's viewport. The export preserves aspect ratio, so a different viewport can
produce a different output height. Review the composition after resizing Rhino.

## Verification

```sh
python3 -m unittest discover -s tests/demo -v
bash scripts/test-coverage.sh
```

The demo suite tests refusal to clear an unapproved scene, command failures,
missing capture files, separate glass/frame layers, valid geometry, deterministic
trees, the exact 21-second timeline, matching loop endpoints, caption layout, and
encoding frame counts. The implementation run passed 19 demo tests (90% combined
Python statement coverage) and 100 existing .NET tests. The .NET coverage gate
covers Logic/Contracts; Rhino display behavior was verified through live captures.

The completed GIF was also opened in a browser and checked with FFprobe for
800 × 592 dimensions, 210 frames, and a 21-second duration.
