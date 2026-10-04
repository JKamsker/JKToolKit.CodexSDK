#!/usr/bin/env python3
"""Capture a mutation campaign's inputs and summarize Stryker JSON without hiding statuses.

Capture BEFORE running Stryker. Summaries validate embedded report sources against
that capture. Reports from different commits or source/test snapshots stay separate;
overlapping files within one snapshot are rejected instead of double counted.
"""
from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess

STATUSES = ('Killed', 'Timeout', 'Survived', 'NoCoverage', 'CompileError',
            'Ignored', 'RuntimeError', 'Pending')
EVALUATED = ('Killed', 'Timeout', 'Survived', 'NoCoverage')


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def canonical_hash(value) -> str:
    return digest(json.dumps(value, sort_keys=True, separators=(',', ':')).encode())


def capture(repository: Path, config: Path) -> dict:
    repository = repository.resolve()
    def git(*args):
        return subprocess.check_output(['git', '-C', str(repository), *args])
    tracked = git('ls-files', '-z', '--cached', '--others', '--exclude-standard', '--', 'src', 'tests').decode().split('\0')
    sources, tests = {}, {}
    for name in sorted(filter(None, tracked)):
        path = repository / name
        if not path.is_file():
            continue
        target = sources if name.startswith('src/') else tests
        target[name] = digest(path.read_bytes())
    manifest = repository / 'dotnet-tools.json'
    return {
        'schema_version': 1,
        'captured_at_utc': datetime.now(timezone.utc).isoformat(),
        'repository_root': str(repository),
        'revision': git('rev-parse', 'HEAD').decode().strip(),
        'dirty': bool(git('status', '--porcelain', '--untracked-files=no').strip()),
        'source_tree_sha256': canonical_hash(sources),
        'test_tree_sha256': canonical_hash(tests),
        'source_files': sources,
        'test_files': tests,
        'tool_manifest_sha256': digest(manifest.read_bytes()),
        'tool_manifest': json.loads(manifest.read_text()),
        'configuration_sha256': digest(config.read_bytes()),
        'configuration': json.loads(config.read_text()),
    }


def metrics(counts: Counter) -> dict:
    total = sum(counts.values())
    detected = counts['Killed'] + counts['Timeout']
    evaluated = sum(counts[s] for s in EVALUATED)
    unresolved = total - evaluated - counts['CompileError'] - counts['Ignored']
    return {
        'statuses': {s: counts[s] for s in (*STATUSES, *sorted(set(counts) - set(STATUSES)))},
        'total_generated': total,
        'detected': detected,
        'evaluated_denominator': evaluated,
        'evaluated_score_percent': round(100 * detected / evaluated, 4) if evaluated else None,
        'raw_detected_score_percent': round(100 * detected / total, 4) if total else None,
        'unresolved': unresolved,
        'complete': unresolved == 0,
    }


def summarize(inputs: list[tuple[Path, Path]]) -> dict:
    groups = {}
    for report_path, provenance_path in inputs:
        report = json.loads(report_path.read_text())
        provenance = json.loads(provenance_path.read_text())
        if provenance.get('schema_version') != 1:
            raise ValueError(f'Unsupported provenance schema: {provenance_path}')
        for kind in ('source', 'test'):
            if canonical_hash(provenance[f'{kind}_files']) != provenance[f'{kind}_tree_sha256']:
                raise ValueError(f'Invalid {kind} manifest digest: {provenance_path}')
        identity = tuple(provenance[k] for k in (
            'revision', 'source_tree_sha256', 'test_tree_sha256', 'tool_manifest_sha256'))
        group = groups.setdefault(identity, {
            'revision': identity[0], 'source_tree_sha256': identity[1],
            'test_tree_sha256': identity[2], 'tool_manifest_sha256': identity[3],
            'reports': [], 'files': {}, '_counts': Counter(),
        })
        report_counts = Counter()
        root = Path(provenance['repository_root'])
        project = Path(report['projectRoot'])
        for name, entry in report['files'].items():
            path = Path(name)
            absolute = path if path.is_absolute() else project / path
            try:
                relative = absolute.relative_to(root).as_posix()
            except ValueError as exc:
                raise ValueError(f'Report source outside captured repository: {name}') from exc
            mutants = entry.get('mutants', [])
            if not mutants:
                # Stryker replaces excluded source text with an explanatory placeholder.
                continue
            expected = provenance['source_files'].get(relative)
            if expected is None or digest(entry['source'].encode()) != expected:
                raise ValueError(f'Source differs from captured revision: {relative}')
            if relative in group['files']:
                raise ValueError(f'Overlapping reports would double count: {relative}')
            counts = Counter(m.get('status', 'Unknown') for m in mutants)
            triage = []
            reasons = Counter()
            for mutant in mutants:
                status = mutant.get('status', 'Unknown')
                if status in ('Ignored', 'CompileError', 'RuntimeError'):
                    reasons[f"{status}: {mutant.get('statusReason', '(not provided)')}"] += 1
                if status not in ('Killed', 'Timeout', 'Ignored', 'CompileError'):
                    triage.append({k: mutant[k] for k in (
                        'id', 'status', 'mutatorName', 'location', 'replacement', 'statusReason') if k in mutant})
            group['files'][relative] = {**metrics(counts), 'status_reasons': dict(reasons), 'triage': triage}
            group['_counts'].update(counts)
            report_counts.update(counts)
        group['reports'].append({
            'report': str(report_path), 'report_sha256': digest(report_path.read_bytes()),
            'provenance': str(provenance_path),
            'configuration_sha256': provenance['configuration_sha256'],
            'configuration': provenance['configuration'], **metrics(report_counts),
        })
    result = []
    for group in groups.values():
        group.update(metrics(group.pop('_counts')))
        result.append(group)
    return {'schema_version': 1, 'score_definitions': {
        'evaluated_score_percent': '100 * (Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage)',
        'raw_detected_score_percent': '100 * (Killed + Timeout) / all generated mutants, including ignored and invalid',
        'complete': 'No Pending, RuntimeError, missing status, or unknown status mutants; does not imply full source scope.',
        'grouping': 'Different commits, source trees, test trees, or tool manifests are never combined.',
    }, 'groups': result}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    c = commands.add_parser('capture')
    c.add_argument('--repository', type=Path, required=True)
    c.add_argument('--config', type=Path, required=True)
    c.add_argument('--output', type=Path, required=True)
    s = commands.add_parser('summarize')
    s.add_argument('--input', nargs=2, type=Path, action='append', required=True,
                   metavar=('REPORT', 'PROVENANCE'))
    s.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        result = capture(args.repository, args.config) if args.command == 'capture' else summarize(args.input)
    except (ValueError, KeyError, OSError, subprocess.CalledProcessError) as exc:
        parser.error(str(exc))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, sort_keys=True) + '\n')


if __name__ == '__main__':
    main()
