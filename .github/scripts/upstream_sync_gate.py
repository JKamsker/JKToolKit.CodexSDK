#!/usr/bin/env python3
"""Gate an upstream sync on CI, merge it, and verify NuGet publishing."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable


CI_WORKFLOW = "ci.yml"
REPAIR_WORKFLOW = "upstream-sync-repair.yml"
DEFAULT_BRANCH = "master"
MAX_REPAIR_ATTEMPTS = 3
POLL_SECONDS = 10
RUN_DISCOVERY_TIMEOUT_SECONDS = 300
RUN_COMPLETION_TIMEOUT_SECONDS = 2_700


@dataclass(frozen=True)
class GateContext:
    repo: str
    pr: int
    branch: str
    version: str
    attempt: int
    source_run: str
    actor: str
    release_via_dispatch: bool = False


class GitHub:
    def run(self, args: list[str]) -> str:
        result = subprocess.run(
            ["gh", *args],
            check=True,
            encoding="utf-8",
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        return result.stdout or ""

    def json(self, args: list[str]) -> Any:
        output = self.run(args)
        return json.loads(output) if output.strip() else None


def load_pr(github: GitHub, context: GateContext) -> dict[str, Any]:
    pull = github.json(
        [
            "pr",
            "view",
            str(context.pr),
            "--repo",
            context.repo,
            "--json",
            "number,state,baseRefName,headRefName,headRefOid,url",
        ]
    )
    if not isinstance(pull, dict):
        raise RuntimeError(f"Could not load upstream pull request #{context.pr}.")
    if pull.get("state") != "OPEN":
        raise RuntimeError(f"Upstream pull request #{context.pr} is not open.")
    if pull.get("baseRefName") != DEFAULT_BRANCH:
        raise RuntimeError(f"Upstream pull request #{context.pr} does not target {DEFAULT_BRANCH}.")
    if pull.get("headRefName") != context.branch:
        raise RuntimeError(
            f"Upstream pull request #{context.pr} uses {pull.get('headRefName')}, "
            f"not the expected branch {context.branch}."
        )
    return pull


def list_runs(
    github: GitHub, context: GateContext, event: str, *, branch: str | None = None,
) -> list[dict[str, Any]]:
    runs = github.json(
        [
            "run",
            "list",
            "--repo",
            context.repo,
            "--workflow",
            CI_WORKFLOW,
            "--branch",
            branch or (context.branch if event == "workflow_dispatch" else DEFAULT_BRANCH),
            "--event",
            event,
            "--limit",
            "30",
            "--json",
            "databaseId,displayTitle,headSha,status,conclusion,url",
        ]
    )
    return runs if isinstance(runs, list) else []


def wait_for_run(
    fetch: Callable[[], list[dict[str, Any]]],
    predicate: Callable[[dict[str, Any]], bool],
    timeout_seconds: int,
) -> dict[str, Any]:
    deadline = time.monotonic() + timeout_seconds
    while time.monotonic() < deadline:
        match = next((run for run in fetch() if predicate(run)), None)
        if match is not None:
            return match
        time.sleep(POLL_SECONDS)
    raise TimeoutError("Timed out waiting for the expected GitHub Actions run.")


def wait_for_completion(github: GitHub, repo: str, run: dict[str, Any]) -> dict[str, Any]:
    run_id = str(run["databaseId"])

    def fetch() -> list[dict[str, Any]]:
        current = github.json(
            [
                "run",
                "view",
                run_id,
                "--repo",
                repo,
                "--json",
                "databaseId,displayTitle,headSha,status,conclusion,url",
            ]
        )
        return [current] if isinstance(current, dict) else []

    return wait_for_run(
        fetch,
        lambda candidate: candidate.get("status") == "completed",
        RUN_COMPLETION_TIMEOUT_SECONDS,
    )


def create_issue(github: GitHub, context: GateContext, title: str, body: str) -> None:
    existing = github.json(
        [
            "issue",
            "list",
            "--repo",
            context.repo,
            "--state",
            "open",
            "--search",
            f'{title} in:title',
            "--limit",
            "1",
            "--json",
            "number,url",
        ]
    )
    if isinstance(existing, list) and existing:
        print(f"An open failure issue already exists: {existing[0].get('url')}")
        return
    github.run(["issue", "create", "--repo", context.repo, "--title", title, "--body", body])


def repair_pause_title(context: GateContext, head_sha: str) -> str:
    """Key the stop condition to both the PR code and the automation running it."""
    digest = hashlib.sha256(f"{context.repo}:{context.pr}:{head_sha}".encode())
    automation = Path(__file__).resolve().parents[1]
    paths = [
        *sorted((automation / "scripts").glob("*.py")),
        *(automation / "workflows" / name for name in (
            "ci.yml", "upstream-sync.yml", "upstream-sync-repair.yml",
            "codex-sdk-parity-pass.md", "codex-sdk-parity-pass.lock.yml",
        )),
    ]
    for path in paths:
        digest.update(str(path.relative_to(automation)).encode())
        digest.update(path.read_bytes())
    return f"Upstream sync {context.version} paused [{digest.hexdigest()[:16]}]"


def can_resume(github: GitHub, context: GateContext) -> bool:
    pull = load_pr(github, context)
    title = repair_pause_title(context, str(pull["headRefOid"]))
    issues = github.json([
        "issue", "list", "--repo", context.repo, "--state", "open",
        "--search", f'"{title}" in:title', "--limit", "100", "--json", "title,url",
    ])
    paused = next((issue for issue in issues or [] if issue.get("title") == title), None)
    if paused:
        print(f"::notice::Automatic repairs are paused for this code and automation: {paused.get('url')}")
        return False

    runs = github.json([
        "run", "list", "--repo", context.repo, "--workflow", REPAIR_WORKFLOW,
        "--branch", DEFAULT_BRANCH, "--limit", "100", "--json", "displayTitle,status,url",
    ])
    prefix = f"Upstream Sync Repair PR #{context.pr} attempt "
    active = next((run for run in runs or []
                   if str(run.get("displayTitle", "")).startswith(prefix)
                   and run.get("status") != "completed"), None)
    if active:
        print(f"::notice::An upstream repair is already active: {active.get('url')}")
        return False
    return True


def schedule_repair(
    github: GitHub,
    context: GateContext,
    source_run: str,
    source_job: str,
) -> bool:
    next_attempt = context.attempt + 1
    if next_attempt > MAX_REPAIR_ATTEMPTS:
        pull = load_pr(github, context)
        head_sha = str(pull["headRefOid"])
        title = repair_pause_title(context, head_sha)
        create_issue(
            github,
            context,
            title,
            f"PR #{context.pr} could not be validated after {MAX_REPAIR_ATTEMPTS} repair attempts.\n\n"
            f"Last source run: https://github.com/{context.repo}/actions/runs/{source_run}\n"
            f"Branch: `{context.branch}`\nHead: `{head_sha}`\n\n"
            "Scheduled syncs will not restart this exhausted repair chain. "
            "Push a fix to the PR, update the automation, or close this issue after "
            "resolving an external blocker to allow a new attempt.",
        )
        return False

    aw_context = json.dumps(
        {
            "command_name": "upstream-sync-repair",
            "actor": context.actor,
            "item_type": "pull_request",
            "item_number": str(context.pr),
        },
        separators=(",", ":"),
    )

    github.run(
        [
            "workflow",
            "run",
            REPAIR_WORKFLOW,
            "--repo",
            context.repo,
            "--ref",
            DEFAULT_BRANCH,
            "-f",
            f"upstream_version={context.version}",
            "-f",
            f"upstream_pr={context.pr}",
            "-f",
            f"upstream_ref={context.branch}",
            "-f",
            f"repair_attempt={next_attempt}",
            "-f",
            f"repair_source_run={source_run}",
            "-f",
            f"repair_source_job={source_job}",
            "-f",
            f"trusted_actor={context.actor}",
            "-f",
            f"aw_context={aw_context}",
        ]
    )
    print(f"Dispatched automatic repair attempt {next_attempt}/{MAX_REPAIR_ATTEMPTS}.")
    return True


def dispatch_gated_ci(github: GitHub, context: GateContext, head_sha: str) -> dict[str, Any]:
    existing_ids = {
        run.get("databaseId")
        for run in list_runs(github, context, "workflow_dispatch")
        if run.get("headSha") == head_sha
    }
    github.run(
        [
            "workflow",
            "run",
            CI_WORKFLOW,
            "--repo",
            context.repo,
            "--ref",
            context.branch,
        ]
    )
    discovered = wait_for_run(
        lambda: list_runs(github, context, "workflow_dispatch"),
        lambda run: run.get("headSha") == head_sha and run.get("databaseId") not in existing_ids,
        RUN_DISCOVERY_TIMEOUT_SECONDS,
    )
    print(f"Waiting for gated CI: {discovered.get('url')}")
    return wait_for_completion(github, context.repo, discovered)


def merge_exact_head(github: GitHub, context: GateContext, head_sha: str) -> str:
    current = load_pr(github, context)
    if current.get("headRefOid") != head_sha:
        raise RuntimeError("The pull request head changed after CI; refusing to merge an untested commit.")
    result = github.json(
        [
            "api",
            "--method",
            "PUT",
            f"repos/{context.repo}/pulls/{context.pr}/merge",
            "--raw-field",
            "merge_method=merge",
            "--raw-field",
            f"sha={head_sha}",
        ]
    )
    if not isinstance(result, dict) or not result.get("merged") or not result.get("sha"):
        message = result.get("message") if isinstance(result, dict) else "unknown response"
        raise RuntimeError(f"GitHub refused to merge pull request #{context.pr}: {message}")
    return str(result["sha"])


def verify_release(github: GitHub, context: GateContext, merge_sha: str) -> bool:
    if context.release_via_dispatch:
        # GITHUB_TOKEN merges do not trigger push workflows. Dispatch on the
        # default branch, but build the exact merge even if that branch advances.
        fetch = lambda: list_runs(github, context, "workflow_dispatch", branch=DEFAULT_BRANCH)
        existing_ids = {run.get("databaseId") for run in fetch()}
        github.run([
            "workflow", "run", CI_WORKFLOW, "--repo", context.repo,
            "--ref", DEFAULT_BRANCH, "-f", "publish=true", "-f", f"release_sha={merge_sha}",
        ])
        release_run = wait_for_run(
            fetch,
            lambda run: run.get("displayTitle") == f"Release upstream merge {merge_sha}"
            and run.get("databaseId") not in existing_ids,
            RUN_DISCOVERY_TIMEOUT_SECONDS,
        )
    else:
        release_run = wait_for_run(
            lambda: list_runs(github, context, "push"),
            lambda run: run.get("headSha") == merge_sha,
            RUN_DISCOVERY_TIMEOUT_SECONDS,
        )
    print(f"Waiting for post-merge NuGet release CI: {release_run.get('url')}")
    completed = wait_for_completion(github, context.repo, release_run)
    jobs = github.json([
        "run", "view", str(completed["databaseId"]), "--repo", context.repo, "--json", "jobs",
    ])
    published = any(
        job.get("name") == "NugetUpload" and job.get("conclusion") == "success"
        for job in (jobs or {}).get("jobs", [])
    )
    if completed.get("conclusion") == "success" and published:
        print(f"NuGet release CI succeeded: {completed.get('url')}")
        return True

    create_issue(
        github,
        context,
        f"NuGet release failed for upstream Codex {context.version}",
        f"The tested upstream sync was merged as `{merge_sha}`, but its post-merge release failed.\n\n"
        f"Run: {completed.get('url')}\n"
        "The workflow must succeed and its NugetUpload job must complete successfully.",
    )
    return False


def run_gate(github: GitHub, context: GateContext) -> int:
    pull = load_pr(github, context)
    head_sha = str(pull["headRefOid"])
    completed = dispatch_gated_ci(github, context, head_sha)
    if completed.get("conclusion") != "success":
        scheduled = schedule_repair(
            github,
            context,
            str(completed["databaseId"]),
            "ci",
        )
        return 0 if scheduled else 1

    merge_sha = merge_exact_head(github, context, head_sha)
    print(f"Merged PR #{context.pr} into {DEFAULT_BRANCH} as {merge_sha}.")
    return 0 if verify_release(github, context, merge_sha) else 1


def parser() -> argparse.ArgumentParser:
    result = argparse.ArgumentParser()
    subparsers = result.add_subparsers(dest="command", required=True)
    for name in ("gate", "schedule-repair", "can-resume"):
        command = subparsers.add_parser(name)
        command.add_argument("--repo", required=True)
        command.add_argument("--pr", required=True, type=int)
        command.add_argument("--branch", required=True)
        command.add_argument("--version", required=True)
        command.add_argument("--attempt", required=True, type=int)
        command.add_argument("--actor", required=True)
        command.add_argument("--source-run", required=True)
        if name == "schedule-repair":
            command.add_argument("--source-job", required=True)
    return result


def main() -> int:
    args = parser().parse_args()
    context = GateContext(
        args.repo, args.pr, args.branch, args.version, args.attempt, args.source_run, args.actor,
        release_via_dispatch=os.environ.get("UPSTREAM_USE_GITHUB_TOKEN") == "true",
    )
    github = GitHub()
    try:
        if args.command == "can-resume":
            allowed = can_resume(github, context)
            with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
                output.write(f"proceed={str(allowed).lower()}\n")
            return 0
        if args.command == "gate":
            return run_gate(github, context)
        return 0 if schedule_repair(github, context, args.source_run, args.source_job) else 1
    except Exception as error:
        create_issue(
            github,
            context,
            f"Upstream sync automation failed for Codex {context.version}",
            f"The upstream automation stopped before it could safely complete PR #{context.pr}.\n\n"
            f"Source run: https://github.com/{context.repo}/actions/runs/{context.source_run}\n"
            f"Error: `{error}`",
        )
        print(f"Upstream sync automation failed: {error}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
