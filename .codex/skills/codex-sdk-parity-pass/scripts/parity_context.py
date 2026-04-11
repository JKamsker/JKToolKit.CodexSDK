#!/usr/bin/env python3
"""Print a compact snapshot of the repo's current Codex parity state."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path


def run_git(args: list[str], cwd: Path) -> str | None:
    result = subprocess.run(
        ["git", *args],
        cwd=str(cwd),
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        return None
    output = result.stdout.strip()
    return output or None


def read_first_non_comment_line(path: Path) -> str | None:
    if not path.is_file():
        return None

    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        return line

    return None


def find_repo_root(start: Path) -> Path:
    candidate = start.resolve()
    if candidate.is_file():
        candidate = candidate.parent

    for current in (candidate, *candidate.parents):
        if (current / ".git").exists() and (current / "UPSTREAM_CODEX_VERSION.txt").exists():
            return current

    raise FileNotFoundError(
        "Could not find the repo root from the provided path. Expected .git and "
        "UPSTREAM_CODEX_VERSION.txt in the same directory."
    )


def collect_docs(root: Path) -> list[dict[str, str]]:
    docs = []
    for path in sorted((root / "docs").glob("codex-*-interop.md")):
        stat = path.stat()
        docs.append(
            {
                "path": path.relative_to(root).as_posix(),
                "lastModifiedUtc": datetime.fromtimestamp(
                    stat.st_mtime, tz=timezone.utc
                ).isoformat(),
            }
        )

    docs.sort(key=lambda item: item["lastModifiedUtc"], reverse=True)
    return docs


def read_package_version(path: Path) -> str | None:
    if not path.is_file():
        return None

    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None

    version = payload.get("version")
    return version if isinstance(version, str) and version else None


def main() -> int:
    parser = argparse.ArgumentParser(description="Print the current Codex parity context for this repo.")
    parser.add_argument(
        "--repo-root",
        help="Optional explicit repository root. Defaults to auto-detection from the current working directory.",
    )
    args = parser.parse_args()

    start = Path(args.repo_root) if args.repo_root else Path.cwd()
    repo_root = find_repo_root(start)

    external_codex = repo_root / "external" / "codex"
    package_json = external_codex / "codex-cli" / "package.json"
    version_pin = repo_root / "UPSTREAM_CODEX_VERSION.txt"

    context = {
        "repoRoot": str(repo_root),
        "branch": run_git(["rev-parse", "--abbrev-ref", "HEAD"], repo_root),
        "pinnedVersion": read_first_non_comment_line(version_pin),
        "externalCodex": {
            "path": str(external_codex),
            "headSha": run_git(["rev-parse", "HEAD"], external_codex),
            "describe": run_git(["describe", "--tags", "--always", "--dirty"], external_codex),
            "packageJsonPath": str(package_json),
            "packageJsonVersion": read_package_version(package_json),
        },
        "interopDocs": collect_docs(repo_root),
        "keyDocs": [
            "docs/upstreamgen.md",
            "docs/codex-0.106-to-0.118-interop.md",
            "docs/Runbooks/Manual-Testing/Exec.md",
            "docs/Runbooks/Manual-Testing/AppServer.md",
        ],
        "skillReferences": [
            ".codex/skills/codex-sdk-parity-pass/references/last-parity-pass.md",
            ".codex/skills/codex-sdk-parity-pass/references/repo-map.md",
        ],
    }

    json.dump(context, sys.stdout, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
