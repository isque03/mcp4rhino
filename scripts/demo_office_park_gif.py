#!/usr/bin/env python3
"""Build/capture an architectural MCP demo; requires local Rhino, MCP4Rhino and FFmpeg.

--reset-scene explicitly deletes ALL objects from the active Rhino document.
Without it, construction requires an empty document. Outputs stay in a fresh
artifacts directory until --publish is supplied after visual review.
"""
import argparse
from datetime import datetime
import json
from pathlib import Path
import shutil
import urllib.request

from office_demo_scene import OfficeScene, CAMERA, TARGET
from office_demo_video import encode
from office_demo_entourage import flight_camera

ROOT = Path(__file__).resolve().parents[1]
URL = 'http://127.0.0.1:4010/mcp'


def decode_response(response, name):
    if response.get('error'):
        raise RuntimeError(f'{name}: {response["error"]}')
    result = response.get('result', {})
    texts = [c['text'] for c in result.get('content', []) if c.get('type') == 'text']
    if result.get('isError') or not texts:
        raise RuntimeError(f'{name}: {texts}')
    data = json.loads(texts[0])
    if data.get('ok') is False:
        raise RuntimeError(f'{name}: operation failed: {data}')
    return data


class McpGateway:
    def __init__(self, directory):
        self.log = directory / 'calls.jsonl'
        self.sequence = 0

    def call(self, name, args=None):
        self.sequence += 1
        params = {'name': name, 'arguments': args or {}}
        body = json.dumps({'jsonrpc': '2.0', 'id': self.sequence,
                           'method': 'tools/call', 'params': params}).encode()
        req = urllib.request.Request(URL, data=body, headers={'Content-Type': 'application/json'})
        with urllib.request.urlopen(req, timeout=180) as response:
            raw = json.load(response)
        # Keep text metadata and errors; embedded PNG bytes are already on disk.
        logged = {**raw, 'result': {**raw.get('result', {}), 'content':
                  [c for c in raw.get('result', {}).get('content', []) if c.get('type') != 'image']}}
        with self.log.open('a') as stream:
            stream.write(json.dumps({'request': params, 'response': logged}) + '\n')
        return decode_response(raw, name)


class CaptureRun:
    def __init__(self, directory):
        self.directory = directory
        self.call = McpGateway(directory).call
        self.scene = OfficeScene(self.call)
        self.beats = []

    def capture(self, caption, ticks):
        path = self.directory / f'capture_{len(self.beats):02d}.png'
        self.call('clear_selection')
        self.call('capture_viewport', {'view': 'Perspective', 'path': str(path)})
        if not path.is_file():
            raise RuntimeError(f'Capture missing: {path}')
        self.beats.append({'file': path.name, 'caption': caption, 'ticks': ticks})
        print('Captured', caption, path.name, flush=True)

    def prepare(self, reset):
        objects = self.call('get_objects')['objects']
        if objects and not reset:
            raise RuntimeError('Use an empty document or --reset-scene to explicitly clear it.')
        # Check preset availability before destructive work on older plugin versions.
        self.call('set_illustration_style', {'view': 'Perspective'})
        if objects:
            self.call('delete_objects', {'ids': ','.join(o['id'] for o in objects)})
        self.call('set_document_units', {'unit_system': 'm'})
        self.scene.layers()
        self.call('set_active_view', {'view': 'Perspective'})
        self.call('set_view', {'view': 'Perspective', 'camera': CAMERA, 'target': TARGET,
                              'up': [0,0,1], 'perspective': True})

    def build(self):
        self.scene.site()
        self.capture('01   Lay out the site', 10)
        self.scene.parking()
        self.capture('01   Lay out the site', 10)
        self.scene.context()
        for i in range(4):
            self.scene.massing(i)
            self.capture('02   Build the office', 6 if i < 3 else 7)
        for i in range(4):
            self.scene.curtain(i)
            self.capture('03   Add the facade', 5)
        self.scene.entrance()
        self.capture('03   Add the facade', 5)
        for i in range(6):
            self.scene.tree(i)
            self.capture('04   Landscape the street', 3)
        for i in range(4):
            self.scene.person(i)
        self.capture('04   Landscape the street', 7)
        self.beats[-1]['ticks'] += 10
        self.fly_around()
        final = dict(self.beats[-1], caption='An office site, built through MCP', ticks=20)
        self.beats = [dict(final, ticks=15)] + self.beats + [final]
        (self.directory / 'timeline.json').write_text(json.dumps(self.beats, indent=2))


    def fly_around(self):
        for step in range(70):
            camera = flight_camera(step, 70, CAMERA, TARGET)
            self.call('set_view', {'view': 'Perspective', 'camera': camera, 'target': TARGET,
                                  'up': [0,0,1], 'perspective': True})
            self.scene.face_camera(camera)
            self.capture('05   Explore the finished building', 1)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reset-scene', action='store_true')
    parser.add_argument('--encode-only', type=Path, metavar='RUN_DIRECTORY')
    parser.add_argument('--skip-encode', action='store_true')
    parser.add_argument('--publish', action='store_true')
    args = parser.parse_args()
    if args.skip_encode and args.publish:
        parser.error('--publish requires encoding')
    if not shutil.which('ffmpeg'):
        parser.error('ffmpeg is required')
    directory = args.encode_only
    if directory is None:
        stamp = datetime.now().strftime('%Y%m%d-%H%M%S-%f')
        directory = ROOT / 'artifacts' / ('office-demo-' + stamp)
        directory.mkdir(parents=True)
        print('Run directory:', directory, flush=True)
        run = CaptureRun(directory)
        run.prepare(args.reset_scene)
        run.build()
    directory = directory.resolve()
    if not args.skip_encode:
        encode(directory)
        if args.publish:
            dest = ROOT / 'docs' / 'media'
            dest.mkdir(parents=True, exist_ok=True)
            for ext in ('gif', 'mp4'):
                shutil.copyfile(directory / ('demo.'+ext), dest / ('mcp4rhino-office-park.'+ext))


if __name__ == '__main__':
    main()
