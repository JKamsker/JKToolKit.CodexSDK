"""Release notes, publication safety, and retry behavior."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import publish_sdk_release as release


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'docs').mkdir()
        (self.root / 'UPSTREAM_CODEX_VERSION.json').write_text(json.dumps({'api': '0.167.0', 'integration': '0.167.0'}))
        (self.root / 'Directory.Build.props').write_text('<Project><PropertyGroup><VersionPrefix>0.167.1</VersionPrefix><CodexCliVersion>0.167.0</CodexCliVersion></PropertyGroup></Project>')
        self.report = self.root / 'docs/codex-0.166.0-to-0.167.0-interop.md'
        self.report.write_text('# Research\n\n## Confirmed Upstream Changes\n\nCross-provider resume.\n\n## SDK Changes\n\nRemoved provider filter.\n\n## Validation\n\nFull tests pass.\n\n## Remaining Drift\n\nExperimental methods use raw interop.\n')

    def test_notes_distinguish_sdk_patch_from_cli_and_preserve_implementation_and_limits(self):
        version, title, body = release.release_notes(self.root, 'owner/sdk', 'a' * 40, '- CLI agent center')
        self.assertEqual('0.167.1', version)
        self.assertIn('Codex CLI 0.167.0', title)
        for text in ('Removed provider filter.', 'Experimental methods use raw interop.', 'Full tests pass.', 'CLI agent center', '/tag/rust-v0.167.0', '/packages/JKToolKit.CodexSDK/0.167.1'):
            self.assertIn(text, body)

    def test_incomplete_parity_or_report_prevents_release(self):
        self.report.write_text('## SDK Changes\n\nSomething changed.\n')
        with self.assertRaisesRegex(ValueError, 'Confirmed Upstream Changes'):
            release.release_notes(self.root, 'owner/sdk', 'a' * 40)
        (self.root / 'UPSTREAM_CODEX_VERSION.json').write_text(json.dumps({'api': '0.167.0', 'integration': '0.166.0'}))
        with self.assertRaisesRegex(ValueError, 'parity must be complete'):
            release.release_notes(self.root, 'owner/sdk', 'a' * 40)

    @patch.object(release, 'gh')
    @patch.object(release.subprocess, 'run')
    def test_existing_release_is_idempotent(self, run, gh):
        run.return_value = subprocess.CompletedProcess([], 0, json.dumps({'html_url': 'release-url', 'draft': False, 'prerelease': False}), '')
        self.assertEqual('release-url', release.publish('owner/sdk', 'a' * 40, '0.167.0', 'title', 'notes', True))
        gh.assert_not_called()

    @patch.object(release, 'gh')
    @patch.object(release.subprocess, 'run')
    def test_only_404_creates_release_with_exact_target_and_notes_file(self, run, gh):
        run.return_value = subprocess.CompletedProcess([], 1, '', 'HTTP 403: Forbidden')
        with self.assertRaises(RuntimeError):
            release.publish('owner/sdk', 'a' * 40, '0.167.0', 'title', 'notes')
        gh.assert_not_called()
        run.return_value = subprocess.CompletedProcess([], 1, '', 'HTTP 404: Not Found')
        def create(*args):
            self.assertEqual('a' * 40, args[args.index('--target') + 1])
            self.assertEqual('notes', Path(args[args.index('--notes-file') + 1]).read_text())
            return 'created'
        gh.side_effect = create
        self.assertEqual('created', release.publish('owner/sdk', 'a' * 40, '0.167.0', 'title', 'notes'))

    @patch.object(release, 'gh')
    def test_recovery_requires_successful_exact_commit_nuget_upload(self, gh):
        gh.side_effect = [json.dumps([{'databaseId': 1, 'event': 'workflow_dispatch', 'headSha': 'newer', 'displayTitle': 'Release upstream merge ' + 'a' * 40, 'conclusion': 'success'}]), json.dumps({'jobs': [{'name': 'NugetUpload', 'conclusion': 'skipped'}]})]
        with self.assertRaisesRegex(ValueError, 'No successful NuGet'):
            release.verify_published('owner/sdk', 'a' * 40)


if __name__ == '__main__':
    unittest.main()
