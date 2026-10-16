#!/usr/bin/env python3
"""Exercise Grasshopper through MCP using discovered component IDs and canonical skill recipes."""
import argparse
import copy
import json
from pathlib import Path
import tempfile
import time
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]


class Mcp:
    def __init__(self, endpoint):
        self.endpoint = endpoint

    def call(self, tool, arguments=None, expect_error=False):
        payload = {'jsonrpc': '2.0', 'id': str(uuid.uuid4()), 'method': 'tools/call',
                   'params': {'name': tool, 'arguments': arguments or {}}}
        request = urllib.request.Request(self.endpoint, json.dumps(payload).encode(),
                                         {'Content-Type': 'application/json'})
        with urllib.request.urlopen(request, timeout=180) as response:
            envelope = json.load(response)
        if 'error' in envelope:
            raise RuntimeError(envelope['error'])
        result = envelope['result']
        value = json.loads(result['content'][0]['text'])
        if expect_error:
            assert result.get('isError'), value
        elif result.get('isError'):
            raise RuntimeError(value)
        return value


def mutation(session, **arguments):
    return {'session_id': session['session_id'], 'expected_revision': session['revision'],
            'request_id': str(uuid.uuid4()), **arguments}


def discover(client, recipe):
    resolved = {}
    for key, full_type in recipe['components'].items():
        candidates = client.call('gh_search_components', {'query': full_type, 'limit': 200})['components']
        matches = [c for c in candidates if c['type'] == full_type and c['supported']]
        assert len(matches) == 1, (full_type, candidates)
        resolved[key] = matches[0]['component_id']
    descriptions = client.call('gh_describe_components', {'component_ids': list(resolved.values())})
    assert len(descriptions) == len(resolved)
    return resolved


def await_job(client, job, allow_component_errors=False):
    deadline = time.monotonic() + 180
    while time.monotonic() < deadline:
        status = client.call('gh_get_job', {'job_id': job['job_id']})
        if status['state'] in ('queued', 'running'):
            time.sleep(0.2)
            continue
        assert status['state'] == 'completed', status
        result = status['result']
        assert result['result_id'], result
        if not allow_component_errors:
            assert not any(d.get('errors') for d in result['diagnostics']), result['diagnostics']
        return result
    raise TimeoutError('Solve did not finish; execution may still be active. Inspect gh_get_job.')


def read_output(client, session, result, endpoint):
    rows = []
    offset = 0
    while True:
        page = client.call('gh_read_output', {'session_id': session['session_id'],
                         'result_id': result['result_id'], **endpoint, 'offset': offset})
        rows.extend(page['rows'])
        if page['next_offset'] is None:
            break
        offset = page['next_offset']
    assert all(row.get('empty') or row.get('value') is not None for row in rows), rows
    return rows


def verify_failed_patch(client, session):
    before = client.call('gh_inspect_document', {'session_id': session['session_id']})
    error = client.call('gh_apply_patch', mutation(session, operations=[
        {'op': 'remove', 'node': str(uuid.uuid4())}]), expect_error=True)
    assert error['code'] == 'node_not_found', error
    after = client.call('gh_inspect_document', {'session_id': session['session_id']})
    assert after['session']['revision'] == before['session']['revision']
    assert after['total'] == before['total']


def verify_variation(client, name, session, aliases, result, endpoint):
    if name == 'loft':
        operations = [{'op': 'set_mapping', 'node': aliases['loft'], 'port': 'in:0', 'mapping': 'flatten'}]
    else:
        control, value = ('count', 8) if name == 'array' else ('width', 25)
        operations = [{'op': 'set_slider', 'node': aliases[control], 'value': value}]
    changed = client.call('gh_apply_patch', mutation(session, operations=operations, solve=True))
    session = changed['session']
    client.call('gh_read_output', {'session_id': session['session_id'], 'result_id': result['result_id'],
                                 **endpoint}, expect_error=True)
    new_result = await_job(client, changed['job'], allow_component_errors=name == 'loft')
    rows = read_output(client, session, new_result, endpoint)
    if name == 'array':
        assert len(rows) == 8, rows
    elif name == 'facade':
        assert len(rows) == 5, rows
    else:
        repaired = client.call('gh_apply_patch', mutation(session, operations=[
            {'op': 'set_mapping', 'node': aliases['loft'], 'port': 'in:0', 'mapping': 'none'}], solve=True))
        session = repaired['session']
        new_result = await_job(client, repaired['job'])
        assert len(read_output(client, session, new_result, endpoint)) == 2
    return session, new_result


def bake_snapshots(client, session, result, endpoint):
    arguments = mutation(session, result_id=result['result_id'], outputs=[endpoint])
    first = client.call('gh_bake', arguments)
    retry = client.call('gh_bake', arguments)
    assert first == retry
    second = client.call('gh_bake', mutation(session, result_id=result['result_id'], outputs=[endpoint]))
    assert not set(first['object_ids']) & set(second['object_ids'])
    return first['object_ids'] + second['object_ids']


def round_trip(client, session, directory):
    paths = []
    for extension in ('gh', 'ghx'):
        path = str(directory / f'study.{extension}')
        before = client.call('gh_inspect_document', {'session_id': session['session_id'], 'limit': 200})
        saved = client.call('gh_save_document', mutation(session, path=path))
        session = saved['session']
        client.call('gh_close_document', mutation(session, discard=True))
        session = client.call('gh_open_document', {'mode': 'open', 'path': path, 'request_id': str(uuid.uuid4())})
        after = client.call('gh_inspect_document', {'session_id': session['session_id'], 'limit': 200})
        assert [n['component_id'] for n in before['nodes']] == [n['component_id'] for n in after['nodes']]
        assert [n['position'] for n in before['nodes']] == [n['position'] for n in after['nodes']]
        assert [n['inputs'] for n in before['nodes']] == [n['inputs'] for n in after['nodes']]
        assert [n['settings'] for n in before['nodes']] == [n['settings'] for n in after['nodes']]
        paths.append(path)
    return session, paths


def run(client, name, bake):
    help_result = client.call('gh_help', {'topic': 'examples/' + name})
    recipe = help_result['recipe']
    resolved = discover(client, recipe)
    session = client.call('gh_open_document', {'mode': 'create', 'name': f'MCP test {name}', 'request_id': str(uuid.uuid4())})
    operations = copy.deepcopy(recipe['operations'])
    for operation in operations:
        if operation['op'] == 'add':
            operation['component_id'] = resolved[operation['component_id'][1:]]
    patch_args = mutation(session, operations=operations, solve=True)
    patch = client.call('gh_apply_patch', patch_args)
    assert patch == client.call('gh_apply_patch', patch_args), 'Patch retry must return original receipt'
    session = patch['session']
    aliases = patch['patch']['aliases']
    result = await_job(client, patch['job'])
    endpoint = {**recipe['output'], 'node': aliases[recipe['output']['node']]}
    rows = read_output(client, session, result, endpoint)
    assert len(rows) == recipe['expected_count'], rows
    verify_failed_patch(client, session)
    session, result = verify_variation(client, name, session, aliases, result, endpoint)
    object_ids = bake_snapshots(client, session, result, endpoint) if bake else []
    directory = Path(tempfile.mkdtemp(prefix='mcp4rhino-gh-'))
    session, paths = round_trip(client, session, directory)
    result = await_job(client, client.call('gh_solve', mutation(session)))
    print(json.dumps({'example': name, 'session': session, 'result': result,
                      'baked_object_ids': object_ids, 'saved': paths}, indent=2), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--endpoint', default='http://127.0.0.1:4010/mcp')
    parser.add_argument('--example', choices=['array', 'facade', 'loft'], default='array')
    parser.add_argument('--bake', action='store_true', help='Also create two additive Rhino snapshots and verify retry deduplication')
    options = parser.parse_args()
    run(Mcp(options.endpoint), options.example, options.bake)
