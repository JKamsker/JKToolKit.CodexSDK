import copy
import json
from pathlib import Path
import tempfile
import unittest

import summarize_mutation as subject


class SummaryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = 'class Sample {}\n'
        self.provenance = {
            'schema_version': 1, 'repository_root': '/captured', 'revision': 'abc',
            'source_files': {'src/SDK/Sample.cs': subject.digest(self.source.encode())},
            'test_files': {'tests/Test.cs': 'testhash'},
            'tool_manifest_sha256': 'toolhash', 'configuration_sha256': 'confighash',
            'configuration': {'stryker-config': {'mutate': ['**/*.cs']}},
        }
        for kind in ('source', 'test'):
            self.provenance[f'{kind}_tree_sha256'] = subject.canonical_hash(self.provenance[f'{kind}_files'])
        self.report = {'projectRoot': '/captured/src/SDK', 'files': {
            '/captured/src/SDK/Sample.cs': {'source': self.source, 'mutants': []}}}

    def write(self, statuses, *, suffix='', provenance=None, report=None):
        provenance = copy.deepcopy(provenance or self.provenance)
        report = copy.deepcopy(report or self.report)
        entry = next(iter(report['files'].values()))
        entry['mutants'] = [{'id': str(i), 'status': status, 'replacement': '{}',
                             'mutatorName': 'Block', 'location': {'start': {'line': 1, 'column': 1}},
                             'statusReason': 'sample reason'} for i, status in enumerate(statuses)]
        p = self.root / f'report{suffix}.json'
        q = self.root / f'provenance{suffix}.json'
        p.write_text(json.dumps(report))
        q.write_text(json.dumps(provenance))
        return p, q

    def test_every_status_and_both_scores_are_preserved(self):
        summary = subject.summarize([self.write(subject.STATUSES + ('FutureStatus',))])
        group = summary['groups'][0]
        self.assertEqual(group['total_generated'], 9)
        self.assertEqual(group['detected'], 2)
        self.assertEqual(group['evaluated_denominator'], 4)
        self.assertEqual(group['evaluated_score_percent'], 50)
        self.assertEqual(group['raw_detected_score_percent'], 22.2222)
        self.assertEqual(group['unresolved'], 3)
        self.assertFalse(group['complete'])
        self.assertEqual(group['statuses']['FutureStatus'], 1)
        entry = group['files']['src/SDK/Sample.cs']
        self.assertEqual(len(entry['triage']), 5)
        self.assertEqual(entry['status_reasons']['CompileError: sample reason'], 1)

    def test_different_revisions_are_separate(self):
        alternate = copy.deepcopy(self.provenance)
        alternate['revision'] = 'def'
        result = subject.summarize([self.write(['Killed']), self.write(['Survived'], suffix='2', provenance=alternate)])
        self.assertEqual(len(result['groups']), 2)
        self.assertEqual([g['evaluated_score_percent'] for g in result['groups']], [100, 0])

    def test_different_tests_and_source_snapshots_are_separate(self):
        alternate = copy.deepcopy(self.provenance)
        alternate['test_files']['tests/Test.cs'] = 'changed'
        alternate['test_tree_sha256'] = subject.canonical_hash(alternate['test_files'])
        result = subject.summarize([self.write(['Killed']), self.write(['Survived'], suffix='2', provenance=alternate)])
        self.assertEqual(len(result['groups']), 2)

    def test_overlap_rejected(self):
        pair = self.write(['Killed'])
        with self.assertRaisesRegex(ValueError, 'double count'):
            subject.summarize([pair, pair])

    def test_modified_embedded_source_rejected(self):
        report = copy.deepcopy(self.report)
        next(iter(report['files'].values()))['source'] = 'different source'
        with self.assertRaisesRegex(ValueError, 'Source differs'):
            subject.summarize([self.write(['Killed'], report=report)])

    def test_corrupt_manifest_rejected(self):
        provenance = copy.deepcopy(self.provenance)
        provenance['source_files']['src/SDK/Sample.cs'] = 'changed'
        with self.assertRaisesRegex(ValueError, 'manifest digest'):
            subject.summarize([self.write(['Killed'], provenance=provenance)])

    def test_report_cannot_escape_repository(self):
        report = copy.deepcopy(self.report)
        report['files']['/other/Sample.cs'] = report['files'].pop('/captured/src/SDK/Sample.cs')
        with self.assertRaisesRegex(ValueError, 'outside captured'):
            subject.summarize([self.write(['Killed'], report=report)])

    def test_no_evaluated_mutants_is_null_not_perfect(self):
        group = subject.summarize([self.write(['Ignored', 'CompileError'])])['groups'][0]
        self.assertIsNone(group['evaluated_score_percent'])
        self.assertEqual(group['raw_detected_score_percent'], 0)

    def test_empty_report_has_no_scores(self):
        group = subject.summarize([self.write([])])['groups'][0]
        self.assertIsNone(group['evaluated_score_percent'])
        self.assertIsNone(group['raw_detected_score_percent'])

    def test_excluded_source_placeholder_has_no_mutants(self):
        report = copy.deepcopy(self.report)
        next(iter(report['files'].values()))['source'] = 'File ignored using mutate filter'
        group = subject.summarize([self.write([], report=report)])['groups'][0]
        self.assertEqual(group['total_generated'], 0)

    def test_relative_report_paths(self):
        report = copy.deepcopy(self.report)
        report['files']['Sample.cs'] = report['files'].pop('/captured/src/SDK/Sample.cs')
        group = subject.summarize([self.write(['Killed'], report=report)])['groups'][0]
        self.assertEqual(list(group['files']), ['src/SDK/Sample.cs'])

    def test_distinct_files_same_snapshot_aggregate(self):
        provenance = copy.deepcopy(self.provenance)
        provenance['source_files']['src/SDK/Other.cs'] = subject.digest(self.source.encode())
        provenance['source_tree_sha256'] = subject.canonical_hash(provenance['source_files'])
        report = copy.deepcopy(self.report)
        report['files']['Other.cs'] = report['files'].pop('/captured/src/SDK/Sample.cs')
        result = subject.summarize([self.write(['Killed'], provenance=provenance),
                                    self.write(['NoCoverage'], suffix='2', provenance=provenance, report=report)])
        group = result['groups'][0]
        self.assertEqual(group['evaluated_score_percent'], 50)
        self.assertTrue(group['complete'])


if __name__ == '__main__':
    unittest.main()
