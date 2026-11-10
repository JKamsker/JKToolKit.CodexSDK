#!/usr/bin/env python3

from __future__ import annotations

import unittest
import json
import os
from pathlib import Path
import subprocess
import tempfile

import patch_gh_aw_codex_endpoint as patcher


class RuntimePricingTests(unittest.TestCase):
    def test_runtime_patch_preserves_budget_and_explicit_pricing(self) -> None:
        for explicit in (None, {"input": 3, "output": 15}):
            with self.subTest(explicit=explicit), tempfile.TemporaryDirectory() as tmp:
                config_path = Path(tmp) / "gh-aw" / "awf-config.json"
                config_path.parent.mkdir()
                proxy = {"maxAiCredits": 2000}
                if explicit is not None:
                    proxy["defaultAiCreditsPricing"] = explicit
                config_path.write_text(json.dumps({"apiProxy": proxy}))
                subprocess.run(
                    ["bash", "-c", "\n".join(patcher.PATCH_SNIPPET)],
                    env={**os.environ, "RUNNER_TEMP": tmp,
                         "CODEX_LB_BASE_URL": "https://example.invalid/v1"},
                    check=True, capture_output=True, text=True,
                )
                config = json.loads(config_path.read_text())
                self.assertEqual(2000, config["apiProxy"]["maxAiCredits"])
                self.assertEqual(explicit or {"input": 10, "output": 50},
                                 config["apiProxy"]["defaultAiCreditsPricing"])
                self.assertEqual({"host": "example.invalid", "basePath": "/v1"},
                                 config["apiProxy"]["targets"]["openai"])

    def test_updates_existing_runtime_patch_and_is_idempotent(self) -> None:
        write = f'          printf awf-config.schema.json {patcher.CONFIG_WRITE}'
        old_snippet = [line for line in patcher.PATCH_SNIPPET
                       if 'defaultAiCreditsPricing' not in line]
        lines = [write, *[f"          {line}" if line else "" for line in old_snippet], "next step"]
        patched, count = patcher.insert_runtime_patch(lines)
        self.assertEqual(1, count)
        self.assertEqual(1, sum('defaultAiCreditsPricing' in line for line in patched))
        self.assertEqual("next step", patched[-1])
        self.assertEqual((patched, 0), patcher.insert_runtime_patch(patched))


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


class ReasoningOverrideTests(unittest.TestCase):
    def test_replaces_existing_effort_without_duplicate_override(self):
        lines = ['cat > config.toml << GH_AW_CODEX_SHELL_POLICY_EOF',
                 '  model_reasoning_effort = "high"']
        patched, _ = patcher.insert_codex_reasoning_effort(lines)
        self.assertEqual(1, sum('model_reasoning_effort =' in line for line in patched))
        self.assertIn('  ' + patcher.REASONING_EFFORT_LINE, patched)
        self.assertEqual((patched, 0), patcher.insert_codex_reasoning_effort(patched))


class AWFCommandTests(unittest.TestCase):
    def test_excludes_endpoint_secret_from_agent_environment(self) -> None:
        line = f"{patcher.AWF_COMMAND} file --env-all --skip-pull"

        patched, changed = patcher.patch_awf_command(line)

        self.assertTrue(changed)
        self.assertIn("--env-all --exclude-env CODEX_LB_BASE_URL", patched)


if __name__ == "__main__":
    unittest.main()
