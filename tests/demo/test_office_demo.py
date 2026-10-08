"""Demo contracts: scene ownership, tool failures, facade layers, and export timing."""
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts'))
from demo_office_park_gif import CaptureRun, McpGateway, decode_response
from office_demo_scene import OfficeScene, tree_canopy, LAYER
from office_demo_entourage import flight_camera, person_profile
from office_demo_video import encode, timeline_frames, render_frame


def response(data):
    return {'result': {'content': [{'type': 'text', 'text': json.dumps(data)}]}}


class ResponseTests(unittest.TestCase):
    def test_success_and_all_error_forms(self):
        self.assertEqual(decode_response(response({'id': 'a'}), 'create'), {'id': 'a'})
        errors = [{'error': {'message': 'failed'}}, {'result': {'isError': True}},
                  {'result': {'content': []}}, response({'ok': False})]
        for error in errors:
            with self.subTest(error=error), self.assertRaises(RuntimeError):
                decode_response(error, 'tool')

    def test_gateway_logs_metadata_without_embedded_images(self):
        with tempfile.TemporaryDirectory() as temp:
            raw = response({'path': 'capture.png'})
            raw['result']['content'].append({'type': 'image', 'data': 'large payload'})
            with patch('urllib.request.urlopen', return_value=io.BytesIO(json.dumps(raw).encode())):
                self.assertEqual(McpGateway(Path(temp)).call('capture_viewport')['path'], 'capture.png')
            log = json.loads((Path(temp)/'calls.jsonl').read_text())
            self.assertEqual(log['request']['name'], 'capture_viewport')
            self.assertNotIn('large payload', json.dumps(log))


class CaptureTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.run = CaptureRun(Path(self.temp.name))

    def test_nonempty_scene_requires_explicit_reset(self):
        self.run.call = Mock(return_value={'objects': [{'id': 'original'}]})
        with self.assertRaisesRegex(RuntimeError, 'empty document'):
            self.run.prepare(False)
        self.assertEqual(self.run.call.call_count, 1)

    def test_explicit_reset_deletes_returned_ids_before_changing_units(self):
        self.run.call = Mock(side_effect=lambda n, a=None: {'objects': [{'id':'a'}, {'id':'b'}]})
        self.run.scene = Mock()
        self.run.prepare(True)
        self.assertEqual(self.run.call.call_args_list[2].args, ('delete_objects', {'ids':'a,b'}))
        self.assertEqual(self.run.call.call_args_list[3].args[0], 'set_document_units')

    def test_missing_display_tool_fails_before_deletion(self):
        def call(name, args=None):
            if name == 'set_illustration_style':
                raise RuntimeError('Unknown tool')
            return {'objects': [{'id':'existing'}]}
        self.run.call = Mock(side_effect=call)
        with self.assertRaisesRegex(RuntimeError, 'Unknown tool'):
            self.run.prepare(True)
        self.assertNotIn('delete_objects', [c.args[0] for c in self.run.call.call_args_list])

    def test_empty_scene_does_not_delete(self):
        self.run.call = Mock(return_value={'objects': []})
        self.run.scene = Mock()
        self.run.prepare(False)
        self.assertNotIn('delete_objects', [c.args[0] for c in self.run.call.call_args_list])

    def test_capture_requires_file_not_just_success_response(self):
        self.run.call = Mock(return_value={'path':'missing.png'})
        with self.assertRaisesRegex(RuntimeError, 'Capture missing'):
            self.run.capture('Site', 10)
        self.assertEqual(self.run.beats, [])

    def test_storyboard_is_twenty_one_seconds_and_loop_end_matches_start(self):
        self.run.scene = Mock()
        self.run.call = Mock()
        def capture(caption, ticks):
            self.run.beats.append({'file':f'{len(self.run.beats)}.png', 'caption':caption, 'ticks':ticks})
        self.run.capture = capture
        self.run.build()
        beats = json.loads((Path(self.temp.name)/'timeline.json').read_text())
        self.assertEqual(sum(b['ticks'] for b in beats), 210)
        self.assertEqual(beats[0]['file'], beats[-1]['file'])
        self.assertEqual(beats[0]['caption'], beats[-1]['caption'])
        self.run.scene.curtain.assert_any_call(3)


class GeometryTests(unittest.TestCase):
    def test_glass_and_frames_are_reassigned_separately(self):
        call = Mock(return_value={'frame_ids':['frame'], 'glass_ids':['glass']})
        OfficeScene(call).curtain(2)
        call.assert_any_call('set_object_layer', {'ids':'frame', 'layer':LAYER+'::Frame'})
        call.assert_any_call('set_object_layer', {'ids':'glass', 'layer':LAYER+'::Glass'})
        call.assert_any_call('set_object_layer', {'ids':'glass', 'layer':LAYER+'::SideGlass'})

    def test_missing_facade_parts_fail_build(self):
        with self.assertRaisesRegex(RuntimeError, 'no frame_ids'):
            OfficeScene(Mock(return_value={'frame_ids':[], 'glass_ids':['g']})).curtain(0)

    def test_canopies_are_deterministic_and_non_degenerate(self):
        points = tree_canopy(2)
        self.assertEqual(points, tree_canopy(2))
        self.assertNotEqual(points, tree_canopy(3))
        self.assertGreater(max(p[0] for p in points)-min(p[0] for p in points), 3)
        self.assertGreater(min(p[1] for p in points), 1)

    def test_flight_starts_at_street_camera_and_rises_smoothly(self):
        from office_demo_scene import CAMERA, TARGET
        first = flight_camera(0, 70, CAMERA, TARGET)
        last = flight_camera(69, 70, CAMERA, TARGET)
        for actual, expected in zip(first, CAMERA):
            self.assertAlmostEqual(actual, expected)
        self.assertEqual(last[2], CAMERA[2]+23)
        self.assertLess(last[0], TARGET[0])
        heights = [flight_camera(i,70,CAMERA,TARGET)[2] for i in range(70)]
        self.assertEqual(heights, sorted(heights))

    def test_people_have_distinct_poses_and_human_scale(self):
        profiles = [person_profile(i) for i in range(4)]
        self.assertEqual(len({tuple(p) for p in profiles}), 4)
        for profile in profiles:
            self.assertTrue(1.65 < max(y for x,y in profile) < 2.1)

    def test_billboards_rotate_without_copying_or_accumulating_same_view_rotation(self):
        call = Mock(return_value={'id':'curve','ids':['surface']})
        scene = OfficeScene(call)
        scene.person(0)
        scene.face_camera([-40,-60,25])
        rotations = [c for c in call.call_args_list if c.args[0]=='transform_objects']
        self.assertEqual(len(rotations),1)
        self.assertFalse(rotations[0].args[1]['copy'])
        scene.face_camera([-40,-60,25])
        self.assertEqual(sum(c.args[0]=='transform_objects' for c in call.call_args_list),1)

    def test_scene_components_use_valid_boxes_and_filled_silhouettes(self):
        calls = []
        def call(name, args):
            calls.append((name,args))
            return {'id':'curve', 'ids':['surface']}
        scene = OfficeScene(call)
        scene.layers()
        scene.site()
        scene.parking()
        scene.context()
        for i in range(4):
            scene.massing(i)
            scene.person(i)
        scene.entrance()
        for i in range(6):
            scene.tree(i)
        for name,args in calls:
            if name == 'create_box':
                self.assertTrue(all(args['min'+a] < args['max'+a] for a in 'XYZ'))
            if name == 'create_planar_surface':
                self.assertFalse(args['keep_inputs'])
        self.assertEqual(sum(n=='create_planar_surface' for n,a in calls), 16)


class ExportTests(unittest.TestCase):
    def test_bad_timing_rejected(self):
        for beats in ([], [{'ticks':0}], [{'ticks':-1}], [{'ticks':1.5}]):
            with self.assertRaises(ValueError):
                list(timeline_frames(beats))

    def test_encode_counts_holds_and_preserves_caption(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root/'shot.png').touch()
            (root/'timeline.json').write_text(json.dumps([
                {'file':'shot.png', 'caption':'Site: 100%', 'ticks':3}]))
            commands = []
            def render(args):
                commands.append(args)
                Path(args[-1]).write_bytes(b'output')
            with patch('office_demo_video.ffmpeg', side_effect=render), patch('office_demo_video.render_frame') as frame:
                frame.side_effect = lambda source, output, caption: output.write_bytes(b'frame')
                encode(root)
                frame.assert_called_once_with(root/'shot.png', root/'encoded_frames/frame_0000.png', 'Site: 100%')
            self.assertEqual(len(list((root/'encoded_frames').glob('frame_*.png'))), 3)
            self.assertEqual(commands[-1][commands[-1].index('-frames:v')+1], '3')
            self.assertTrue((root/'demo.mp4').exists())

    def test_frame_has_even_dimensions_and_separate_caption_strip(self):
        from PIL import Image
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            Image.new('RGB', (1310, 838), '#b0d0e0').save(root/'source.png')
            render_frame(root/'source.png', root/'frame.png', 'Site: 100%')
            with Image.open(root/'frame.png') as frame:
                self.assertEqual(frame.size, (800, 592))
                self.assertEqual(frame.getpixel((0, 0)), (176, 208, 224))
                self.assertEqual(frame.getpixel((0, 591)), (248, 250, 248))

    def test_missing_capture_rejected_before_encoding(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)
            (root/'timeline.json').write_text(json.dumps([{'file':'gone.png','caption':'','ticks':1}]))
            with self.assertRaises(FileNotFoundError):
                encode(root)


if __name__ == '__main__':
    unittest.main()
