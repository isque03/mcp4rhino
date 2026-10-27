#!/usr/bin/env python3
"""Validate release versions, CI provenance, package contents, and Yak listings."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ALPHA_BRANCH = 'release/0.6.0-alpha'
TELEMETRY_ALPHA_BRANCH = 'release/0.6.1-alpha'
TOOLS_ALPHA_BRANCH = 'release/0.6.2-alpha'
GRAPH_ALPHA_BRANCH = 'release/0.6.4-alpha'
GUIDANCE_ALPHA_BRANCH = 'release/0.6.5-alpha'
PROJECTS = ('MCP4Rhino', 'MCP4Rhino.Contracts', 'MCP4Rhino.Logic',
            'MCP4Rhino.Tools', 'MCP4Rhino.Grasshopper')
REQUIRED_FILES = ('icon.png', 'manifest.yml', 'net8.0/MCP4Rhino.rhp',
                  'net8.0/MCP4Rhino.Contracts.dll', 'net8.0/MCP4Rhino.Logic.dll',
                  'net8.0/MCP4Rhino.Tools.dll', 'net8.0/MCP4Rhino.Grasshopper.dll',
                  'net8.0/MCP4Rhino.rui', 'skills/grasshopper-parametric-design/SKILL.md')
SDK_FILES = {'rhinocommon.dll', 'grasshopper.dll', 'gh_io.dll'}


def manifest_field(text, field):
    matches = re.findall(r'^' + re.escape(field) + r':\s*(\S+)\s*$', text, re.MULTILINE)
    if len(matches) != 1:
        raise ValueError(f'Manifest must contain exactly one {field}')
    return matches[0].strip('"\'')


def release_metadata(version, tag, workflow_ref):
    stable = re.fullmatch(r'(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)', version)
    grasshopper_alpha = re.fullmatch(r'0\.6\.0-alpha(?:\.[1-9]\d*)?', version)
    telemetry_alpha = re.fullmatch(r'0\.6\.1-alpha(?:\.[1-9]\d*)?', version)
    tools_alpha = re.fullmatch(r'0\.6\.2-alpha(?:\.[1-9]\d*)?', version)
    graph_alpha = re.fullmatch(r'0\.6\.4-alpha(?:\.[1-9]\d*)?', version)
    guidance_alpha = re.fullmatch(r'0\.6\.5-alpha(?:\.[1-9]\d*)?', version)
    alpha = grasshopper_alpha or telemetry_alpha or tools_alpha or graph_alpha or guidance_alpha
    if not stable and not alpha:
        raise ValueError('Only stable SemVer, 0.6.0-alpha[.N], 0.6.1-alpha[.N], 0.6.2-alpha[.N], 0.6.4-alpha[.N], or 0.6.5-alpha[.N] releases are supported')
    if tag != 'v' + version:
        raise ValueError('Tag must equal v + manifest version')
    if grasshopper_alpha:
        branch = ALPHA_BRANCH
    elif telemetry_alpha:
        branch = TELEMETRY_ALPHA_BRANCH
    elif tools_alpha:
        branch = TOOLS_ALPHA_BRANCH
    elif graph_alpha:
        branch = GRAPH_ALPHA_BRANCH
    elif guidance_alpha:
        branch = GUIDANCE_ALPHA_BRANCH
    else:
        branch = 'main'
    if workflow_ref != 'refs/heads/' + branch:
        raise ValueError(f'Publish must run from {branch}')
    return {'VERSION': version, 'BUILD_BRANCH': branch,
            'PRERELEASE': 'true' if alpha else 'false',
            'MAKE_LATEST': 'false' if alpha else 'true'}


def verify_source_versions(root):
    version = manifest_field((root / 'manifest.yml').read_text(), 'version')
    numeric = version.split('-')[0] + '.0'
    for project in PROJECTS:
        tree = ET.parse(root / 'src' / project / (project + '.csproj'))
        for field, expected in [('Version', version), ('AssemblyVersion', numeric),
                                ('FileVersion', numeric)]:
            values = [node.text for node in tree.findall('.//' + field)]
            if values != [expected]:
                raise ValueError(f'{project} {field} must equal {expected}')
    rpc = (root / 'src/MCP4Rhino.Logic/McpJsonRpc.cs').read_text()
    if re.findall(r'ServerVersion\s*=\s*"([^"]+)"', rpc) != [version]:
        raise ValueError('MCP server version must equal manifest version')
    return version


def verify_package(package, version, skill_root):
    with zipfile.ZipFile(package) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise ValueError('Package contains duplicate entries')
        required = set(REQUIRED_FILES)
        required.update('skills/grasshopper-parametric-design/' + str(p.relative_to(skill_root)).replace('\\', '/')
                        for p in skill_root.rglob('*') if p.is_file())
        missing = required - set(names)
        if missing:
            raise ValueError('Package missing: ' + ', '.join(sorted(missing)))
        if any(Path(name).name.lower() in SDK_FILES for name in names):
            raise ValueError('Package must not bundle Rhino/Grasshopper SDK assemblies')
        manifest = archive.read('manifest.yml').decode('utf-8-sig')
        for field, expected in [('name', 'MCP4Rhino'), ('version', version), ('icon', 'icon.png')]:
            if manifest_field(manifest, field) != expected:
                raise ValueError(f'Package manifest {field} must equal {expected}')
        if archive.testzip():
            raise ValueError('Package contains corrupt files')


def select_build_run(runs, sha, branch):
    for run in runs:
        if (run.get('headSha') == sha and run.get('headBranch') == branch
                and run.get('event') == 'push' and run.get('conclusion') == 'success'):
            return run['databaseId']
    raise ValueError(f'No successful push Build for exact SHA {sha} on {branch}')


def verify_artifact(payload):
    matches = [a for a in payload['artifacts'] if a['name'] == 'yak-packages']
    if len(matches) != 1 or matches[0].get('expired') is not False or matches[0].get('size_in_bytes', 0) <= 0:
        raise ValueError('Exactly one available, nonempty yak-packages artifact is required; rerun Build')
    return matches[0]['id']


def listing_versions(listing):
    matches = re.findall(r'^MCP4Rhino\s+\(([^)]+)\)\s*$', listing, re.MULTILINE | re.IGNORECASE)
    if len(matches) != 1:
        raise ValueError('Yak search did not return an unambiguous MCP4Rhino listing')
    versions = {value.strip() for value in matches[0].split(',')}
    if any(not re.fullmatch(r'[0-9]+(?:\.[0-9]+){2,3}(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?', v)
           for v in versions):
        raise ValueError('Yak search returned an unexpected version format')
    return versions


def ensure_unpublished(yak, version):
    if not os.environ.get('YAK_TOKEN'):
        raise ValueError('YAK_TOKEN must be supplied by the production environment')
    result = subprocess.run([yak, 'search', 'MCP4Rhino', '--all', '--prerelease'],
                            capture_output=True, text=True, check=True)
    if version in listing_versions(result.stdout):
        raise ValueError(f'MCP4Rhino {version} already exists; use a new version and tag')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    metadata = commands.add_parser('metadata')
    metadata.add_argument('tag')
    metadata.add_argument('workflow_ref')
    commands.add_parser('source-versions')
    package = commands.add_parser('package')
    package.add_argument('path', type=Path)
    package.add_argument('version', nargs='?')
    build = commands.add_parser('select-run')
    build.add_argument('sha')
    build.add_argument('branch')
    commands.add_parser('artifact')
    registry = commands.add_parser('registry')
    registry.add_argument('yak')
    registry.add_argument('version')
    execute(parser.parse_args())


def execute(args):
    if args.command == 'metadata':
        version = verify_source_versions(ROOT)
        print(json.dumps(release_metadata(version, args.tag, args.workflow_ref)))
    elif args.command == 'source-versions':
        print(verify_source_versions(ROOT))
    elif args.command == 'package':
        version = args.version or manifest_field((ROOT / 'manifest.yml').read_text(), 'version')
        verify_package(args.path, version, ROOT / 'skills/grasshopper-parametric-design')
        print(f'OK package: {args.path} ({version})')
    elif args.command == 'select-run':
        print(select_build_run(json.load(sys.stdin), args.sha, args.branch))
    elif args.command == 'artifact':
        print(verify_artifact(json.load(sys.stdin)))
    elif args.command == 'registry':
        ensure_unpublished(args.yak, args.version)
        print(f'MCP4Rhino {args.version} is not yet published')


if __name__ == '__main__':
    try:
        main()
    except subprocess.CalledProcessError as error:
        sys.exit(f'Release check failed: external command exited with status {error.returncode}')
    except (ValueError, OSError, KeyError, zipfile.BadZipFile, ET.ParseError) as error:
        sys.exit(f'Release check failed: {error}')
