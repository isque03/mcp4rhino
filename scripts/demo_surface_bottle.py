#!/usr/bin/env python3
"""Smoke: revolve bottle + loft + pipe via SurfaceFoundationTools (mm)."""
from __future__ import annotations

import json
import urllib.request
from pathlib import Path

URL = "http://127.0.0.1:4010/mcp"
OUT = Path.home() / "Library/Logs/MCP4Rhino"


def mcp(method, params=None, id=1):
    body = json.dumps({"jsonrpc": "2.0", "id": id, "method": method, "params": params or {}}).encode()
    req = urllib.request.Request(URL, data=body, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=180) as r:
        return json.loads(r.read().decode())


def call(name, args=None):
    res = mcp("tools/call", {"name": name, "arguments": args or {}})
    if res.get("error"):
        raise RuntimeError(f"{name}: {res['error']}")
    r = res.get("result", {})
    text = r.get("content", [{}])[0].get("text", "{}")
    if r.get("isError"):
        raise RuntimeError(f"{name}: {text}")
    return json.loads(text)


def ids_of(o):
    if "id" in o:
        return [o["id"]]
    return list(o.get("ids", []))


def main():
    print("reload", call("mcp4rhino_reload", {}))
    tools = {t["name"] for t in mcp("tools/list")["result"]["tools"]}
    for n in ("revolve_surface", "loft_surface", "pipe_surface", "join_surfaces", "create_plane_surface"):
        assert n in tools, f"missing {n}"

    old = call("get_objects", {"layer": "Demo_Bottle"})
    if old.get("objects"):
        call("delete_objects", {"ids": ",".join(o["id"] for o in old["objects"])})

    call("create_layer", {"name": "Demo_Bottle::Curves", "color": "#888888"})
    call("create_layer", {"name": "Demo_Bottle::Body", "color": "#4A90A4"})
    call("set_document_units", {"unit_system": "mm"})

    prof = call(
        "create_curve",
        {
            "points": [
                [20, 0, 0],
                [35, 0, 15],
                [40, 0, 50],
                [38, 0, 90],
                [28, 0, 120],
                [18, 0, 140],
                [16, 0, 160],
                [14, 0, 180],
            ],
            "degree": 3,
            "layer": "Demo_Bottle::Curves",
            "name": "bottle_profile",
        },
    )
    body = call(
        "revolve_surface",
        {
            "profile_id": prof["id"],
            "axis_start": [0, 0, 0],
            "axis_end": [0, 0, 200],
            "angle_deg": 360,
            "keep_input": True,
            "layer": "Demo_Bottle::Body",
            "name": "bottle_body",
        },
    )
    print("revolve", body)

    circle = call(
        "create_circle",
        {"center": [0, 0, 0], "radius": 20, "layer": "Demo_Bottle::Curves"},
    )
    base = call(
        "create_planar_surface",
        {"ids": [circle["id"]], "keep_inputs": True, "layer": "Demo_Bottle::Body"},
    )
    joined = call(
        "join_surfaces",
        {"ids": ids_of(body) + ids_of(base), "keep_inputs": False, "layer": "Demo_Bottle::Body"},
    )
    print("join", joined)

    c1 = call("create_circle", {"center": [200, 0, 0], "radius": 30, "layer": "Demo_Bottle::Curves"})
    c2 = call("create_circle", {"center": [200, 0, 80], "radius": 20, "layer": "Demo_Bottle::Curves"})
    print("loft", call("loft_surface", {"ids": [c1["id"], c2["id"]], "layer": "Demo_Bottle::Body"}))

    line = call("create_line", {"start": [300, 0, 0], "end": [300, 0, 100], "layer": "Demo_Bottle::Curves"})
    print("pipe", call("pipe_surface", {"curve_id": line["id"], "radius": 8, "layer": "Demo_Bottle::Body"}))

    plane = call(
        "create_plane_surface",
        {"origin": [400, 0, 0], "width": 60, "height": 40, "layer": "Demo_Bottle::Body"},
    )
    print("edges", call("list_surface_edges", {"id": plane["id"]}).get("count"))

    call("run_rhino_command", {"script": "_-SetDisplayMode _Viewport=_Active Mode=Shaded _Enter"})
    call("zoom_extents", {"layer": "Demo_Bottle"})
    call(
        "set_view",
        {
            "view": "Perspective",
            "camera": [250, -400, 220],
            "target": [200, 0, 80],
            "up": [0, 0, 1],
            "perspective": True,
        },
    )
    OUT.mkdir(parents=True, exist_ok=True)
    print(
        "capture",
        call(
            "capture_viewport",
            {
                "view": "Perspective",
                "width": 1400,
                "height": 1000,
                "path": str(OUT / "surface_smoke_bottle.png"),
            },
        ),
    )


if __name__ == "__main__":
    main()
