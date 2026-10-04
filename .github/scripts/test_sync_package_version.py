"""Exercise release version transitions and the CI version guard."""

import importlib.util
import json
from pathlib import Path
import os
import shutil
import subprocess
import tempfile
import unittest

from test_upstream_workflows import step_script


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("sync_package_version", ROOT / "scripts/sync-package-version.py")
VERSIONING = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERSIONING)


class PackageVersionTests(unittest.TestCase):
    def setUp(self):
        """Create an isolated checkout fixture for each test."""
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.props = self.root / "Directory.Build.props"

    def setup_versions(self, upstream, baseline, package):
        """Write independent CLI, parity, and package version markers."""
        (self.root / "UPSTREAM_CODEX_VERSION.json").write_text(json.dumps({"api": upstream, "integration": "0.159.0"}))
        self.props.write_text(
            f"<Project><PropertyGroup><CodexCliVersion>{baseline}</CodexCliVersion>"
            f"<VersionPrefix>{package}</VersionPrefix></PropertyGroup></Project>"
        )

    def test_sync_transitions_are_idempotent(self):
        """Exercise CLI upgrades, SDK patches, collisions, and repeated syncs."""
        for upstream, baseline, package, expected in [
            ("0.160.0", "0.159.3", "0.159.3", "0.160.0"),
            ("0.160.0", "0.160.0", "0.160.2", "0.160.2"),
            ("0.161.0", "0.160.0", "0.160.2", "0.161.0"),
            ("0.160.1", "0.160.0", "0.160.0", "0.160.1"),
            ("0.160.1", "0.160.0", "0.160.1", "0.160.2"),
            ("0.160.1", "0.160.0", "0.160.3", "0.160.4"),
            ("0.160.0", "0.160.0", "0.0.123", "0.160.0"),
        ]:
            with self.subTest(upstream=upstream, baseline=baseline, package=package):
                self.setup_versions(upstream, baseline, package)
                self.assertEqual(expected, VERSIONING.update_version(self.root))
                contents = self.props.read_bytes()
                self.assertEqual(expected, VERSIONING.update_version(self.root))
                self.assertEqual(expected, VERSIONING.update_version(self.root, check=True))
                self.assertEqual(contents, self.props.read_bytes())
                self.assertIn(f"<CodexCliVersion>{upstream}</CodexCliVersion>", self.props.read_text())

    def test_sdk_patch_does_not_change_cli_pin(self):
        """Keep the CLI pin stable across successive SDK-only releases."""
        self.setup_versions("0.160.0", "0.160.0", "0.160.0")
        marker = (self.root / "UPSTREAM_CODEX_VERSION.json").read_bytes()
        self.assertEqual("0.160.1", VERSIONING.update_version(self.root, bump_patch=True))
        self.assertEqual("0.160.2", VERSIONING.update_version(self.root, bump_patch=True))
        self.assertEqual("0.160.2", VERSIONING.update_version(self.root, check=True))
        self.assertEqual(marker, (self.root / "UPSTREAM_CODEX_VERSION.json").read_bytes())

    def test_check_and_bump_reject_stale_baseline_without_mutation(self):
        """Reject stale version state before any files are written."""
        self.setup_versions("0.161.0", "0.160.0", "0.160.2")
        before = self.props.read_bytes()
        for options in ({"check": True}, {"bump_patch": True}):
            with self.assertRaises(ValueError):
                VERSIONING.update_version(self.root, **options)
            self.assertEqual(before, self.props.read_bytes())

    def test_invalid_versions_fail_without_mutation(self):
        """Reject malformed versions while retaining the original project file."""
        for invalid in ("0.160.0-preview", "0.160", "01.160.0", "", "0.160.0\n"):
            with self.subTest(invalid=invalid):
                self.setup_versions(invalid, "0.160.0", "0.160.0")
                before = self.props.read_bytes()
                with self.assertRaises(ValueError):
                    VERSIONING.update_version(self.root)
                self.assertEqual(before, self.props.read_bytes())

    def test_comments_are_preserved_during_sync_and_patch_bumps(self):
        """Ignore commented properties before and after their live counterparts."""
        self.setup_versions("0.161.0", "0.160.0", "0.160.2")
        comment = "<!--\n<VersionPrefix>0.1.0</VersionPrefix>\n<CodexCliVersion>0.1.0</CodexCliVersion>\n-->"
        original = self.props.read_text().replace("<PropertyGroup>", f"<PropertyGroup>{comment}")
        original = original.replace("</PropertyGroup>", f"{comment}</PropertyGroup>")
        self.props.write_text(original)
        self.assertEqual("0.161.0", VERSIONING.update_version(self.root))
        self.assertEqual(2, self.props.read_text().count(comment))
        self.assertEqual("0.161.1", VERSIONING.update_version(self.root, bump_patch=True))
        self.assertEqual(2, self.props.read_text().count(comment))
        self.assertEqual("0.161.1", VERSIONING.update_version(self.root, check=True))

    def test_missing_and_duplicate_live_properties_still_fail(self):
        """Comments never substitute for a missing or duplicated live property."""
        for name in ("CodexCliVersion", "VersionPrefix"):
            for duplicate in (False, True):
                with self.subTest(name=name, duplicate=duplicate):
                    self.setup_versions("0.161.0", "0.160.0", "0.160.0")
                    prop = f"<{name}>0.160.0</{name}>"
                    replacement = f"<!-- {prop} -->" + (prop * 2 if duplicate else "")
                    self.props.write_text(self.props.read_text().replace(prop, replacement))
                    before = self.props.read_bytes()
                    with self.assertRaisesRegex(ValueError, f"exactly one {name}"):
                        VERSIONING.update_version(self.root)
                    self.assertEqual(before, self.props.read_bytes())


class CiVersionTests(unittest.TestCase):
    def setUp(self):
        """Create an isolated checkout fixture for each test."""
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.output = self.root / "output"

    def run_script(self, script, **env):
        """Execute a real CI shell step with isolated output files."""
        self.output.write_text("")
        return subprocess.run(
            ["bash", "-c", script], cwd=self.root,
            env={**os.environ, "GITHUB_OUTPUT": str(self.output), **env},
            capture_output=True, text=True,
        )

    def test_ci_uses_committed_version_and_rejects_drift(self):
        """Export the committed version and stop CI when its CLI baseline drifts."""
        # Run the actual CI shell step against a standalone checkout fixture.
        (self.root / "scripts").mkdir()
        shutil.copy(ROOT / "scripts/sync-package-version.py", self.root / "scripts")
        shutil.copy(ROOT / "Directory.Build.props", self.root)
        marker = self.root / "UPSTREAM_CODEX_VERSION.json"
        shutil.copy(ROOT / "UPSTREAM_CODEX_VERSION.json", marker)
        env_path = self.root / "env"
        script = step_script("ci.yml", "Compute Version")
        result = self.run_script(script, GITHUB_ENV=str(env_path))
        self.assertEqual(0, result.returncode, result.stderr)
        expected = VERSIONING.update_version(self.root, check=True)
        self.assertEqual(f"version={expected}\n", self.output.read_text())
        self.assertEqual(f"VERSION={expected}\n", env_path.read_text())

        marker.write_text(json.dumps({"api": "9.0.0"}))
        result = self.run_script(script, GITHUB_ENV=str(env_path))
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("", self.output.read_text())


if __name__ == "__main__":
    unittest.main()
