"""The workflow-run observer must not race the upstream repair orchestrator."""

import os
import unittest
from unittest.mock import patch

import dispatch_parity_repair as repair


class DispatcherTests(unittest.TestCase):
    @patch.dict(os.environ, {"SOURCE_RUN_ID": "123", "GITHUB_REPOSITORY": "owner/repo"})
    @patch.object(repair, "dispatch_repair")
    @patch.object(repair, "load_agent_log", return_value="")
    @patch.object(repair, "load_source_run")
    def test_only_standalone_parity_failure_dispatches_repair(self, load_run, load_log, dispatch):
        failed_run = {
            "conclusion": "failure",
            "jobs": [{"name": "parity / agent", "steps": [
                {"name": "Check whether parity validation is required", "conclusion": "success"},
                {"name": "Build before safe output", "conclusion": "failure"},
            ]}],
        }
        load_run.return_value = {**failed_run, "workflowName": "Upstream Sync (@openai/codex)"}
        self.assertEqual(0, repair.main())
        load_log.assert_not_called()
        dispatch.assert_not_called()

        load_run.return_value = {**failed_run, "workflowName": "Codex SDK Parity Pass"}
        self.assertEqual(0, repair.main())
        dispatch.assert_called_once()

    @patch.dict(os.environ, {"SOURCE_RUN_ID": "123", "GITHUB_REPOSITORY": "owner/repo"})
    @patch.object(repair, "dispatch_repair")
    @patch.object(
        repair,
        "load_agent_log",
        return_value=(
            "parity_repair_context.repair_attempt=0\n"
            "parity_repair_context.upstream_sync_pr=true\n"
            "parity_repair_context.upstream_version=1.2.3\n"
            "parity_repair_context.upstream_pr=42\n"
            "parity_repair_context.upstream_ref=automation/upstream-codex-1.2.3\n"
            "parity_repair_context.trusted_actor=maintainer\n"
        ),
    )
    @patch.object(repair, "load_source_run")
    def test_retries_upstream_agent_failure_before_validation(self, load_run, _load_log, dispatch):
        load_run.return_value = {
            "conclusion": "failure",
            "workflowName": "Codex SDK Parity Pass",
            "jobs": [{"name": "agent", "conclusion": "failure", "steps": []}],
        }

        self.assertEqual(0, repair.main())

        dispatch.assert_called_once()
        context = dispatch.call_args.kwargs["context"]
        self.assertEqual("42", context.upstream_pr)
        self.assertEqual("automation/upstream-codex-1.2.3", context.upstream_ref)
        self.assertEqual("maintainer", context.trusted_actor)

    def test_dispatch_propagates_trusted_actor_context(self):
        context = repair.RepairContext(
            attempt=0,
            upstream_sync_pr="true",
            upstream_version="1.2.3",
            upstream_pr="42",
            upstream_ref="automation/upstream-codex-1.2.3",
            trusted_actor="maintainer",
        )

        with patch.object(repair, "run_gh") as run_gh:
            repair.dispatch_repair(
                repo="owner/repo",
                workflow_file="parity.yml",
                ref="master",
                source_run_id="123",
                context=context,
                max_attempts=3,
                dry_run=False,
            )

        command = run_gh.call_args.args[0]
        self.assertIn("trusted_actor=maintainer", command)
        self.assertTrue(any('"actor":"maintainer"' in part for part in command))

    @patch.dict(os.environ, {"SOURCE_RUN_ID": "123", "GITHUB_REPOSITORY": "owner/repo"})
    @patch.object(repair, "dispatch_repair")
    @patch.object(repair, "load_agent_log", return_value="")
    @patch.object(repair, "load_source_run")
    def test_does_not_retry_unrelated_agent_failure(self, load_run, _load_log, dispatch):
        load_run.return_value = {
            "conclusion": "failure",
            "workflowName": "Codex SDK Parity Pass",
            "jobs": [{"name": "agent", "conclusion": "failure", "steps": []}],
        }

        self.assertEqual(0, repair.main())

        dispatch.assert_not_called()


if __name__ == "__main__":
    unittest.main()
