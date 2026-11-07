#!/usr/bin/env python3
"""Publish a versioned GitHub release from the validated SDK parity report."""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import tempfile
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def gh(*args: str) -> str:
    return subprocess.check_output(['gh', *args], text=True).strip()


def release_notes(root: Path, repo: str, target: str, upstream_notes: str = "") -> tuple[str, str, str]:
    marker = json.loads((root / 'UPSTREAM_CODEX_VERSION.json').read_text())
    cli = marker['api']
    if marker['integration'] != cli:
        raise ValueError('SDK parity must be complete before publishing a release')
    props = ET.parse(root / 'Directory.Build.props')
    version = props.findtext('.//VersionPrefix')
    if props.findtext('.//CodexCliVersion') != cli or not re.fullmatch(r'\d+\.\d+\.\d+', version or ''):
        raise ValueError('SDK package version and CLI baseline must be synchronized')
    reports = sorted((root / 'docs').glob(f'codex-*-to-{cli}-interop.md'))
    if len(reports) != 1:
        raise ValueError(f'Expected one parity report for Codex CLI {cli}, found {len(reports)}')
    report = reports[0]
    content = report.read_text().strip()
    for section in ('Confirmed Upstream Changes', 'SDK Changes', 'Validation', 'Remaining Drift'):
        match = re.search(rf'^## {section}[^\S\n]*\n(.*?)(?=^## |\Z)', content, re.MULTILINE | re.DOTALL)
        if not match or not match[1].strip():
            raise ValueError(f'Parity report must describe {section}')
    # Keep the complete reviewed report, including validation and limitations.
    content = re.sub(r'^# .*\n', '', content, count=1).strip()
    content = re.sub(r'^(#{2,}) ', r'#\1 ', content, flags=re.MULTILINE)
    upstream_notes = re.sub(r'^(#{2,}) ', r'#\1 ', upstream_notes, flags=re.MULTILINE)
    notes = (
        f'JKToolKit.CodexSDK **{version}** integrates Codex CLI **{cli}**.\n\n'
        f'[What is new in Codex CLI {cli}](https://github.com/openai/codex/releases/tag/rust-v{cli}) · '
        f'[SDK parity report](https://github.com/{repo}/blob/{target}/{report.relative_to(root).as_posix()})\n\n'
        'The upstream changes and their SDK implementation are detailed below. '
        'Terminal UI and runtime features are delivered by the bundled CLI; '
        'the report identifies typed SDK support and remaining raw protocol surfaces.\n\n'
        + (f'## What is new in Codex CLI\n\n{upstream_notes}\n\n' if upstream_notes else '')
        + f'## How CodexSDK implements this release\n\n{content}\n\n## Packages\n\n'
        + '\n'.join(f'- [{name} {version}](https://www.nuget.org/packages/{name}/{version})'
                    for name in ('JKToolKit.CodexSDK', 'JKToolKit.CodexSDK.SemanticKernel', 'JKToolKit.CodexSDK.AgentFramework'))
        + f'\n\nBundled runtime packages use CLI version `{cli}`.\n'
    )
    return version, f'{version}: Codex CLI {cli} integration', notes


def verify_published(repo: str, target: str) -> None:
    runs = json.loads(gh('run', 'list', '--repo', repo, '--workflow', 'ci.yml', '--branch', 'master',
                         '--limit', '100', '--json', 'databaseId,event,headSha,displayTitle,conclusion'))
    for run in runs:
        exact_push = run['event'] == 'push' and run['headSha'] == target
        exact_dispatch = run['event'] == 'workflow_dispatch' and run['displayTitle'] == f'Release upstream merge {target}'
        if run['conclusion'] != 'success' or not (exact_push or exact_dispatch):
            continue
        jobs = json.loads(gh('run', 'view', str(run['databaseId']), '--repo', repo, '--json', 'jobs'))['jobs']
        if any(job['name'] == 'NugetUpload' and job['conclusion'] == 'success' for job in jobs):
            return
    raise ValueError(f'No successful NuGet publication found for {target}')


def publish(repo: str, target: str, version: str, title: str, notes: str, verify: bool = False) -> str:
    # Only a real 404 means absent; auth/transport errors must fail the job.
    lookup = subprocess.run(['gh', 'api', f'repos/{repo}/releases/tags/{version}'], text=True, capture_output=True)
    if lookup.returncode == 0:
        existing = json.loads(lookup.stdout)
        if existing.get('draft') or existing.get('prerelease'):
            raise ValueError(f'Release {version} already exists but is not a published stable release')
        return existing['html_url']
    if 'HTTP 404' not in lookup.stderr:
        raise RuntimeError(lookup.stderr)
    if verify:
        verify_published(repo, target)
    with tempfile.TemporaryDirectory() as tmp:
        body = Path(tmp) / 'release.md'
        body.write_text(notes)
        return gh('release', 'create', version, '--repo', repo, '--target', target,
                  '--title', title, '--notes-file', str(body), '--latest')


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', required=True)
    parser.add_argument('--target', required=True)
    parser.add_argument('--verify-published', action='store_true', help='Require existing successful NuGet CI (sync recovery).')
    parser.add_argument('--notes-output', type=Path, help='Write reviewable notes without publishing.')
    args = parser.parse_args()
    if not re.fullmatch(r'[0-9a-f]{40}', args.target):
        parser.error('--target must be a full commit SHA')
    cli = json.loads((ROOT / 'UPSTREAM_CODEX_VERSION.json').read_text())['api']
    upstream = gh('release', 'view', f'rust-v{cli}', '--repo', 'openai/codex', '--json', 'body', '--jq', '.body')
    upstream = re.split(r'^## (?:Changelog|Full Changelog)\s*$', upstream, maxsplit=1, flags=re.MULTILINE)[0].strip()
    version, title, notes = release_notes(ROOT, args.repo, args.target, upstream)
    if args.notes_output:
        args.notes_output.write_text(notes)
        return
    print(publish(args.repo, args.target, version, title, notes, args.verify_published))


if __name__ == '__main__':
    main()
