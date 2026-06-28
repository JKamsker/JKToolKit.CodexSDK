#!/usr/bin/env python3
"""Dispatch a bounded repair run after parity validation-gate failures."""

from __future__ import annotations

import json
import os
import re
import subprocess
import sys
from dataclasses import dataclass
from typing import Any


VALIDATION_STEP_NAMES = {
    "Setup .NET for parity validation",
    "Materialize safe-output patch for validation",
    "Restore parity validation dependencies",
    "Verify generated DTOs before safe output",
    "Build before safe output",
    "Test before safe output",
}


@dataclass(frozen=True)
class RepairContext:
    attempt: int
    upstream_sync_pr: str
    upstream_version: str
    upstream_pr: str


def run_gh(args: list[str], *, capture: bool = True) -> str:
    command = ["gh", *args]
    result = subprocess.run(
        command,
        check=True,
        encoding="utf-8",
        stdout=subprocess.PIPE if capture else None,
        stderr=subprocess.PIPE if capture else None,
    )
    return result.stdout or ""


def load_source_run(source_run_id: str) -> dict[str, Any]:
    output = run_gh(
        [
            "run",
            "view",
            source_run_id,
            "--json",
            "status,conclusion,event,headBranch,headSha,url,jobs",
        ]
    )
    return json.loads(output)


def find_agent_job(run: dict[str, Any]) -> dict[str, Any] | None:
    for job in run.get("jobs") or []:
        if job.get("name") == "agent":
            return job
    return None


def failed_validation_steps(agent_job: dict[str, Any]) -> list[str]:
    steps = agent_job.get("steps") or []
    guard_completed = any(
        step.get("name") == "Check whether parity validation is required"
        and step.get("conclusion") == "success"
        for step in steps
    )
    if not guard_completed:
        return []

    return [
        step.get("name", "")
        for step in steps
        if step.get("name") in VALIDATION_STEP_NAMES and step.get("conclusion") == "failure"
    ]


def load_agent_log(source_run_id: str, agent_job: dict[str, Any]) -> str:
    job_id = str(agent_job.get("databaseId") or "")
    if not job_id:
        return ""

    try:
        return run_gh(["run", "view", source_run_id, "--job", job_id, "--log"])
    except subprocess.CalledProcessError as exc:
        print(f"::warning::Could not read agent job log: {exc.stderr or exc}", file=sys.stderr)
        return ""


def parse_repair_context(log_text: str) -> RepairContext:
    values: dict[str, str] = {}
    for match in re.finditer(r"parity_repair_context\.([a-z_]+)=([^\r\n]*)", log_text):
        values[match.group(1)] = match.group(2).strip()

    attempt_raw = values.get("repair_attempt", "0")
    attempt = int(attempt_raw) if attempt_raw.isdigit() else 0
    return RepairContext(
        attempt=attempt,
        upstream_sync_pr=values.get("upstream_sync_pr") or "false",
        upstream_version=values.get("upstream_version") or "",
        upstream_pr=values.get("upstream_pr") or "",
    )


def dispatch_repair(
    *,
    repo: str,
    workflow_file: str,
    ref: str,
    source_run_id: str,
    context: RepairContext,
    max_attempts: int,
    dry_run: bool,
) -> None:
    if context.attempt >= max_attempts:
        print(
            f"Repair attempt limit reached ({context.attempt}/{max_attempts}); "
            "not dispatching another parity repair run."
        )
        return

    next_attempt = context.attempt + 1
    args = [
        "workflow",
        "run",
        workflow_file,
        "--repo",
        repo,
        "--ref",
        ref,
        "-f",
        f"upstream_sync_pr={context.upstream_sync_pr}",
        "-f",
        f"upstream_version={context.upstream_version}",
        "-f",
        f"upstream_pr={context.upstream_pr}",
        "-f",
        f"repair_attempt={next_attempt}",
        "-f",
        f"repair_source_run={source_run_id}",
        "-f",
        "repair_source_job=agent",
    ]

    print(f"Dispatching parity repair attempt {next_attempt}/{max_attempts} from run {source_run_id}.")
    if dry_run:
        print("Dry run command: gh " + " ".join(args))
        return

    run_gh(args, capture=False)


def main() -> int:
    source_run_id = os.environ["SOURCE_RUN_ID"]
    repo = os.environ["GITHUB_REPOSITORY"]
    default_branch = os.environ.get("DEFAULT_BRANCH") or "master"
    workflow_file = os.environ.get("PARITY_WORKFLOW_FILE") or "codex-sdk-parity-pass.lock.yml"
    max_attempts = int(os.environ.get("MAX_REPAIR_ATTEMPTS") or "3")
    dry_run = os.environ.get("DRY_RUN") == "1"

    source_run = load_source_run(source_run_id)
    if source_run.get("conclusion") != "failure":
        print(f"Source run conclusion is {source_run.get('conclusion')}; no repair dispatch needed.")
        return 0

    agent_job = find_agent_job(source_run)
    if not agent_job:
        print("Source run has no agent job; no repair dispatch needed.")
        return 0

    failures = failed_validation_steps(agent_job)
    if not failures:
        print("Agent job did not fail in the parity validation gate; no repair dispatch needed.")
        return 0

    print("Validation-gate failure detected:")
    for failure in failures:
        print(f"- {failure}")

    context = parse_repair_context(load_agent_log(source_run_id, agent_job))
    dispatch_repair(
        repo=repo,
        workflow_file=workflow_file,
        ref=default_branch,
        source_run_id=source_run_id,
        context=context,
        max_attempts=max_attempts,
        dry_run=dry_run,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
