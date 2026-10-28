#!/usr/bin/env python3
"""Exercise Grasshopper through MCP using discovered component IDs and canonical skill recipes."""
import argparse
import base64
import copy
import json
from pathlib import Path
import tempfile
import time
import urllib.request
import urllib.error
import uuid

ROOT = Path(__file__).resolve().parents[1]


class Mcp:
    def __init__(self, endpoint, protocol='2024-11-05', resources=False):
        self.endpoint, self.protocol, self.resources = endpoint, protocol, resources

    def rpc(self, method, parameters=None, expect_error=False):
        arguments = copy.deepcopy(parameters or {})
        headers = {'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream'}
        if self.protocol != '2024-11-05':
            headers['MCP-Protocol-Version'] = self.protocol
        if self.protocol == '2026-07-28':
            arguments['_meta'] = {'io.modelcontextprotocol/protocolVersion': self.protocol,
                'io.modelcontextprotocol/clientCapabilities': {},
                'io.modelcontextprotocol/clientInfo': {'name': 'MCP4Rhino smoke', 'version': '0.7.2-alpha'}}
            headers['Mcp-Method'] = method
            if method in ('tools/call', 'prompts/get', 'resources/read'):
                name = arguments.get('name', arguments.get('uri'))
                safe = name.isascii() and name == name.strip() and all(32 <= ord(c) <= 126 for c in name) and not name.startswith('=?base64?')
                headers['Mcp-Name'] = name if safe else '=?base64?' + base64.b64encode(name.encode()).decode() + '?='
        payload = {'jsonrpc': '2.0', 'id': str(uuid.uuid4()), 'method': method, 'params': arguments}
        request = urllib.request.Request(self.endpoint, json.dumps(payload).encode(), headers)
        try:
            with urllib.request.urlopen(request, timeout=180) as response:
                envelope = json.load(response)
        except urllib.error.HTTPError as error:
            envelope = json.load(error)
        if expect_error:
            assert 'error' in envelope, envelope
            return envelope['error']
        if 'error' in envelope:
            raise RuntimeError(envelope['error'])
        return envelope['result']

    def call(self, tool, arguments=None, expect_error=False):
        result = self.rpc('tools/call', {'name': tool, 'arguments': arguments or {}})
        value = json.loads(result['content'][0]['text'])
        if expect_error:
            assert result.get('isError'), value
        elif result.get('isError'):
            raise RuntimeError(value)
        return value

    def read_resource(self, uri):
        result = self.rpc('resources/read', {'uri': uri})
        return json.loads(result['contents'][0]['text'])


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
    for key, contract in recipe.get('port_contracts', {}).items():
        description = descriptions[list(resolved).index(key)]
        assert [p['name'] for p in description['inputs']] == contract['inputs'], description
    return resolved


def apply_recipe(client, session, recipe, resolved):
    operations = copy.deepcopy(recipe['operations'])
    aliases = {}
    for start in range(0, len(operations), 100):
        batch = operations[start:start + 100]
        for op in batch:
            if op['op'] == 'add':
                op['component_id'] = resolved[op['component_id'][1:]]
            for endpoint in (op, op.get('source', {}), op.get('target', {})):
                if endpoint.get('node') in aliases:
                    endpoint['node'] = aliases[endpoint['node']]
        arguments = mutation(session, operations=batch, solve=start + 100 >= len(operations))
        patch = client.call('gh_apply_patch', arguments)
        assert patch == client.call('gh_apply_patch', arguments), 'Patch retry must return original receipt'
        aliases.update(patch['patch']['aliases'])
        session = patch['session']
    return session, aliases, patch['job']


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
    first = before['nodes'][0]['node']
    error = client.call('gh_apply_patch', mutation(session, operations=[
        {'op': 'set_properties', 'node': first, 'name': 'must roll back'},
        {'op': 'remove', 'node': str(uuid.uuid4())}]), expect_error=True)
    assert error['code'] == 'node_not_found', error
    after = client.call('gh_inspect_document', {'session_id': session['session_id']})
    assert after['session']['revision'] == before['session']['revision']
    assert after['total'] == before['total']
    assert after['nodes'] == before['nodes']


def verify_variation(client, name, session, aliases, result, endpoint):
    if name == 'stair':
        return vary_stair(client, session, aliases, result, endpoint)
    if name == 'loft':
        operations = [{'op': 'set_mapping', 'node': aliases['loft'], 'port': 'in:0', 'mapping': 'flatten'}]
    else:
        control, value = {'array': ('count', 8), 'facade': ('width', 25), 'profile': ('depth', 200)}[name]
        operations = [{'op': 'set_slider', 'node': aliases[control], 'value': value}]
    changed = client.call('gh_apply_patch', mutation(session, operations=operations, solve=True))
    session = changed['session']
    client.call('gh_read_output', {'session_id': session['session_id'], 'result_id': result['result_id'],
                                 **endpoint}, expect_error=True)
    if getattr(client, 'resources', False):
        verify_expired_resource(client, session, result)
    new_result = await_job(client, changed['job'], allow_component_errors=name == 'loft')
    rows = read_output(client, session, new_result, endpoint)
    if name == 'array':
        assert len(rows) == 8, rows
    elif name == 'facade':
        assert len(rows) == 5, rows
    elif name == 'loft':
        repaired = client.call('gh_apply_patch', mutation(session, operations=[
            {'op': 'set_mapping', 'node': aliases['loft'], 'port': 'in:0', 'mapping': 'none'}], solve=True))
        session = repaired['session']
        new_result = await_job(client, repaired['job'])
        assert len(read_output(client, session, new_result, endpoint)) == 2
    elif name == 'profile':
        assert len(rows) == 2
        assert all(abs(row['value']['bounds']['max'][2] - 200) < 0.001 for row in rows)
    return session, new_result


def verify_geometry(client, session, aliases, result, recipe):
    for name, reference in recipe['checks'].items():
        endpoint = {**reference, 'node': aliases[reference['node']]}
        rows = read_output(client, session, result, endpoint)
        if name not in ('outlines', 'outline', 'solids', 'treads', 'stringers'):
            continue
        assert rows and all(not row.get('empty') for row in rows), (name, rows)
        for row in rows:
            value = row['value']
            assert value['valid'], (name, value)
            checks = value['measurements']
            if name in ('outline', 'outlines'):
                assert checks['closed'] and checks['planar'] and checks['self_intersecting'] is False, value
            else:
                assert checks['solid'] is True, value
        if name == 'solids':
            assert len({tuple(row['path']) for row in rows}) == 2


def vary_stair(client, session, aliases, result, endpoint):
    recipe = client.call('gh_help', {'topic': 'examples/stair'})['recipe']
    for control, value, rise, treads, run in [('height', 3040, 190, 15, 4191), ('risers', 17, 3040 / 17, 16, 4470.4)]:
        changed = client.call('gh_apply_patch', mutation(session, operations=[
            {'op': 'set_slider', 'node': aliases[control], 'value': value}], solve=True))
        session = changed['session']
        client.call('gh_read_output', {'session_id': session['session_id'], 'result_id': result['result_id'],
                                    **endpoint}, expect_error=True)
        if getattr(client, 'resources', False):
            verify_expired_resource(client, session, result)
        result = await_job(client, changed['job'])
        outputs = [{'name': name, 'node': aliases[name], 'port': 'out:0'} for name in ('rise', 'tread_count', 'run', 'treads', 'stringers')]
        summary = client.call('gh_get_solution_summary', {'session_id': session['session_id'],
            'expected_revision': session['revision'], 'result_id': result['result_id'], 'outputs': outputs})
        by_name = {item['name']: item for item in summary['outputs']}
        for name, expected in [('rise', rise), ('tread_count', treads), ('run', run)]:
            actual = by_name[name]['data']['rows'][0]['value']['value']
            assert abs(actual - expected) < 1e-6, (name, actual, expected)
        assert by_name['treads']['item_count'] == treads, summary
        assert by_name['stringers']['item_count'] == 3, summary
        verify_geometry(client, session, aliases, result, recipe)
    return session, result


def capture_views(client, session, result):
    context = {'session_id': session['session_id'], 'expected_revision': session['revision'], 'result_id': result['result_id']}
    canvas = client.call('gh_capture_canvas', {**context, 'framing': 'fit'})
    assert canvas['identity']['result_id'] == result['result_id'], canvas
    client.call('gh_show_document', {**mutation(session), 'fit': True})
    viewport = client.call('capture_viewport', {'grasshopper': context})
    assert viewport['grasshopper']['result_id'] == canvas['identity']['result_id'], viewport
    return [canvas, viewport]


def list_fixture_recipe():
    operations = []
    for name, items in [('left', [1, 2, 3]), ('right', [10, 20]), ('pattern', [0, 1])]:
        operations.extend([{'op': 'add', 'alias': name, 'component_id': '$integer'},
            {'op': 'set_value', 'node': name, 'port': 'in:0', 'tree': [{'path': [0], 'items': items}]}])
    operations.append({'op': 'add', 'alias': 'weave', 'component_id': '$weave'})
    for name, port in [('pattern', 0), ('left', 1), ('right', 2)]:
        operations.append({'op': 'connect', 'source': {'node': name, 'port': 'out:0'},
                           'target': {'node': 'weave', 'port': f'in:{port}'}})
    return {'components': {'integer': 'Grasshopper.Kernel.Parameters.Param_Integer',
                          'weave': 'MathComponents.ArrayComponents.Component_Weave'},
            'operations': operations, 'port_contracts': {'weave': {'inputs': ['Pattern', 'Stream 0', 'Stream 1']}}}


def verify_list_matching(client):
    recipe = list_fixture_recipe()
    resolved = discover(client, recipe)
    session = client.call('gh_open_document', {'mode': 'create', 'name': 'MCP list fixture', 'request_id': str(uuid.uuid4())})
    session, aliases, job = apply_recipe(client, session, recipe, resolved)
    result = await_job(client, job)
    endpoint = {'node': aliases['weave'], 'port': 'out:0'}
    rows = read_output(client, session, result, endpoint)
    assert [row['value']['value'] for row in rows] == [1, 10, 2, 20, 3], rows
    # Empty persistent branches and nulls must survive inspection; native components own downstream matching.
    changed = client.call('gh_apply_patch', mutation(session, operations=[{'op': 'set_value',
        'node': aliases['right'], 'port': 'in:0', 'tree': [{'path': [0], 'items': []}, {'path': [3, 0], 'items': [None]}]}]))
    session = changed['session']
    graph = client.call('gh_inspect_document', {'session_id': session['session_id'], 'limit': 200})
    node = next(n for n in graph['nodes'] if n['node'] == aliases['right'])
    sample = node['inputs'][0]['persistent']['sample']
    assert sample[0]['empty'] and sample[0]['item_index'] is None, sample
    assert sample[1]['value'] is None and sample[1]['item_index'] == 0, sample
    client.call('gh_close_document', mutation(session, discard=True))


def verify_bad_profile(client, session, aliases, recipe):
    points = [{'type': 'point', 'value': p} for p in [[0, 0, 0], [100, 100, 0], [0, 100, 0], [100, 0, 0]]]
    changed = client.call('gh_apply_patch', mutation(session, operations=[{'op': 'set_value',
        'node': aliases['corners'], 'port': 'in:0', 'tree': [{'path': [0], 'items': points}]}], solve=True))
    session = changed['session']
    result = await_job(client, changed['job'], allow_component_errors=True)
    rows = read_output(client, session, result, {'node': aliases['outline'], 'port': 'out:0'})
    assert rows[0]['value']['measurements']['self_intersecting'] is True, rows
    restore = next(op for op in recipe['operations'] if op['op'] == 'set_value' and op['node'] == 'corners')
    changed = client.call('gh_apply_patch', mutation(session, operations=[{**restore, 'node': aliases['corners']}], solve=True))
    return changed['session'], await_job(client, changed['job'])


def vary_reopened(client, name, session, aliases):
    control, value = ('going', 300) if name == 'stair' else ('depth', 250)
    changed = client.call('gh_apply_patch', mutation(session, operations=[
        {'op': 'set_slider', 'node': aliases[control], 'value': value}], solve=True))
    session = changed['session']
    result = await_job(client, changed['job'])
    target = 'run' if name == 'stair' else 'direction'
    rows = read_output(client, session, result, {'node': aliases[target], 'port': 'out:0'})
    if name == 'stair':
        assert abs(rows[0]['value']['value'] - 4800) < 1e-6, rows
    else:
        assert abs(rows[0]['value']['value'][2] - 250) < 1e-6, rows
    return session, result


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


def run(client, name, bake, skip_save=False, capture=False):
    help_result = client.call('gh_help', {'topic': 'examples/' + name})
    recipe = help_result['recipe']
    resolved = discover(client, recipe)
    session = client.call('gh_open_document', {'mode': 'create', 'name': f'MCP test {name}', 'request_id': str(uuid.uuid4())})
    session, aliases, job = apply_recipe(client, session, recipe, resolved)
    result = await_job(client, job)
    endpoint = {**recipe['output'], 'node': aliases[recipe['output']['node']]}
    rows = read_output(client, session, result, endpoint)
    assert len(rows) == recipe['expected_count'], rows
    if name in ('profile', 'stair'):
        verify_geometry(client, session, aliases, result, recipe)
    if name == 'profile':
        verify_list_matching(client)
        session, result = verify_bad_profile(client, session, aliases, recipe)
    verify_failed_patch(client, session)
    session, result = verify_variation(client, name, session, aliases, result, endpoint)
    directory = Path(tempfile.mkdtemp(prefix='mcp4rhino-gh-'))
    paths = []
    if not skip_save:
        session, paths = round_trip(client, session, directory)
    result = await_job(client, client.call('gh_solve', mutation(session)))
    expected_count = 8 if name == 'array' else recipe['expected_count']
    assert len(read_output(client, session, result, endpoint)) == expected_count
    if not skip_save and name in ('profile', 'stair'):
        session, result = vary_reopened(client, name, session, aliases)
        verify_geometry(client, session, aliases, result, recipe)
    if getattr(client, 'resources', False):
        verify_resources(client, session, result, endpoint)
    captures = capture_views(client, session, result) if capture else []
    object_ids = bake_snapshots(client, session, result, endpoint) if bake else []
    print(json.dumps({'example': name, 'session': session, 'result': result,
                      'baked_object_ids': object_ids, 'saved': paths, 'captures': captures,
                      'save_reopen_verified': not skip_save}, indent=2), flush=True)


def protocol_probe(client):
    method = 'server/discover' if client.protocol == '2026-07-28' else 'initialize'
    arguments = {} if client.protocol == '2026-07-28' else {'protocolVersion': client.protocol,
        'capabilities': {}, 'clientInfo': {'name': 'MCP4Rhino smoke', 'version': '0.7.2-alpha'}}
    client.rpc(method, arguments)
    before = client.call('gh_status')
    for method in ('resources/list', 'resources/templates/list', 'prompts/list'):
        assert client.rpc(method), method
    document = client.read_resource('mcp4rhino://rhino/documents/active')
    sessions = client.read_resource('mcp4rhino://grasshopper/sessions')
    client.rpc('prompts/get', {'name': 'inspect_model'})
    after = client.call('gh_status')
    assert before['initialized'] == after['initialized'], (before, after)
    if not before['initialized']:
        assert sessions['status'] == 'uninitialized', sessions
    return {'protocol': client.protocol, 'document': document, 'sessions': sessions, 'initialization_unchanged': True}


def verify_expired_resource(client, session, old_result):
    uri = f"mcp4rhino://grasshopper/sessions/{session['session_id']}/results/{old_result['result_id']}"
    error = client.rpc('resources/read', {'uri': uri}, expect_error=True)
    assert error['code'] == -32002 and 'stale_result' in error['message'], error


def verify_resources(client, session, result, endpoint):
    base = f"mcp4rhino://grasshopper/sessions/{session['session_id']}"
    before = client.call('gh_inspect_document', {'session_id': session['session_id']})
    graph = client.read_resource(base + f"/graph?expected_revision={session['revision']}")
    assert graph['revision'] == session['revision'], graph
    assert graph['data']['total_nodes'] == before['total'], graph
    node = client.read_resource(base + '/nodes/' + endpoint['node'])
    assert node['data']['node'] == endpoint['node'], node
    component = client.read_resource('mcp4rhino://grasshopper/components/' + node['data']['component_id'])
    assert component['data']['ports_status'] == 'cached', component
    identity = client.read_resource(base + '/results/' + result['result_id'])
    assert identity['identity']['result_id'] == result['result_id'], identity
    output_uri = base + f"/results/{result['result_id']}/nodes/{endpoint['node']}/outputs/" + endpoint['port'].split(':')[1]
    output = client.read_resource(output_uri)
    tool = client.call('gh_read_output', {'session_id': session['session_id'], 'result_id': result['result_id'], **endpoint})
    assert output['data']['item_count'] == tool['item_count'], output
    assert output['data']['branch_count'] == tool['branch_count'], output
    document = client.read_resource('mcp4rhino://rhino/documents/active')
    info = client.call('get_document_info')
    assert document['units']['model'] == info['units'], document
    for collection, identifier in [('layers', 'layer_id'), ('views', 'view_id')]:
        rows = document['data'][collection]['items']
        if rows:
            client.read_resource(f"mcp4rhino://rhino/documents/{document['document_id']}/{collection}/{rows[0][identifier]}")
    objects = document['data']['objects']['items']
    if objects:
        client.read_resource(f"mcp4rhino://rhino/documents/{document['document_id']}/objects/{objects[0]['object_id']}")
    after = client.call('gh_inspect_document', {'session_id': session['session_id']})
    assert before['session']['revision'] == after['session']['revision'], (before, after)
    assert before['session']['result_id'] == after['session']['result_id'], (before, after)
    verify_component_resource(client)


def verify_component_resource(client):
    full_type = 'Grasshopper.Kernel.Parameters.Param_String'
    matches = client.call('gh_search_components', {'query': full_type, 'limit': 200})['components']
    component = next(item for item in matches if item['type'] == full_type and item['supported'])
    uri = 'mcp4rhino://grasshopper/components/' + component['component_id']
    initial = client.read_resource(uri)
    assert initial['data']['ports_status'] in ('cached', 'not_described'), initial
    if initial['data']['ports_status'] == 'not_described':
        assert initial['data']['contract'] is None and initial['data']['next_tool'] == 'gh_describe_components', initial
    client.call('gh_describe_components', {'component_ids': [component['component_id']]})
    assert client.read_resource(uri)['data']['ports_status'] == 'cached'
    missing = client.rpc('resources/read', {'uri': 'mcp4rhino://grasshopper/components/' + str(uuid.uuid4())}, expect_error=True)
    assert missing['code'] == -32002, missing


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--endpoint', default='http://127.0.0.1:4010/mcp')
    parser.add_argument('--example', choices=['array', 'facade', 'loft', 'profile', 'stair'], default='array')
    parser.add_argument('--bake', action='store_true', help='Also create two additive Rhino snapshots and verify retry deduplication')
    parser.add_argument('--skip-save', action='store_true', help='Skip persistence checks and explicitly report them unverified')
    parser.add_argument('--capture', action='store_true', help='Capture native canvas, then explicitly show the definition and capture its Rhino preview')
    parser.add_argument('--protocol', choices=['2024-11-05', '2025-11-25', '2026-07-28'], default='2024-11-05')
    parser.add_argument('--resources', action='store_true', help='Check resource/tool parity, read side effects, and stale result URIs')
    parser.add_argument('--context-only', action='store_true', help='Read context/catalogs without creating a graph or initializing Grasshopper')
    options = parser.parse_args()
    client = Mcp(options.endpoint, options.protocol, options.resources)
    if options.resources or options.context_only:
        print(json.dumps(protocol_probe(client), indent=2), flush=True)
    if not options.context_only:
        run(client, options.example, options.bake, options.skip_save, options.capture)
