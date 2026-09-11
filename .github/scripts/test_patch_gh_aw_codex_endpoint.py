#!/usr/bin/env python3

from __future__ import annotations

import unittest

import patch_gh_aw_codex_endpoint as patcher


class EndpointEnvironmentTests(unittest.TestCase):
    def test_inserts_secret_when_env_precedes_run(self) -> None:
        lines = [
            "      - name: Execute threat detection with AWF",
            "        env:",
            "          CODEX_API_KEY: value",
            "        run: |",
            f"          {patcher.AWF_COMMAND} file --env-all --skip-pull",
        ]

        patched, count = patcher.insert_endpoint_env(lines)

        self.assertEqual(1, count)
        self.assertEqual(patcher.CODEX_ENDPOINT_ENV, patched[2])

    def test_inserts_secret_when_env_follows_run(self) -> None:
        lines = [
            "      - name: Execute Codex CLI",
            "        run: |",
            f"          {patcher.AWF_COMMAND} file --env-all --skip-pull",
            "        env:",
            "          CODEX_API_KEY: value",
        ]

        patched, count = patcher.insert_endpoint_env(lines)

        self.assertEqual(1, count)
        self.assertEqual(patcher.CODEX_ENDPOINT_ENV, patched[4])

    def test_does_not_duplicate_existing_secret(self) -> None:
        lines = [
            "      - name: Execute Codex CLI",
            "        env:",
            patcher.CODEX_ENDPOINT_ENV,
            "        run: |",
            f"          {patcher.AWF_COMMAND} file --env-all --skip-pull",
        ]

        patched, count = patcher.insert_endpoint_env(lines)

        self.assertEqual(0, count)
        self.assertEqual(lines, patched)


class CodexConfigTests(unittest.TestCase):
    def test_adds_medium_reasoning_to_agent_and_detection_configs(self) -> None:
        lines = [
            'cat > "/tmp/gh-aw/mcp-config/config.toml" << GH_AW_CODEX_SHELL_POLICY_EOF',
            'cat > "${RUNNER_TEMP}/gh-aw/mcp-config/config.toml" << GH_AW_CODEX_DETECTION_CONFIG_EOF',
        ]

        patched, count = patcher.insert_codex_reasoning_effort(lines)

        self.assertEqual(2, count)
        self.assertEqual(2, patched.count(patcher.REASONING_EFFORT_LINE))


class AWFCommandTests(unittest.TestCase):
    def test_excludes_endpoint_secret_from_agent_environment(self) -> None:
        line = f"{patcher.AWF_COMMAND} file --env-all --skip-pull"

        patched, changed = patcher.patch_awf_command(line)

        self.assertTrue(changed)
        self.assertIn("--env-all --exclude-env CODEX_LB_BASE_URL", patched)


if __name__ == "__main__":
    unittest.main()
