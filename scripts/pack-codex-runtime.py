#!/usr/bin/env python3
"""Pack opt-in, pinned RID runtimes. Downloads are verified against the committed lock."""
import argparse
import base64
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import tarfile
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
RIDS = {'linux-x64': 'linux-x64', 'linux-arm64': 'linux-arm64',
        'win-x64': 'win32-x64', 'win-arm64': 'win32-arm64',
        'osx-x64': 'darwin-x64', 'osx-arm64': 'darwin-arm64'}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rid', choices=RIDS, action='append')
    parser.add_argument('--update-lock', action='store_true')
    parser.add_argument('--output', type=Path, default=ROOT / 'nuget-packages')
    args = parser.parse_args()
    version = json.loads((ROOT / 'UPSTREAM_CODEX_VERSION.json').read_text())['api']
    lock_path = ROOT / 'runtime' / 'runtime-lock.json'
    if args.update_lock:
        lock = {'version': version, 'packages': {}}
        for rid, platform in RIDS.items():
            with urllib.request.urlopen(f'https://registry.npmjs.org/@openai/codex/{version}-{platform}', timeout=60) as response:
                dist = json.load(response)['dist']
            lock['packages'][rid] = {k: dist[k] for k in ('tarball', 'integrity')}
        lock_path.write_text(json.dumps(lock, indent=2) + '\n')
        return
    lock = json.loads(lock_path.read_text())
    if lock['version'] != version:
        raise SystemExit('Runtime lock differs from SDK pin. Run --update-lock and review the new hashes.')
    args.output.mkdir(parents=True, exist_ok=True)
    for rid in args.rid or RIDS:
        pack_runtime(rid, version, lock['packages'][rid], args.output.resolve())

def pack_runtime(rid, version, entry, output):
    work = ROOT / 'artifacts' / 'runtime' / rid
    work.mkdir(parents=True, exist_ok=True)
    archive = work / 'runtime.tgz'
    if not archive.exists():
        with urllib.request.urlopen(entry['tarball'], timeout=120) as response, archive.open('wb') as dest:
            shutil.copyfileobj(response, dest)
    digest = base64.b64encode(hashlib.file_digest(archive.open('rb'), 'sha512').digest()).decode()
    if entry['integrity'] != 'sha512-' + digest:
        archive.unlink()
        raise SystemExit(f'Integrity check failed for {rid}')
    content = work / 'content'
    if content.exists(): shutil.rmtree(content)
    content.mkdir()
    with tarfile.open(archive) as tar:
        for member in tar.getmembers():
            parts = PurePosixPath(member.name).parts
            if '..' in parts or member.name.startswith('/') or member.issym() or member.islnk():
                raise SystemExit(f'Unsafe archive member: {member.name}')
            if not member.isfile(): continue
            # Preserve the official vendor layout, including rg, sandbox helpers, and notices.
            if len(parts) > 3 and parts[:2] == ('package', 'vendor'):
                relative = Path(*parts[3:])
            elif parts[-1].lower().startswith(('license', 'notice')):
                relative = Path(parts[-1])
            else: continue
            dest = content / relative
            dest.parent.mkdir(parents=True, exist_ok=True)
            with tar.extractfile(member) as source, dest.open('wb') as target:
                shutil.copyfileobj(source, target)
            dest.chmod(member.mode & 0o777)
    binary = content / 'bin' / ('codex.exe' if rid.startswith('win-') else 'codex')
    if not binary.exists(): raise SystemExit(f'Missing expected executable: {binary}')
    # NuGet extraction does not promise Unix executable modes. Restore them after copy and publish.
    package_id = f'JKToolKit.CodexSDK.Runtime.{rid}'
    prefix = f'codex-runtime/{version}/{rid}'
    targets = work / f'{package_id}.targets'
    targets.write_text(f'''<Project>
  <ItemGroup>
    <Content Include="$(MSBuildThisFileDirectory)../content/**/*">
      <Link>{prefix}/%(RecursiveDir)%(Filename)%(Extension)</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
      <Pack>false</Pack>
    </Content>
  </ItemGroup>
  <Target Name="CodexRuntimePermissions_{rid.replace('-', '_')}" AfterTargets="CopyFilesToOutputDirectory;Publish" Condition="!$([MSBuild]::IsOSPlatform('Windows'))">
    <ItemGroup>
      <_CodexExecutable_{rid.replace('-', '_')} Include="$(TargetDir){prefix}/**/*;$(PublishDir){prefix}/**/*" />
    </ItemGroup>
    <Exec Command="chmod u+x &quot;%(_CodexExecutable_{rid.replace('-', '_')}.Identity)&quot;" Condition="'@(_CodexExecutable_{rid.replace('-', '_')})' != ''" />
  </Target>
</Project>
''')
    shutil.copy(ROOT / 'runtime' / 'LICENSE-CODEX', work / 'LICENSE')
    for notice in (ROOT / 'runtime').glob('*-CODEX'):
        shutil.copy(notice, content / notice.name)
    project = work / 'Runtime.csproj'
    project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework><PackageId>{package_id}</PackageId>
    <Version>{version}</Version><Authors>JKamsker;OpenAI</Authors>
    <Description>Optional Codex CLI {version} runtime for {rid}. Contains OpenAI Codex and its bundled helpers.</Description>
    <PackageLicenseFile>LICENSE</PackageLicenseFile><IncludeBuildOutput>false</IncludeBuildOutput>
    <EnableDefaultItems>false</EnableDefaultItems><SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
    <NoWarn>NU5128</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <None Include="content/**/*" Pack="true" PackagePath="content/" />
    <None Include="{package_id}.targets" Pack="true" PackagePath="buildTransitive/" />
    <None Include="LICENSE" Pack="true" PackagePath="/" />
  </ItemGroup>
</Project>
''')
    subprocess.run(['dotnet', 'pack', str(project), '-c', 'Release', '-o', str(output)], check=True)
    print(f'Packed {package_id} {version}', flush=True)

if __name__ == '__main__': main()
