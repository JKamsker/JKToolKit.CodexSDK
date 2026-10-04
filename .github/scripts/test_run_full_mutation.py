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
        self.output = self.root / 'artifacts'
        self.provenance = dict.fromkeys(('revision', 'source_tree_sha256', 'test_tree_sha256',
                                        'tool_manifest_sha256', 'configuration_sha256'), 'stable')
        self.calls = []

    def execute(self, *, snapshots=None, failure=False):
        def run(command, **kwargs):
            self.calls.append((command, kwargs))
            return subprocess.CompletedProcess(command, 1 if failure and 'stryker' in command else 0)
        with mock.patch.object(subject, '__file__', str(self.root / '.github/scripts/run_full_mutation.py')), \
             mock.patch('sys.argv', ['run', '--profile', 'sdk', '--profile', 'sdk',
                                     '--output', str(self.output), '--dotnet', '/fake/dotnet']), \
             mock.patch.object(subject.shutil, 'which', return_value='/fake/dotnet'), \
             mock.patch.object(subject, 'capture', side_effect=snapshots or [self.provenance, self.provenance]), \
             mock.patch.object(subject, 'summarize', return_value={'groups': []}), \
             mock.patch.object(subject.subprocess, 'run', side_effect=run), \
             mock.patch.dict(os.environ, {'CODEX_E2E': '1', 'CODEX_DOCKER_E2E': '1'}):
            subject.main()

    def test_sets_fixture_runtime_disables_live_and_deduplicates_profiles(self):
        self.execute()
        self.assertEqual(len(self.calls), 2)
        command, arguments = self.calls[-1]
        self.assertEqual(arguments['env']['DOTNET_ROOT'], '/fake')
        self.assertNotIn('CODEX_E2E', arguments['env'])
        self.assertNotIn('CODEX_DOCKER_E2E', arguments['env'])
        self.assertIn('--skip-version-check', command)
        self.assertEqual(json.loads((self.output / 'sdk.provenance.json').read_text()), self.provenance)
        self.assertTrue((self.output / 'summary.json').exists())

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
