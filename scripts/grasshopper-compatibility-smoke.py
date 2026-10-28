#!/usr/bin/env python3
"""Native 0.7.2-alpha.1 checks on an explicitly supplied disposable, already-open definition."""
import argparse
import importlib.util
import json
from pathlib import Path
import time
import uuid

SPEC = importlib.util.spec_from_file_location('grasshopper_smoke', Path(__file__).with_name('grasshopper-smoke.py'))
smoke = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(smoke)


def inspect_all(client, session):
    nodes, offset, revision = [], 0, None
    while True:
        page = client.call('gh_inspect_document', {'session_id': session['session_id'], 'offset': offset, 'limit': 200})
        observed = page['session']['revision']
        if revision is None:
            revision = observed
        assert revision == observed, 'Graph changed during inspection'
        nodes.extend(page['nodes'])
        offset += len(page['nodes'])
        if offset >= page['total']:
            return page['session'], nodes
        assert page['nodes'], 'Inspection stopped before all nodes were returned'


def graph_identity(nodes):
    """Reported identities only; native object-reference/private-state checks remain manual."""
    return sorted((node['node'], node['component_id'], node.get('assembly_identity'), node['position'],
        [(side, [(port['parameter_id'], port['port'], port['type'], port['access'], port.get('data_type'), port['sources'])
                 for port in node[side]]) for side in ('inputs', 'outputs')]) for node in nodes)


def await_denied(client, job):
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        status = client.call('gh_get_job', {'job_id': job['job_id']})
        if status['state'] not in ('queued', 'running'):
            assert status['state'] == 'failed', status
            assert any(code in json.dumps(status) for code in ('component_authorization_required', 'restricted_component')), status
            return
        time.sleep(0.2)
    raise AssertionError('Authorization job did not finish')


def exercise(client, session, slider_id, value, expect_solve_denied=False):
    session, before = inspect_all(client, session)
    sliders = [node for node in before if node['node'] == slider_id]
    assert len(sliders) == 1 and sliders[0]['supported'], 'Supply a verified slider instance GUID'
    settings = sliders[0]['settings']
    original = settings['raw_value']
    assert value != original, 'Choose a new raw value so the test changes the slider'
    baseline = graph_identity(before)
    try:
        arguments = smoke.mutation(session, solve=False, operations=[
            {'op': 'set_slider', 'node': slider_id, 'value': original},
            {'op': 'set_slider', 'node': slider_id, 'value': value}])
        receipt = client.call('gh_apply_patch', arguments)
        assert receipt == client.call('gh_apply_patch', arguments), 'Retry changed receipt'
        session, after = inspect_all(client, receipt['session'])
        assert session['revision'] == receipt['session']['revision'], 'Retry mutated the graph'
        assert baseline == graph_identity(after), 'Node/port/wire identities or positions changed'
        assert next(node for node in after if node['node'] == slider_id)['settings']['raw_value'] == value
        rejected = smoke.mutation(session, solve=False, operations=[
            {'op': 'set_slider', 'node': slider_id, 'value': original},
            {'op': 'set_slider', 'node': slider_id, 'value': settings['maximum'] + 1}])
        client.call('gh_apply_patch', rejected, expect_error=True)
        unchanged_session, unchanged = inspect_all(client, session)
        assert unchanged_session['revision'] == session['revision'], 'Invalid batch changed revision'
        assert baseline == graph_identity(unchanged), 'Invalid batch changed topology'
        assert next(node for node in unchanged if node['node'] == slider_id)['settings']['raw_value'] == value
        if expect_solve_denied:
            denied = smoke.mutation(session, solve=True, operations=[{'op': 'set_slider', 'node': slider_id, 'value': original}])
            error = client.call('gh_apply_patch', denied, expect_error=True)
            assert any(code in json.dumps(error) for code in ('component_authorization_required', 'restricted_component')), error
            same_session, same = inspect_all(client, session)
            assert same_session['revision'] == session['revision'], 'Denied solve committed a patch'
            assert next(node for node in same if node['node'] == slider_id)['settings']['raw_value'] == value
            await_denied(client, client.call('gh_solve', smoke.mutation(session)))
        return {'slider_patch_verified': True, 'reported_graph_identity_preserved': True,
                'solve_denial_verified': expect_solve_denied, 'native_undo_redo_verified': False}
    finally:
        session, _ = inspect_all(client, session)
        client.call('gh_apply_patch', smoke.mutation(session, solve=False,
            operations=[{'op': 'set_slider', 'node': slider_id, 'value': original}]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--endpoint', default='http://127.0.0.1:4010/mcp')
    parser.add_argument('--document-id', required=True, help='Disposable already-open GH document runtime ID')
    parser.add_argument('--slider-id', required=True, help='Placed slider instance GUID')
    parser.add_argument('--value', required=True, type=float, help='New raw value within the slider range/accuracy')
    parser.add_argument('--expect-solve-denied', action='store_true')
    args = parser.parse_args()
    client = smoke.Mcp(args.endpoint)
    support = client.call('get_support_info')
    assert support['server_version'] == '0.7.2-alpha.1', support
    session = client.call('gh_open_document', {'mode': 'attach', 'document_id': args.document_id, 'request_id': str(uuid.uuid4())})
    result = exercise(client, session, args.slider_id, args.value, args.expect_solve_denied)
    print(json.dumps({'version': support['server_version'], 'session_id': session['session_id'], **result}, indent=2))


if __name__ == '__main__':
    main()
