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


if __name__ == "__main__":
    unittest.main()
