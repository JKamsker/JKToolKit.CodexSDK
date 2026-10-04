import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest import mock

import run_full_mutation as subject


class RunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'tests/JKToolKit.CodexSDK.Tests').mkdir(parents=True)
        (self.root / 'tests/JKToolKit.CodexSDK.Tests/stryker-full-sdk-config.json').write_text(
            json.dumps({'stryker-config': {'concurrency': 4, 'mutate': ['**/*.cs']}}))
        self.output = self.root / 'artifacts'
        self.provenance = dict.fromkeys(('revision', 'source_tree_sha256', 'test_tree_sha256',
                                        'tool_manifest_sha256', 'configuration_sha256'), 'stable')
        self.calls = []

    def execute(self, *, snapshots=None, failure=False, extra_args=()):
        def run(command, **kwargs):
            self.calls.append((command, kwargs))
            return subprocess.CompletedProcess(command, 1 if failure and 'stryker' in command else 0)
        with mock.patch.object(subject, '__file__', str(self.root / '.github/scripts/run_full_mutation.py')), \
             mock.patch('sys.argv', ['run', '--profile', 'sdk', '--profile', 'sdk',
                                     '--output', str(self.output), '--dotnet', '/fake/dotnet', *extra_args]), \
             mock.patch.object(subject.shutil, 'which', return_value='/fake/dotnet'), \
             mock.patch.object(subject, 'capture', side_effect=snapshots or [self.provenance, self.provenance]), \
             mock.patch.object(subject, 'summarize', return_value={'groups': []}), \
             mock.patch.object(subject.subprocess, 'run', side_effect=run), \
             mock.patch.dict(os.environ, {'CODEX_E2E': '1', 'CODEX_DOCKER_E2E': '1'}):
            subject.main()

    def test_sets_fixture_runtime_disables_live_and_deduplicates_profiles(self):
        self.execute()
        self.assertEqual(len(self.calls), 3)
        command, arguments = self.calls[-1]
        self.assertEqual(arguments['env']['DOTNET_ROOT'], '/fake')
        self.assertNotIn('CODEX_E2E', arguments['env'])
        self.assertNotIn('CODEX_DOCKER_E2E', arguments['env'])
        self.assertIn('--skip-version-check', command)
        self.assertEqual(arguments['env']['PATH'].split(os.pathsep)[0], str(self.output / 'offline-bin'))
        self.assertEqual(json.loads((self.output / 'sdk.provenance.json').read_text()), self.provenance)
        self.assertTrue((self.output / 'summary.json').exists())

    def test_installed_cli_paths_removed_and_stub_fails_fast(self):
        installed = self.root / 'installed'
        installed.mkdir()
        (installed / 'codex').write_text('not the test fixture')
        env = {'PATH': str(installed) + os.pathsep + '/fake/dotnet-root'}
        self.output.mkdir()
        subject.isolate_default_cli(env, self.output)
        self.assertNotIn(str(installed), env['PATH'].split(os.pathsep))
        self.assertIn('/fake/dotnet-root', env['PATH'].split(os.pathsep))
        if os.name != 'nt':
            process = subprocess.run([str(self.output / 'offline-bin/codex'), 'exec'], capture_output=True)
            self.assertEqual(process.returncode, 64)
            self.assertIn(b'disabled', process.stderr)

    def test_bundled_cli_is_rejected_before_running_mutants(self):
        bundled = self.root / 'tests/bin/codex-runtime/1/linux-x64/bin'
        bundled.mkdir(parents=True)
        (bundled / 'codex').write_text('bundled cli')
        with self.assertRaisesRegex(RuntimeError, 'bypasses offline'):
            self.execute()
        self.assertEqual(self.calls, [])

    def test_worker_override_is_captured_in_exact_effective_config(self):
        self.execute(extra_args=('--concurrency', '16', '--verbosity', 'debug'))
        effective = self.output / 'sdk.effective-config.json'
        config = json.loads(effective.read_text())['stryker-config']
        self.assertEqual(config['concurrency'], 16)
        self.assertEqual(config['verbosity'], 'debug')
        self.assertEqual(config['mutate'], ['**/*.cs'])
        self.assertIn(str(effective), self.calls[-1][0])
        original = self.root / 'tests/JKToolKit.CodexSDK.Tests/stryker-full-sdk-config.json'
        self.assertEqual(json.loads(original.read_text())['stryker-config']['concurrency'], 4)

    def test_zero_workers_rejected(self):
        with self.assertRaises(SystemExit):
            self.execute(extra_args=('--concurrency', '0'))
        self.assertEqual(self.calls, [])

    def test_changed_input_rejects_summary(self):
        changed = dict(self.provenance, source_tree_sha256='changed')
        with self.assertRaisesRegex(RuntimeError, 'inputs changed'):
            self.execute(snapshots=[self.provenance, changed])
        self.assertFalse((self.output / 'summary.json').exists())

    def test_initial_test_or_mutation_failure_preserves_evidence(self):
        with self.assertRaisesRegex(SystemExit, 'sdk failed'):
            self.execute(failure=True)
        self.assertTrue((self.output / 'sdk.provenance.json').exists())
        self.assertTrue((self.output / 'sdk.log').exists())
        self.assertFalse((self.output / 'summary.json').exists())

    def test_existing_artifacts_cannot_be_overwritten(self):
        self.output.mkdir()
        with self.assertRaises(SystemExit):
            self.execute()
        self.assertEqual(self.calls, [])


if __name__ == '__main__':
    unittest.main()
