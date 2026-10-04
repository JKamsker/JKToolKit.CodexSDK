#!/usr/bin/env python3
"""Run reproducible, offline full mutation profiles in sequence (four workers each)."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess

from summarize_mutation import capture, summarize

PROFILES = ('sdk', 'agentframework', 'semantickernel')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', choices=PROFILES, action='append',
                        help='Repeat to select profiles; defaults to all three.')
    parser.add_argument('--output', type=Path, required=True,
                        help='New artifact directory (must not already exist).')
    parser.add_argument('--dotnet', default='dotnet')
    args = parser.parse_args()
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
    subprocess.run([executable, 'tool', 'restore'], cwd=root, env=environment, check=True)
    inputs = []
    for profile in dict.fromkeys(args.profile or PROFILES):
        config = project / f'stryker-full-{profile}-config.json'
        provenance = capture(root, config)
        provenance_path = output / f'{profile}.provenance.json'
        provenance_path.write_text(json.dumps(provenance, indent=2) + '\n')
        command = [executable, 'stryker', '--skip-version-check', '--config-file',
                   config.name, '--output', str(output / profile)]
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
