#!/usr/bin/env python3
"""Tests for the upstream sync CI/merge/release gate."""

from __future__ import annotations

import unittest
from dataclasses import replace
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
        actor="maintainer",
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
        self.assertIn("trusted_actor=maintainer", command)
        self.assertTrue(any('"actor":"maintainer"' in value for value in command))
        self.assertTrue(any('"item_number":"42"' in value for value in command))

    def test_creates_issue_after_final_attempt(self) -> None:
        github = FakeGitHub([open_pull(), []])

        result = gate.schedule_repair(
            github,
            context(attempt=gate.MAX_REPAIR_ATTEMPTS),
            "333",
            "ci",
        )

        self.assertFalse(result)
        self.assertTrue(any(command[:2] == ["issue", "create"] for command in github.commands))
        self.assertFalse(any(command[:2] == ["workflow", "run"] for command in github.commands))


class PersistentRepairTests(unittest.TestCase):
    def test_exhausted_chain_stays_paused_on_next_scheduled_run(self) -> None:
        title = gate.repair_pause_title(context(), "abc123")
        github = FakeGitHub([open_pull(), [{"title": title, "url": "issue-url"}]])

        self.assertFalse(gate.can_resume(github, context(attempt=0)))
        self.assertFalse(any(command[:2] == ["workflow", "run"] for command in github.commands))

    def test_unrelated_search_match_does_not_pause(self) -> None:
        github = FakeGitHub([open_pull(), [{"title": "unrelated"}], []])
        self.assertTrue(gate.can_resume(github, context()))

    def test_closed_issue_allows_retry(self) -> None:
        github = FakeGitHub([open_pull(), [], []])
        self.assertTrue(gate.can_resume(github, context()))
        issue_query = github.commands[1]
        self.assertEqual("open", issue_query[issue_query.index("--state") + 1])

    def test_new_code_or_automation_has_a_new_retry_budget(self) -> None:
        title = gate.repair_pause_title(context(), "abc123")
        self.assertEqual(title, gate.repair_pause_title(context(attempt=3), "abc123"))
        self.assertNotEqual(title, gate.repair_pause_title(context(), "fixed-head"))
        with patch.object(gate.Path, "read_bytes", return_value=b"updated automation"):
            self.assertNotEqual(title, gate.repair_pause_title(context(), "abc123"))

    def test_active_repair_prevents_a_second_chain_for_same_pr(self) -> None:
        for status in ("queued", "in_progress", "waiting"):
            with self.subTest(status=status):
                github = FakeGitHub([open_pull(), [], [{
                    "displayTitle": "Upstream Sync Repair PR #42 attempt 2",
                    "status": status, "url": "run-url",
                }]])
                self.assertFalse(gate.can_resume(github, context()))

    def test_completed_and_other_pr_repairs_do_not_block(self) -> None:
        github = FakeGitHub([open_pull(), [], [
            {"displayTitle": "Upstream Sync Repair PR #42 attempt 3", "status": "completed"},
            {"displayTitle": "Upstream Sync Repair PR #420 attempt 1", "status": "in_progress"},
        ]])
        self.assertTrue(gate.can_resume(github, context()))


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


class ReleaseTests(unittest.TestCase):
    def test_builtin_token_dispatches_exact_merge_and_ignores_old_runs(self) -> None:
        old_run = {"databaseId": 10, "displayTitle": "Release upstream merge merge456"}
        release_run = {
            "databaseId": 11, "displayTitle": "Release upstream merge merge456",
            "headSha": "newer-default-head", "status": "completed", "conclusion": "success",
        }
        github = FakeGitHub([
            [old_run], [old_run, release_run], release_run,
            {"jobs": [{"name": "NugetUpload", "conclusion": "success"}]},
        ])

        self.assertTrue(gate.verify_release(
            github, replace(context(), release_via_dispatch=True), "merge456",
        ))

        dispatch = next(command for command in github.commands if command[:2] == ["workflow", "run"])
        self.assertIn("publish=true", dispatch)
        self.assertIn("release_sha=merge456", dispatch)
        self.assertEqual("master", dispatch[dispatch.index("--ref") + 1])
        for command in github.commands:
            if command[:2] == ["run", "list"]:
                self.assertEqual("master", command[command.index("--branch") + 1])

    def test_owner_token_uses_push_release_without_duplicate_dispatch(self) -> None:
        release_run = {
            "databaseId": 12, "headSha": "merge456", "status": "completed", "conclusion": "success",
        }
        github = FakeGitHub([
            [release_run], release_run,
            {"jobs": [{"name": "NugetUpload", "conclusion": "success"}]},
        ])

        self.assertTrue(gate.verify_release(github, context(), "merge456"))
        self.assertFalse(any(command[:2] == ["workflow", "run"] for command in github.commands))

    def test_successful_workflow_with_skipped_upload_is_not_a_release(self) -> None:
        release_run = {
            "databaseId": 12, "headSha": "merge456", "status": "completed", "conclusion": "success",
        }
        github = FakeGitHub([
            [release_run], release_run,
            {"jobs": [{"name": "NugetUpload", "conclusion": "skipped"}]}, [],
        ])

        self.assertFalse(gate.verify_release(github, context(), "merge456"))
        self.assertTrue(any(command[:2] == ["issue", "create"] for command in github.commands))


if __name__ == "__main__":
    unittest.main()
