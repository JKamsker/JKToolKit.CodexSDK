#!/usr/bin/env python3
"""Compile gh-aw workflows and apply repository-specific lockfile patches."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW_DIR = REPO_ROOT / ".github" / "workflows"
PINNED_ACTION_ARGS = [
    "--action-mode",
    "action",
    "--action-tag",
    "v0.88.7",
    "--actions-repo",
    "github/gh-aw-actions",
]
ACTION_SELECTION_FLAGS = {"--action-mode", "--action-tag", "--actions-repo", "--gh-aw-ref"}


def compile_args(args: list[str]) -> list[str]:
    has_explicit_action_selection = any(
        arg in ACTION_SELECTION_FLAGS
        or any(arg.startswith(f"{flag}=") for flag in ACTION_SELECTION_FLAGS)
        for arg in args
    )
    if has_explicit_action_selection:
        return args
    return [*PINNED_ACTION_ARGS, *args]


def selected_lockfiles(args: list[str]) -> list[Path]:
    if "--no-emit" in args:
        return []

    selected: list[Path] = []
    skip_next = False
    for arg in args:
        if skip_next:
            skip_next = False
            continue
        if arg in {
            "--dir",
            "-d",
            "--engine",
            "-e",
            "--logical-repo",
            "--schedule-seed",
            *ACTION_SELECTION_FLAGS,
        }:
            skip_next = True
            continue
        if arg.startswith("-"):
            continue

        path = Path(arg)
        if path.suffix == ".md":
            md_path = path if path.is_absolute() else REPO_ROOT / path
            selected.append(md_path.with_suffix(".lock.yml"))
        elif "/" not in arg and "\\" not in arg:
            selected.append(WORKFLOW_DIR / f"{arg}.lock.yml")

    if selected:
        return selected
    return sorted(WORKFLOW_DIR.glob("*.lock.yml"))


def main() -> int:
    args = sys.argv[1:]
    subprocess.run(["gh", "aw", "compile", *compile_args(args)], cwd=REPO_ROOT, check=True)

    lockfiles = selected_lockfiles(args)
    if not lockfiles:
        return 0

    patcher = Path(__file__).with_name("patch_gh_aw_codex_endpoint.py")
    subprocess.run(
        [sys.executable, str(patcher), *(str(path) for path in lockfiles)],
        cwd=REPO_ROOT,
        check=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
