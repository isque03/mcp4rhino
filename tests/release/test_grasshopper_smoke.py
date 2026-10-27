"""Regression tests for native smoke orchestration, without Rhino or network calls."""
import copy
import io
import importlib.util
import json
from pathlib import Path
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('grasshopper_smoke', ROOT / 'scripts/grasshopper-smoke.py')
smoke = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(smoke)


class PatchClient:
    def __init__(self):
        self.calls = []
        self.receipts = {}
        self.ids = set()

    def call(self, tool, arguments):
        assert tool == 'gh_apply_patch'
        key = arguments['request_id']
        if key in self.receipts:
            assert arguments == self.receipts[key][0]
            return self.receipts[key][1]
        aliases = {}
        for operation in arguments['operations']:
            if operation['op'] == 'add':
                alias = operation['alias']
                assert operation['component_id'] == 'discovered-type'
                aliases[alias] = f'instance-{alias}'
            for endpoint in (operation, operation.get('source', {}), operation.get('target', {})):
                if 'node' in endpoint:
                    assert endpoint['node'] in aliases or endpoint['node'] in self.ids
        self.ids.update(aliases.values())
        self.calls.append(copy.deepcopy(arguments))
        result = {'session': {'session_id': 's', 'revision': arguments['expected_revision'] + 1},
                  'patch': {'aliases': aliases}, 'job': {'job_id': 'j'} if arguments['solve'] else None}
        self.receipts[key] = (copy.deepcopy(arguments), result)
        return result


class SmokeBatchTests(unittest.TestCase):
    def test_later_batches_resolve_previous_aliases_and_solve_only_once(self):
        operations = [{'op': 'add', 'alias': f'n{i}', 'component_id': '$native'} for i in range(99)]
        operations += [{'op': 'connect', 'source': {'node': 'n0', 'port': 'out:0'},
                        'target': {'node': 'n1', 'port': 'in:0'}},
                       {'op': 'set_slider', 'node': 'n0', 'value': 7},
                       {'op': 'add', 'alias': 'later', 'component_id': '$native'},
                       {'op': 'connect', 'source': {'node': 'n0', 'port': 'out:0'},
                        'target': {'node': 'later', 'port': 'in:0'}}]
        original = copy.deepcopy(operations)
        client = PatchClient()
        session, aliases, job = smoke.apply_recipe(client, {'session_id': 's', 'revision': 0},
                                                   {'operations': operations}, {'native': 'discovered-type'})
        self.assertEqual([len(call['operations']) for call in client.calls], [100, 3])
        self.assertEqual([call['solve'] for call in client.calls], [False, True])
        self.assertEqual(client.calls[1]['operations'][0]['node'], 'instance-n0')
        self.assertEqual(client.calls[1]['operations'][2]['target']['node'], 'later')
        self.assertEqual(session['revision'], 2)
        self.assertEqual(len(aliases), 100)
        self.assertEqual(job, {'job_id': 'j'})
        self.assertEqual(operations, original)

    def test_short_recipe_keeps_intra_patch_aliases(self):
        client = PatchClient()
        smoke.apply_recipe(client, {'session_id': 's', 'revision': 0}, {'operations': [
            {'op': 'add', 'alias': 'control', 'component_id': '$native'},
            {'op': 'set_slider', 'node': 'control', 'value': 3}]}, {'native': 'discovered-type'})
        self.assertEqual(client.calls[0]['operations'][1]['node'], 'control')
        self.assertTrue(client.calls[0]['solve'])


class SmokeProtocolTests(unittest.TestCase):
    def test_modern_requests_carry_metadata_and_matching_headers(self):
        response = io.BytesIO(json.dumps({'result': {'contents': [{'text': '{"status":"available"}'}]}}).encode())
        uri = 'mcp4rhino://grasshopper/sessions'
        with patch.object(smoke.urllib.request, 'urlopen', return_value=response) as send:
            value = smoke.Mcp('http://127.0.0.1:4010/mcp', '2026-07-28').read_resource(uri)
        request = send.call_args.args[0]
        body = json.loads(request.data)
        headers = {key.lower(): value for key, value in request.header_items()}
        self.assertEqual(headers['mcp-name'], uri)
        self.assertEqual(headers['mcp-method'], body['method'])
        self.assertEqual(headers['mcp-protocol-version'], body['params']['_meta']['io.modelcontextprotocol/protocolVersion'])
        self.assertEqual(value['status'], 'available')

    def test_legacy_clients_still_parse_text_first_tool_results(self):
        response = io.BytesIO(json.dumps({'result': {'content': [{'text': '{"value":3}'}], 'isError': False}}).encode())
        with patch.object(smoke.urllib.request, 'urlopen', return_value=response) as send:
            result = smoke.Mcp('http://127.0.0.1:4010/mcp').call('test')
        self.assertEqual(result, {'value': 3})
        self.assertNotIn('_meta', json.loads(send.call_args.args[0].data)['params'])


if __name__ == '__main__':
    unittest.main()
