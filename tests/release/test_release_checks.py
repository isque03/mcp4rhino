"""Release contracts exercised without credentials, Rhino, or external services."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts'))
import release_checks as release


class ReleaseMetadataTests(unittest.TestCase):
    def test_alpha_and_successors_stay_out_of_latest_stable(self):
        for version in ('0.6.0-alpha', '0.6.0-alpha.1', '0.6.0-alpha.12'):
            with self.subTest(version=version):
                metadata = release.release_metadata(version, 'v' + version,
                                                     'refs/heads/' + release.ALPHA_BRANCH)
                self.assertEqual(metadata['PRERELEASE'], 'true')
                self.assertEqual(metadata['MAKE_LATEST'], 'false')

    def test_telemetry_alpha_publishes_from_its_release_branch(self):
        for version in ('0.6.1-alpha', '0.6.1-alpha.1'):
            with self.subTest(version=version):
                metadata = release.release_metadata(
                    version, 'v' + version, 'refs/heads/' + release.TELEMETRY_ALPHA_BRANCH)
                self.assertEqual(metadata['BUILD_BRANCH'], release.TELEMETRY_ALPHA_BRANCH)
                self.assertEqual(metadata['PRERELEASE'], 'true')
                self.assertEqual(metadata['MAKE_LATEST'], 'false')
        with self.assertRaises(ValueError):
            release.release_metadata('0.6.1-alpha', 'v0.6.1-alpha', 'refs/heads/main')
        with self.assertRaises(ValueError):
            release.release_metadata(
                '0.6.1-alpha', 'v0.6.1-alpha', 'refs/heads/' + release.ALPHA_BRANCH)

    def test_tools_alpha_publishes_from_its_release_branch(self):
        for version in ('0.6.2-alpha', '0.6.2-alpha.1'):
            with self.subTest(version=version):
                metadata = release.release_metadata(
                    version, 'v' + version, 'refs/heads/' + release.TOOLS_ALPHA_BRANCH)
                self.assertEqual(metadata['BUILD_BRANCH'], release.TOOLS_ALPHA_BRANCH)
                self.assertEqual(metadata['PRERELEASE'], 'true')
                self.assertEqual(metadata['MAKE_LATEST'], 'false')
        with self.assertRaises(ValueError):
            release.release_metadata('0.6.2-alpha', 'v0.6.2-alpha', 'refs/heads/main')
        with self.assertRaises(ValueError):
            release.release_metadata(
                '0.6.2-alpha', 'v0.6.2-alpha', 'refs/heads/' + release.TELEMETRY_ALPHA_BRANCH)

    def test_graph_alpha_requires_matching_branch(self):
        for version in ('0.6.4-alpha', '0.6.4-alpha.1'):
            metadata = release.release_metadata(version, 'v' + version,
                                                'refs/heads/' + release.GRAPH_ALPHA_BRANCH)
            self.assertEqual(metadata['BUILD_BRANCH'], release.GRAPH_ALPHA_BRANCH)
            self.assertEqual(metadata['PRERELEASE'], 'true')
            self.assertEqual(metadata['MAKE_LATEST'], 'false')
            for branch in ('main', release.TOOLS_ALPHA_BRANCH):
                with self.assertRaises(ValueError):
                    release.release_metadata(version, 'v' + version, 'refs/heads/' + branch)
        for version in ('0.6.4-alpha.0', '0.6.4-alpha.01'):
            with self.assertRaises(ValueError):
                release.release_metadata(version, 'v' + version, 'refs/heads/' + release.GRAPH_ALPHA_BRANCH)

    def test_stable_requires_main(self):
        metadata = release.release_metadata('0.6.0', 'v0.6.0', 'refs/heads/main')
        self.assertEqual(metadata['PRERELEASE'], 'false')
        self.assertEqual(metadata['MAKE_LATEST'], 'true')
        with self.assertRaises(ValueError):
            release.release_metadata('0.6.0', 'v0.6.0', 'refs/heads/' + release.ALPHA_BRANCH)

    def test_reject_mismatched_tag_and_wrong_dispatch_ref(self):
        for tag, ref in [('v0.6.0', 'refs/heads/' + release.ALPHA_BRANCH),
                         ('v0.6.0-alpha', 'refs/heads/main'),
                         ('v0.6.0-alpha', 'refs/tags/v0.6.0-alpha'),
                         ('v0.6.0-alpha', 'refs/heads/feat/grasshopper1')]:
            with self.subTest(tag=tag, ref=ref), self.assertRaises(ValueError):
                release.release_metadata('0.6.0-alpha', tag, ref)

    def test_reject_unsupported_or_invalid_versions(self):
        for version in ('0.6.0-beta', '0.7.0-alpha', '0.6.0-alpha.0',
                        '0.6.0-alpha.01', '00.6.0', '0.6.0-alpha\nVERSION=evil'):
            with self.subTest(version=version), self.assertRaises(ValueError):
                release.release_metadata(version, 'v' + version, 'refs/heads/' + release.ALPHA_BRANCH)

    def test_source_versions_match_manifest_and_mcp(self):
        expected = release.manifest_field((ROOT / 'manifest.yml').read_text(), 'version')
        self.assertEqual(release.verify_source_versions(ROOT), expected)

    def test_detect_source_version_drift(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'manifest.yml').write_text('version: 0.6.0-alpha\n')
            for project in release.PROJECTS:
                parent = root / 'src' / project
                parent.mkdir(parents=True)
                (parent / (project + '.csproj')).write_text(
                    '<Project><PropertyGroup><Version>0.6.0-alpha</Version>'
                    '<AssemblyVersion>0.6.0.0</AssemblyVersion><FileVersion>0.6.0.0</FileVersion>'
                    '</PropertyGroup></Project>')
            rpc = root / 'src/MCP4Rhino.Logic/McpJsonRpc.cs'
            rpc.write_text('public const string ServerVersion = "0.5.3";')
            with self.assertRaisesRegex(ValueError, 'MCP server version'):
                release.verify_source_versions(root)
            rpc.write_text('public const string ServerVersion = "0.6.0-alpha";')
            project = root / 'src/MCP4Rhino/MCP4Rhino.csproj'
            project.write_text(project.read_text().replace('0.6.0.0', '0.5.3.0'))
            with self.assertRaisesRegex(ValueError, 'AssemblyVersion'):
                release.verify_source_versions(root)


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.skill = self.root / 'skill'
        (self.skill / 'references').mkdir(parents=True)
        (self.skill / 'SKILL.md').write_text('entrypoint')
        (self.skill / 'references/workflow.md').write_text('workflow')
        self.files = dict.fromkeys(release.REQUIRED_FILES, b'content')
        self.files['skills/grasshopper-parametric-design/references/workflow.md'] = b'workflow'
        self.files['manifest.yml'] = b'name: MCP4Rhino\nversion: 0.6.0-alpha\nicon: icon.png\n'

    def verify(self):
        package = self.root / 'package.yak'
        with zipfile.ZipFile(package, 'w') as archive:
            for name, data in self.files.items():
                archive.writestr(name, data)
        release.verify_package(package, '0.6.0-alpha', self.skill)

    def test_complete_package(self):
        self.verify()

    def test_allows_packaged_install_token(self):
        self.files['net8.0/telemetry-install-token'] = b'install-token'
        self.verify()

    def test_reject_version_mismatch(self):
        self.files['manifest.yml'] = self.files['manifest.yml'].replace(b'0.6.0-alpha', b'0.5.3')
        with self.assertRaisesRegex(ValueError, 'version'):
            self.verify()

    def test_reject_missing_adapter_or_help_reference(self):
        for name in ('net8.0/MCP4Rhino.Grasshopper.dll',
                     'skills/grasshopper-parametric-design/references/workflow.md'):
            data = self.files.pop(name)
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, 'missing'):
                self.verify()
            self.files[name] = data

    def test_reject_sdk_anywhere_in_archive(self):
        for name in ('net8.0/Grasshopper.dll', 'nested/GH_IO.dll', 'RHINOCOMMON.DLL'):
            self.files[name] = b'sdk'
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, 'SDK'):
                self.verify()
            del self.files[name]

    def test_require_exact_package_name_and_unambiguous_manifest(self):
        original = self.files['manifest.yml']
        for manifest in (original.replace(b'MCP4Rhino', b'MCP4RhinoExperimental'),
                         original + b'version: 0.5.3\n'):
            self.files['manifest.yml'] = manifest
            with self.assertRaises(ValueError):
                self.verify()


class BuildArtifactTests(unittest.TestCase):
    def setUp(self):
        self.sha = 'a' * 40
        self.run = {'databaseId': 123, 'headSha': self.sha, 'headBranch': release.ALPHA_BRANCH,
                    'event': 'push', 'conclusion': 'success'}

    def test_exact_successful_push_build(self):
        self.assertEqual(release.select_build_run([self.run], self.sha, release.ALPHA_BRANCH), 123)

    def test_reject_wrong_sha_branch_pr_or_failed_build(self):
        for field, value in [('headSha', 'b' * 40), ('headBranch', 'main'),
                             ('event', 'pull_request'), ('conclusion', 'failure')]:
            with self.subTest(field=field), self.assertRaises(ValueError):
                release.select_build_run([{**self.run, field: value}], self.sha, release.ALPHA_BRANCH)
        with self.assertRaises(ValueError):
            release.select_build_run([], self.sha, release.ALPHA_BRANCH)

    def test_artifact_must_be_available_unique_and_nonempty(self):
        artifact = {'id': 456, 'name': 'yak-packages', 'expired': False, 'size_in_bytes': 100}
        self.assertEqual(release.verify_artifact({'artifacts': [artifact]}), 456)
        for artifacts in ([], [artifact, artifact], [{**artifact, 'expired': True}],
                          [{**artifact, 'size_in_bytes': 0}], [{**artifact, 'name': 'other'}]):
            with self.subTest(artifacts=artifacts), self.assertRaises(ValueError):
                release.verify_artifact({'artifacts': artifacts})

    @unittest.skipIf(os.name == 'nt', 'Bash downloader runs on the Linux publishing runner')
    def test_download_script_never_downloads_a_wrong_sha(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            fake = path / 'gh'
            fake.write_text('#!/usr/bin/env python3\nimport json, sys\n'
                            'if sys.argv[1:3] == ["run", "list"]:\n'
                            f' print({json.dumps(json.dumps([{**self.run, "headSha": "b" * 40}]))})\n'
                            'else:\n raise SystemExit("must not download")\n')
            fake.chmod(0o755)
            env = {**os.environ, 'PATH': directory + os.pathsep + os.environ['PATH']}
            result = subprocess.run(['bash', str(ROOT / 'scripts/ci-download-build-artifact.sh'),
                                     self.sha, str(path / 'dist'), release.ALPHA_BRANCH],
                                    env=env, capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('exact SHA', result.stderr)
            self.assertFalse((path / 'dist').exists())


class RegistryTests(unittest.TestCase):
    def query(self, listing, version='0.6.0-alpha'):
        result = subprocess.CompletedProcess([], 0, stdout=listing)
        with patch.dict(os.environ, {'YAK_TOKEN': 'unit-test-placeholder'}), \
                patch.object(release.subprocess, 'run', return_value=result) as run:
            release.ensure_unpublished('yak', version)
            self.assertIn('--prerelease', run.call_args.args[0])
            self.assertTrue(run.call_args.kwargs['check'])

    def test_unpublished_version_and_exact_comparison(self):
        self.query('MCP4Rhino (0.6.0-alpha.1, 0.5.3)\n')

    def test_reject_duplicate_alpha_and_numbered_successor(self):
        for version in ('0.6.0-alpha', '0.6.0-alpha.1'):
            with self.subTest(version=version), self.assertRaisesRegex(ValueError, 'already exists'):
                self.query('MCP4Rhino (0.6.0-alpha, 0.6.0-alpha.1, 0.5.3)', version)

    def test_reject_missing_or_malformed_registry_listing(self):
        for listing in ('', 'No packages found', 'MCP4RhinoOther (0.5.3)', 'MCP4Rhino (error)'):
            with self.subTest(listing=listing), self.assertRaises(ValueError):
                self.query(listing)

    def test_network_or_authentication_failure_propagates(self):
        with patch.dict(os.environ, {'YAK_TOKEN': 'unit-test-placeholder'}), \
                patch.object(release.subprocess, 'run', side_effect=subprocess.CalledProcessError(1, 'yak')):
            with self.assertRaises(subprocess.CalledProcessError):
                release.ensure_unpublished('yak', '0.6.0-alpha')

    def test_missing_token_stops_before_any_registry_command(self):
        with patch.dict(os.environ, {}, clear=True), patch.object(release.subprocess, 'run') as run:
            with self.assertRaisesRegex(ValueError, 'YAK_TOKEN'):
                release.ensure_unpublished('yak', '0.6.0-alpha')
            run.assert_not_called()


if __name__ == '__main__':
    unittest.main()
