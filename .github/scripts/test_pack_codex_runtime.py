import base64
import hashlib
import importlib.util
import io
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import tempfile
import unittest
from unittest.mock import patch
from xml.sax.saxutils import escape, quoteattr


SCRIPT = Path(__file__).resolve().parents[2] / 'scripts' / 'pack-codex-runtime.py'
SPEC = importlib.util.spec_from_file_location('pack_codex_runtime', SCRIPT)
PACKER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PACKER)


class RuntimePackagingTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='runtime packaging ')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'runtime').mkdir()
        (self.root / 'runtime' / 'LICENSE-CODEX').write_text('license')
        (self.root / 'runtime' / 'NOTICE-CODEX').write_text('notice')
        self.root_patch = patch.object(PACKER, 'ROOT', self.root)
        self.root_patch.start()
        self.addCleanup(self.root_patch.stop)

    @staticmethod
    def archive(version):
        output = io.BytesIO()
        with tarfile.open(fileobj=output, mode='w:gz') as archive:
            for name, mode in [('bin/codex', 0o755), ('codex-path/helper tool', 0o750), ('NOTICE', 0o644)]:
                data = version.encode()
                member = tarfile.TarInfo('package/vendor/test-target/' + name)
                member.mode = mode
                member.size = len(data)
                archive.addfile(member, io.BytesIO(data))
        return output.getvalue()

    def pack(self, version, data):
        entry = {'tarball': 'https://example.invalid/' + version,
                 'integrity': 'sha512-' + base64.b64encode(hashlib.sha512(data).digest()).decode()}
        with patch.object(PACKER.urllib.request, 'urlopen', return_value=io.BytesIO(data)) as download, \
                patch.object(PACKER.subprocess, 'run'):
            PACKER.pack_runtime('linux-x64', version, entry, self.root / 'packages')
        return download.call_count

    def test_upstream_update_downloads_new_archive_and_keeps_existing_version_cache(self):
        old = self.archive('1.0.0')
        new = self.archive('2.0.0')
        self.assertEqual(1, self.pack('1.0.0', old))
        self.assertEqual(1, self.pack('2.0.0', new))
        self.assertEqual(0, self.pack('1.0.0', old))
        self.assertEqual(0, self.pack('2.0.0', new))

    def test_cached_archive_is_still_integrity_checked(self):
        data = self.archive('1.0.0')
        self.pack('1.0.0', data)
        cached = self.root / 'artifacts/runtime/1.0.0/linux-x64/runtime.tgz'
        cached.write_bytes(b'corrupt cached download')
        with self.assertRaisesRegex(SystemExit, 'Integrity check failed'):
            self.pack('1.0.0', data)
        self.assertFalse(cached.exists())

    @unittest.skipIf(os.name == 'nt' or shutil.which('dotnet') is None, 'Requires Unix and MSBuild')
    def test_build_and_publish_allow_other_users_to_execute_only_upstream_executables(self):
        self.pack('1.0.0', self.archive('1.0.0'))
        work = self.root / 'artifacts/runtime/1.0.0/linux-x64'
        # Model NuGet extraction losing Unix executable modes.
        for path in (work / 'content').rglob('*'):
            if path.is_file():
                path.chmod(0o644)
        output = self.root / 'build output'
        publish = self.root / 'publish output'
        (work / 'buildTransitive').mkdir()
        targets = work / 'buildTransitive/JKToolKit.CodexSDK.Runtime.linux-x64.targets'
        shutil.copy(work / targets.name, targets)
        project = self.root / 'consumer.proj'
        project.write_text(f'''<Project>
  <PropertyGroup>
    <TargetDir>{escape(str(output))}/</TargetDir>
    <PublishDir>{escape(str(publish))}/</PublishDir>
  </PropertyGroup>
  <Import Project={quoteattr(str(targets))} />
  <Target Name="CopyFilesToOutputDirectory">
    <Copy SourceFiles="@(Content)" DestinationFiles="@(Content->'$(TargetDir)%(Link)')" />
  </Target>
  <Target Name="Publish">
    <Copy SourceFiles="@(Content)" DestinationFiles="@(Content->'$(PublishDir)%(Link)')" />
  </Target>
</Project>''')
        # Build and publish run separately, as in ordinary dotnet commands.
        for target, directory in [('CopyFilesToOutputDirectory', output), ('Publish', publish)]:
            result = subprocess.run(['dotnet', 'msbuild', str(project), '-nologo', '-t:' + target],
                                    capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            payload = directory / 'codex-runtime/1.0.0/linux-x64'
            for name in ['bin/codex', 'codex-path/helper tool']:
                self.assertEqual(0o555, (payload / name).stat().st_mode & 0o555)
            for name in ['NOTICE', 'LICENSE-CODEX', 'NOTICE-CODEX']:
                self.assertEqual(0, (payload / name).stat().st_mode & 0o111)


if __name__ == '__main__':
    unittest.main()
