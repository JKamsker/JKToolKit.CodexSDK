#!/usr/bin/env python3
"""Tests for the upstream sync CI/merge/release gate."""

from __future__ import annotations

import unittest
from unittest.mock import patch

import upstream_sync_gate as gate


class FakeGitHub:
    def __init__(self, json_responses: list[object] | None = None) -> None:
        self.commands: list[list[str]] = []
        self.json_responses = list(json_responses or [])

    def run(self, args: list[str]) -> str:
        self.commands.append(args)
        return ""

    def json(self, args: list[str]) -> object:
        self.commands.append(args)
        if not self.json_responses:
            raise AssertionError(f"No fake JSON response remains for: {args}")
        return self.json_responses.pop(0)


def context(attempt: int = 0) -> gate.GateContext:
    return gate.GateContext(
        repo="owner/repo",
        pr=42,
        branch="automation/upstream-codex-1.2.3",
        version="1.2.3",
        attempt=attempt,
        source_run="1000",
    )


def open_pull(head: str = "abc123") -> dict[str, object]:
    return {
        "number": 42,
        "state": "OPEN",
        "baseRefName": "master",
        "headRefName": "automation/upstream-codex-1.2.3",
        "headRefOid": head,
        "url": "https://github.com/owner/repo/pull/42",
    }


class LoadPullRequestTests(unittest.TestCase):
    def test_accepts_expected_open_pull_request(self) -> None:
        github = FakeGitHub([open_pull()])

        result = gate.load_pr(github, context())

        self.assertEqual("abc123", result["headRefOid"])

    def test_rejects_unexpected_head_branch(self) -> None:
        pull = open_pull()
        pull["headRefName"] = "untrusted"
        github = FakeGitHub([pull])

        with self.assertRaisesRegex(RuntimeError, "not the expected branch"):
            gate.load_pr(github, context())


class RepairTests(unittest.TestCase):
    def test_dispatches_next_bounded_attempt(self) -> None:
        github = FakeGitHub()

        result = gate.schedule_repair(github, context(attempt=1), "222", "ci")

        self.assertTrue(result)
        command = github.commands[0]
        self.assertEqual(["workflow", "run", gate.REPAIR_WORKFLOW], command[:3])
        self.assertIn("repair_attempt=2", command)
        self.assertIn("repair_source_run=222", command)
        self.assertIn("repair_source_job=ci", command)

    def test_creates_issue_after_final_attempt(self) -> None:
        github = FakeGitHub([[]])

        result = gate.schedule_repair(
            github,
            context(attempt=gate.MAX_REPAIR_ATTEMPTS),
            "333",
            "ci",
        )

        self.assertFalse(result)
        self.assertTrue(any(command[:2] == ["issue", "create"] for command in github.commands))
        self.assertFalse(any(command[:2] == ["workflow", "run"] for command in github.commands))


class MergeTests(unittest.TestCase):
    def test_merges_only_the_tested_head(self) -> None:
        github = FakeGitHub([open_pull(), {"merged": True, "sha": "merge456"}])

        result = gate.merge_exact_head(github, context(), "abc123")

        self.assertEqual("merge456", result)
        merge_command = github.commands[-1]
        self.assertIn("sha=abc123", merge_command)
        self.assertIn("merge_method=merge", merge_command)

    def test_refuses_changed_head(self) -> None:
        github = FakeGitHub([open_pull("new-head")])

        with self.assertRaisesRegex(RuntimeError, "untested commit"):
            gate.merge_exact_head(github, context(), "tested-head")


class GateFlowTests(unittest.TestCase):
    @patch.object(gate, "verify_release", return_value=True)
    @patch.object(gate, "merge_exact_head", return_value="merge456")
    @patch.object(
        gate,
        "dispatch_gated_ci",
        return_value={"databaseId": 77, "conclusion": "success"},
    )
    @patch.object(gate, "load_pr", return_value=open_pull())
    def test_green_ci_merges_and_verifies_release(
        self,
        _load: object,
        _dispatch: object,
        merge: object,
        release: object,
    ) -> None:
        github = FakeGitHub()

        result = gate.run_gate(github, context())

        self.assertEqual(0, result)
        merge.assert_called_once_with(github, context(), "abc123")
        release.assert_called_once_with(github, context(), "merge456")

    @patch.object(gate, "schedule_repair", return_value=True)
    @patch.object(
        gate,
        "dispatch_gated_ci",
        return_value={"databaseId": 88, "conclusion": "failure"},
    )
    @patch.object(gate, "load_pr", return_value=open_pull())
    def test_red_ci_schedules_repair_without_merging(
        self,
        _load: object,
        _dispatch: object,
        repair: object,
    ) -> None:
        github = FakeGitHub()

        result = gate.run_gate(github, context())

        self.assertEqual(0, result)
        repair.assert_called_once_with(github, context(), "88", "ci")


if __name__ == "__main__":
    unittest.main()
