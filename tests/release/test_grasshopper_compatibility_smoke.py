"""Smoke orchestration tests only; no Grasshopper runtime is loaded."""
import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('compatibility_smoke', ROOT / 'scripts/grasshopper-compatibility-smoke.py')
smoke = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(smoke)


def nodes():
    return [
        {'node': 'slider', 'component_id': 'native-slider', 'position': [0, 0], 'supported': True,
         'inputs': [], 'outputs': [{'parameter_id': 'slider-out', 'port': 'out:0', 'type': 'Number', 'access': 'item', 'sources': []}],
         'settings': {'raw_value': 4, 'value': 8, 'minimum': 0, 'maximum': 10}},
        {'node': 'plugin', 'component_id': 'thirdparty', 'assembly_identity': 'Plugin, Version=1.0.0.0',
         'position': [200, 0], 'supported': False, 'inputs': [
             {'parameter_id': 'plugin-in', 'port': 'in:0', 'type': 'Number', 'access': 'list',
              'sources': [{'node': 'slider', 'parameter_id': 'slider-out', 'port': 'out:0'}]}], 'outputs': []}]


class FakeClient:
    def __init__(self):
        self.nodes = nodes()
        self.session = {'session_id': 's', 'revision': 0}
        self.receipts = {}
        self.solve_attempts = 0

    def call(self, tool, arguments, expect_error=False):
        if tool == 'gh_inspect_document':
            return copy.deepcopy({'session': self.session, 'total': len(self.nodes), 'nodes': self.nodes})
        if tool == 'gh_solve':
            self.solve_attempts += 1
            return {'job_id': 'denied-job'}
        if tool == 'gh_get_job':
            return {'state': 'failed', 'error': 'component_authorization_required: Plugin'}
        assert tool == 'gh_apply_patch'
        key = arguments['request_id']
        if key in self.receipts:
            return copy.deepcopy(self.receipts[key])
        assert arguments['expected_revision'] == self.session['revision']
        if arguments['solve']:
            assert expect_error
            return {'error': {'code': 'component_authorization_required'}}
        if any(op['value'] > 10 for op in arguments['operations']):
            assert expect_error
            return {'error': {'code': 'invalid_value'}}
        for operation in arguments['operations']:
            self.nodes[0]['settings']['raw_value'] = operation['value']
        self.session['revision'] += 1
        receipt = copy.deepcopy({'session': self.session, 'patch': {'aliases': {}}, 'job': None})
        self.receipts[key] = receipt
        return copy.deepcopy(receipt)


class CompatibilitySmokeTests(unittest.TestCase):
    def test_mixed_definition_orchestration_restores_raw_expression_value(self):
        client = FakeClient()
        original = smoke.graph_identity(client.nodes)
        result = smoke.exercise(client, client.session, 'slider', 6, expect_solve_denied=True)
        self.assertTrue(result['slider_patch_verified'])
        self.assertTrue(result['solve_denial_verified'])
        self.assertFalse(result['native_undo_redo_verified'])
        self.assertEqual(client.solve_attempts, 1)
        self.assertEqual(client.nodes[0]['settings']['raw_value'], 4)
        self.assertEqual(client.nodes[0]['settings']['value'], 8)
        self.assertEqual(smoke.graph_identity(client.nodes), original)
        self.assertEqual(client.session['revision'], 2)  # Change plus explicit restoration.

    def test_reported_identity_detects_source_order_and_parameter_changes(self):
        original = nodes()
        reordered = copy.deepcopy(original)
        reordered[1]['inputs'][0]['sources'].append({'node': 'other', 'parameter_id': 'other-out', 'port': 'out:0'})
        changed_order = copy.deepcopy(reordered)
        changed_order[1]['inputs'][0]['sources'].reverse()
        self.assertNotEqual(smoke.graph_identity(reordered), smoke.graph_identity(changed_order))
        changed_port = copy.deepcopy(original)
        changed_port[1]['inputs'][0]['parameter_id'] = 'reconstructed'
        self.assertNotEqual(smoke.graph_identity(original), smoke.graph_identity(changed_port))
        self.assertEqual(smoke.graph_identity(original), smoke.graph_identity(list(reversed(original))))

    def test_paged_inspection_rejects_concurrent_revision_change(self):
        class PagedClient:
            def call(self, tool, arguments):
                offset = arguments['offset']
                return {'session': {'session_id': 's', 'revision': offset}, 'total': 2, 'nodes': [nodes()[offset]]}
        with self.assertRaisesRegex(AssertionError, 'Graph changed'):
            smoke.inspect_all(PagedClient(), {'session_id': 's'})

    def test_test_value_must_exercise_a_change(self):
        client = FakeClient()
        with self.assertRaisesRegex(AssertionError, 'new raw value'):
            smoke.exercise(client, client.session, 'slider', 4)
        self.assertFalse(client.receipts)


if __name__ == '__main__':
    unittest.main()
