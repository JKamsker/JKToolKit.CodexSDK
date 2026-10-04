"""Ensure a release cannot silently skip changed SDK contents under an old version."""

from io import BytesIO
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import HTTPError, URLError
from zipfile import ZipFile

import verify_sdk_release as release


def package(package_id, *, payload=b"assembly", signed=False, version="0.160.0"):
    """Create a minimal unsigned or repository-signed package fixture."""
    stream = BytesIO()
    with ZipFile(stream, "w") as archive:
        archive.writestr(f"{package_id}.nuspec", (
            '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">'
            f"<metadata><id>{package_id}</id><version>{version}</version></metadata></package>"
        ))
        archive.writestr(f"lib/net10.0/{package_id}.dll", payload)
        if signed:
            archive.writestr(".signature.p7s", b"repository-signature")
    return stream.getvalue()


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        """Stage all SDK packages separately from a reusable runtime package."""
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for package_id in release.SDK_PACKAGES:
            (self.root / f"{package_id}.0.160.0.nupkg").write_bytes(package(package_id))
        (self.root / "JKToolKit.CodexSDK.Runtime.linux-x64.0.160.0.nupkg").write_bytes(b"runtime")

    @patch.object(release, "published_package", return_value=None)
    def test_new_release_requires_all_three_sdk_packages(self, published):
        """Plan all SDK identities and leave independent runtime packages alone."""
        pending = release.plan_release(self.root, "0.160.0")
        self.assertEqual(3, len(pending))
        self.assertTrue(all(path.is_file() for path in pending))
        self.assertEqual([(name, "0.160.0") for name in release.SDK_PACKAGES],
                         [call.args for call in published.call_args_list])

    @patch.object(release, "published_package")
    def test_signed_identical_retry_skips_all_sdk_packages(self, published):
        """Allow an identical retry after NuGet.org adds its repository signature."""
        published.side_effect = lambda name, version: package(name, signed=True)
        self.assertEqual([], release.plan_release(self.root, "0.160.0"))

    @patch.object(release, "published_package")
    def test_partial_publication_resumes_only_missing_packages(self, published):
        """Recover after a previous upload published only the core SDK."""
        published.side_effect = lambda name, version: package(name, signed=True) if name == release.SDK_PACKAGES[0] else None
        pending = release.plan_release(self.root, "0.160.0")
        self.assertEqual([f"{name}.0.160.0.nupkg" for name in release.SDK_PACKAGES[1:]],
                         [path.name for path in pending])

    @patch.object(release, "published_package")
    def test_conflict_in_any_sdk_package_rejects_release(self, published):
        """Reject old contents for each SDK identity, even when others are unpublished."""
        for conflict in release.SDK_PACKAGES:
            with self.subTest(conflict=conflict):
                published.side_effect = lambda name, version: package(name, payload=b"old") if name == conflict else None
                with self.assertRaisesRegex(ValueError, "--bump-patch"):
                    release.plan_release(self.root, "0.160.0")

    @patch.object(release, "published_package", return_value=None)
    def test_missing_or_misidentified_artifact_fails(self, _published):
        """Do not report a successful release when an SDK artifact is absent or wrong."""
        path = self.root / f"{release.SDK_PACKAGES[-1]}.0.160.0.nupkg"
        path.unlink()
        with self.assertRaises(FileNotFoundError):
            release.plan_release(self.root, "0.160.0")
        path.write_bytes(package("Unexpected.Package"))
        with self.assertRaisesRegex(ValueError, "identity"):
            release.plan_release(self.root, "0.160.0")

    @patch.object(release, "published_package")
    def test_conflict_removes_stale_publish_manifest(self, published):
        """A failure cannot leave a previous plan available to a publishing step."""
        published.side_effect = lambda name, version: package(name, payload=b"old")
        manifest = self.root / "pending.txt"
        manifest.write_text("stale-package.nupkg\n")
        with patch("sys.argv", ["verify", "--packages", str(self.root), "--version", "0.160.0", "--output", str(manifest)]):
            self.assertEqual(1, release.main())
        self.assertFalse(manifest.exists())


class FetchTests(unittest.TestCase):
    @patch.object(release, "urlopen")
    def test_only_404_means_unpublished(self, open_url):
        """Fail closed on authentication, transport, and NuGet service failures."""
        open_url.side_effect = HTTPError("url", 404, "missing", {}, None)
        self.assertIsNone(release.published_package("JKToolKit.CodexSDK", "0.160.0"))
        for error in (HTTPError("url", 503, "unavailable", {}, None),
                      HTTPError("url", 403, "forbidden", {}, None), URLError("offline")):
            with self.subTest(error=error):
                open_url.side_effect = error
                with self.assertRaises(URLError):
                    release.published_package("JKToolKit.CodexSDK", "0.160.0")


if __name__ == "__main__":
    unittest.main()
