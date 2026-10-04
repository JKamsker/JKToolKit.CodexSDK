#!/usr/bin/env python3
"""Run reproducible, offline full mutation profiles in sequence (four workers by default)."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess

from summarize_mutation import capture, summarize

PROFILES = ('sdk', 'agentframework', 'semantickernel')


def isolate_default_cli(environment, output):
    """Block default CLI fallback while preserving explicitly selected test fixtures."""
    shim = output / 'offline-bin'
    shim.mkdir()
    (shim / 'codex').write_text('#!/bin/sh\nprintf "%s\\n" "Codex execution disabled in mutation tests" >&2\nexit 64\n')
    (shim / 'codex').chmod(0o755)
    (shim / 'codex.cmd').write_text('@echo Codex execution disabled in mutation tests 1>&2\r\n@exit /b 64\r\n')
    names = ('codex', 'codex.exe', 'codex.cmd', 'codex.bat')
    # A path-resolution mutant might skip the first match: remove real CLI dirs too.
    safe_paths = [item for item in environment.get('PATH', '').split(os.pathsep)
                  if item and not any((Path(item) / name).exists() for name in names)]
    environment['PATH'] = os.pathsep.join([str(shim), *safe_paths])


def reject_bundled_cli(root):
    for directory in (root / 'src', root / 'tests'):
        for runtime in directory.rglob('codex-runtime'):
            if any(p.name in ('codex', 'codex.exe') for p in runtime.rglob('*')):
                raise RuntimeError(f'Bundled Codex executable bypasses offline PATH isolation: {runtime}')


def prepare_profile(executable, root, project, environment, output, profile):
    # A prior failed/aborted run may leave an instrumented reference or stale
    # .stryker-unchanged backup. Force fresh dependencies, then retire backups so
    # Stryker snapshots this build instead of restoring an earlier revision.
    subprocess.run([executable, 'build', str(project / 'JKToolKit.CodexSDK.Tests.csproj'),
                    '-c', 'Release', '-t:Rebuild'], cwd=root, env=environment, check=True)
    reject_bundled_cli(root)
    for backup in (project / 'bin').rglob('*.stryker-unchanged'):
        archive = output / f'{profile}.previous-backups' / backup.relative_to(project / 'bin')
        archive.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(backup, archive)
        backup.unlink()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', choices=PROFILES, action='append',
                        help='Repeat to select profiles; defaults to all three.')
    parser.add_argument('--output', type=Path, required=True,
                        help='New artifact directory (must not already exist).')
    parser.add_argument('--dotnet', default='dotnet')
    parser.add_argument('--concurrency', type=int, help='Override worker count in each effective profile.')
    parser.add_argument('--verbosity', choices=('error', 'warning', 'info', 'debug', 'trace'),
                        help='Override Stryker logging verbosity.')
    args = parser.parse_args()
    if args.concurrency is not None and args.concurrency < 1:
        parser.error('Concurrency must be a positive integer.')
    root = Path(__file__).resolve().parents[2]
    project = root / 'tests/JKToolKit.CodexSDK.Tests'
    output = args.output.resolve()
    if output.exists():
        parser.error('Output already exists; use a new directory to avoid mixing runs.')
    executable = shutil.which(args.dotnet)
    if not executable:
        parser.error(f'dotnet executable not found: {args.dotnet}')
    executable = str(Path(executable).resolve())
    environment = dict(os.environ)
    environment['DOTNET_ROOT'] = str(Path(executable).parent)
    environment['PATH'] = environment['DOTNET_ROOT'] + os.pathsep + environment.get('PATH', '')
    environment.pop('CODEX_E2E', None)
    environment.pop('CODEX_DOCKER_E2E', None)
    output.mkdir(parents=True)
    isolate_default_cli(environment, output)
    reject_bundled_cli(root)
    subprocess.run([executable, 'tool', 'restore'], cwd=root, env=environment, check=True)
    inputs = []
    for profile in dict.fromkeys(args.profile or PROFILES):
        prepare_profile(executable, root, project, environment, output, profile)
        settings = json.loads((project / f'stryker-full-{profile}-config.json').read_text())
        if args.concurrency is not None:
            settings['stryker-config']['concurrency'] = args.concurrency
        if args.verbosity is not None:
            settings['stryker-config']['verbosity'] = args.verbosity
        config = output / f'{profile}.effective-config.json'
        config.write_text(json.dumps(settings, indent=2) + '\n')
        provenance = capture(root, config)
        provenance_path = output / f'{profile}.provenance.json'
        provenance_path.write_text(json.dumps(provenance, indent=2) + '\n')
        command = [executable, 'stryker', '--skip-version-check', '--config-file',
                   str(config), '--output', str(output / profile)]
        print(f'Running {profile}; log: {output / (profile + ".log")}', flush=True)
        with (output / f'{profile}.log').open('w') as log:
            result = subprocess.run(command, cwd=project, env=environment, stdout=log, stderr=subprocess.STDOUT)
        after = capture(root, config)
        if any(provenance[k] != after[k] for k in (
            'revision', 'source_tree_sha256', 'test_tree_sha256',
            'tool_manifest_sha256', 'configuration_sha256')):
            raise RuntimeError('Campaign inputs changed during execution; report is not reproducible.')
        if result.returncode:
            raise SystemExit(f'{profile} failed ({result.returncode}); inspect {output / (profile + ".log")}')
        report = output / profile / 'reports/mutation-report.json'
        inputs.append((report, provenance_path))
        (output / 'summary.json').write_text(json.dumps(summarize(inputs), indent=2) + '\n')
    print(f'Summary: {output / "summary.json"}')


if __name__ == '__main__':
    main()
