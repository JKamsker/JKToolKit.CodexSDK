#!/usr/bin/env python3

from __future__ import annotations

import unittest

import compile_gh_aw


class CompileArgsTests(unittest.TestCase):
    def test_defaults_to_pinned_upstream_actions_for_fork_compiler(self) -> None:
        self.assertEqual(
            [*compile_gh_aw.PINNED_ACTION_ARGS, "daily-repo-status"],
            compile_gh_aw.compile_args(["daily-repo-status"]),
        )

    def test_preserves_explicit_action_selection(self) -> None:
        args = ["--gh-aw-ref", "main", "daily-repo-status"]

        self.assertEqual(args, compile_gh_aw.compile_args(args))

    def test_preserves_equals_style_action_selection(self) -> None:
        args = ["--action-tag=v0.88.7", "daily-repo-status"]

        self.assertEqual(args, compile_gh_aw.compile_args(args))


class SelectedLockfilesTests(unittest.TestCase):
    def test_ignores_action_selection_values(self) -> None:
        selected = compile_gh_aw.selected_lockfiles(
            [
                "--action-mode",
                "action",
                "--action-tag",
                "v0.88.7",
                "--actions-repo",
                "github/gh-aw-actions",
                "daily-repo-status",
            ]
        )

        self.assertEqual(
            [compile_gh_aw.WORKFLOW_DIR / "daily-repo-status.lock.yml"],
            selected,
        )

    def test_no_emit_selects_no_lockfiles(self) -> None:
        self.assertEqual([], compile_gh_aw.selected_lockfiles(["--no-emit"]))


if __name__ == "__main__":
    unittest.main()
