"""Execute workflow shell guards without contacting GitHub or publishing packages."""

from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import textwrap
import unittest


WORKFLOWS = Path(__file__).resolve().parents[1] / "workflows"


def step_script(workflow: str, name: str, key: str = "run") -> str:
    """Read a literal step script; fail if the workflow changes its representation."""
    text = (WORKFLOWS / workflow).read_text()
    step = text.split(f"      - name: {name}\n", 1)[1].split("\n      - name:", 1)[0]
    match = re.search(rf"^\s+{key}: \|\n((?:[ ]+[^\n]*\n|\n)+)", step, re.MULTILINE)
    if match is None:
        raise AssertionError(f"No literal {key} script in {name}")
    # The next job/step is less indented than the script.
    lines = match[1].splitlines()
    indent = len(lines[0]) - len(lines[0].lstrip())
    body = []
    for line in lines:
        if line.strip() and len(line) - len(line.lstrip()) < indent:
            break
        body.append(line)
    return textwrap.dedent("\n".join(body))


class WorkflowShellTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.output = self.root / "output"

    def run_script(self, script: str, **env: str) -> subprocess.CompletedProcess[str]:
        self.output.write_text("")
        return subprocess.run(
            ["bash", "-c", script], cwd=self.root,
            env={**os.environ, "GITHUB_OUTPUT": str(self.output), **env},
            capture_output=True, text=True,
        )

    def git(self, *args: str) -> str:
        return subprocess.check_output(["git", *args], cwd=self.root, text=True).strip()

    def test_release_target_guard(self) -> None:
        self.git("init", "-q", "-b", "master")
        self.git("-c", "user.name=Test", "-c", "user.email=test@example.org", "commit", "-qm", "first", "--allow-empty")
        merged = self.git("rev-parse", "HEAD")
        self.git("checkout", "-qb", "unmerged")
        self.git("-c", "user.name=Test", "-c", "user.email=test@example.org", "commit", "-qm", "unmerged", "--allow-empty")
        unmerged = self.git("rev-parse", "HEAD")
        self.git("checkout", "-q", "master")
        self.git("-c", "user.name=Test", "-c", "user.email=test@example.org", "commit", "-qm", "advanced", "--allow-empty")
        current = self.git("rev-parse", "HEAD")
        script = step_script("ci.yml", "Validate checkout and release target")
        base = dict(
            GITHUB_SHA=current, GITHUB_EVENT_NAME="workflow_dispatch", GITHUB_REF="refs/heads/master",
            DEFAULT_BRANCH="master", PUBLISH="true", RELEASE_SHA=merged,
        )
        scenarios = [
            ({}, True, merged),
            ({"RELEASE_SHA": current}, True, current),
            ({"PUBLISH": "false", "RELEASE_SHA": ""}, True, current),
            ({"PUBLISH": "false", "RELEASE_SHA": "", "GITHUB_EVENT_NAME": "pull_request"}, True, current),
            ({"PUBLISH": "false"}, False, None),
            ({"GITHUB_REF": "refs/heads/feature"}, False, None),
            ({"GITHUB_EVENT_NAME": "pull_request"}, False, None),
            ({"RELEASE_SHA": ""}, False, None),
            ({"RELEASE_SHA": "HEAD"}, False, None),
            ({"RELEASE_SHA": "0" * 40}, False, None),
            ({"RELEASE_SHA": unmerged}, False, None),
        ]
        for overrides, success, target in scenarios:
            with self.subTest(overrides=overrides):
                result = self.run_script(script, **{**base, **overrides})
                self.assertEqual(success, result.returncode == 0, result.stdout + result.stderr)
                self.assertEqual(f"checkout_ref={target}\n" if success else "", self.output.read_text())

    def test_missing_tag_defers_but_transport_failure_fails(self) -> None:
        stub = self.root / "git"
        stub.write_text('#!/bin/sh\nprintf "%s" "$TAG_RESULT"\nexit "$TAG_EXIT"\n')
        stub.chmod(0o755)
        script = step_script("upstream-sync.yml", "Check upstream tag availability")
        for tag_result, tag_exit, expected in [("", "0", "false"), ("abc refs/tags/rust-v1.2.3", "0", "true"), ("", "128", None)]:
            with self.subTest(tag_exit=tag_exit, tag_result=tag_result):
                result = self.run_script(
                    script, PATH=f"{self.root}:{os.environ['PATH']}", LATEST_CODEX_VERSION="1.2.3",
                    TAG_RESULT=tag_result, TAG_EXIT=tag_exit,
                )
                if expected is None:
                    self.assertNotEqual(0, result.returncode)
                    self.assertEqual("", self.output.read_text())
                else:
                    self.assertEqual(0, result.returncode, result.stderr)
                    self.assertEqual(f"ready={expected}\n", self.output.read_text())

    def test_bootstrap_changes_api_marker_and_leaves_integration_baseline(self) -> None:
        stub = self.root / "git"
        stub.write_text('#!/bin/sh\nexit 0\n')
        stub.chmod(0o755)
        marker = self.root / "UPSTREAM_CODEX_VERSION.json"
        marker.write_text(json.dumps({"api": "1.0.0", "integration": "1.0.0"}))
        script = step_script("upstream-sync.yml", "Update pin + submodule")
        result = self.run_script(script, PATH=f"{self.root}:{os.environ['PATH']}", LATEST_CODEX_VERSION="1.2.3")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual({"api": "1.2.3", "integration": "1.0.0"}, json.loads(marker.read_text()))
        bootstrap = (WORKFLOWS / "upstream-sync.yml").read_text().split("\n  parity:")[0]
        self.assertLess(bootstrap.index("- name: Regenerate upstream DTOs"), bootstrap.index("- name: Create pull request"))
        self.assertNotIn("Verify user automation token", bootstrap)

    def test_dto_generation_is_deterministic_and_cleans_partial_output_on_failure(self) -> None:
        command_log = self.root / "commands.log"
        dotnet = self.root / "dotnet"
        dotnet.write_text(textwrap.dedent("""\
            #!/bin/sh
            printf 'dotnet %s\\n' "$*" >> "$COMMAND_LOG"
            if [ "${FAIL_GENERATE:-0}" = 1 ] && echo "$*" | grep -q -- ' generate$'; then
              exit 1
            fi
            exit 0
            """))
        dotnet.chmod(0o755)
        git = self.root / "git"
        git.write_text(textwrap.dedent("""\
            #!/bin/sh
            printf 'git %s\\n' "$*" >> "$COMMAND_LOG"
            exit 0
            """))
        git.chmod(0o755)
        script = step_script("upstream-sync.yml", "Regenerate upstream DTOs")
        environment = {
            "PATH": f"{self.root}:{os.environ['PATH']}",
            "COMMAND_LOG": str(command_log),
        }

        success = self.run_script(script, **environment)
        self.assertEqual(0, success.returncode, success.stderr)
        self.assertEqual("ready=true\n", self.output.read_text())
        success_commands = command_log.read_text()
        self.assertIn("dotnet restore src/JKToolKit.CodexSDK.UpstreamGen", success_commands)
        self.assertIn("-- generate", success_commands)
        self.assertIn("-- check", success_commands)
        self.assertNotIn("git restore", success_commands)

        command_log.write_text("")
        failure = self.run_script(script, **environment, FAIL_GENERATE="1")
        self.assertEqual(0, failure.returncode, failure.stderr)
        self.assertEqual("ready=false\n", self.output.read_text())
        failure_commands = command_log.read_text()
        self.assertIn("git restore --source=HEAD --staged --worktree", failure_commands)
        self.assertIn("git clean -fd --", failure_commands)


class ExistingPullTests(unittest.TestCase):
    def test_resume_only_expected_same_repo_branch(self) -> None:
        script = step_script("upstream-sync.yml", "Check for an existing upstream PR", "script")
        valid = {"number": 42, "head": {"ref": "automation/upstream-codex-1.2.3", "repo": {"full_name": "owner/repo"}}}
        cases = [
            ([valid], "true"),
            ([{**valid, "head": {**valid["head"], "repo": {"full_name": "fork/repo"}}}], "false"),
            ([], "false"),
        ]
        for pulls, exists in cases:
            with self.subTest(exists=exists):
                harness = f'''
const outputs = {{}};
const core = {{setOutput: (k, v) => outputs[k] = v, notice: () => {{}}}};
const context = {{repo: {{owner: 'owner', repo: 'repo'}}}};
const github = {{rest: {{pulls: {{list: 'pulls'}}}}, paginate: async () => {json.dumps(pulls)}}};
(async () => {{ {script} }})().then(() => console.log(JSON.stringify(outputs)));
'''
                result = subprocess.run(
                    ["node", "-e", harness], env={**os.environ, "UPSTREAM_VERSION": "1.2.3"},
                    capture_output=True, text=True, check=True,
                )
                outputs = json.loads(result.stdout)
                self.assertEqual(exists, outputs["exists"])


if __name__ == "__main__":
    unittest.main()
