#!/usr/bin/env python3
"""Keep SDK package versions on the pinned Codex CLI release line."""

import argparse
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET


def parse_version(value: str) -> tuple[int, int, int]:
    if not re.fullmatch(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)", value):
        raise ValueError(f"Expected a stable major.minor.patch version, got {value!r}")
    return tuple(map(int, value.split(".")))


def update_version(root: Path, *, check: bool = False, bump_patch: bool = False) -> str:
    upstream = json.loads((root / "UPSTREAM_CODEX_VERSION.json").read_text(encoding="utf-8"))["api"]
    target = parse_version(upstream)
    path = root / "Directory.Build.props"
    text = path.read_text(encoding="utf-8")
    props = ET.fromstring(text)

    def property_value(name: str) -> str:
        elements = props.findall(f"./PropertyGroup/{name}")
        if len(elements) != 1 or not elements[0].text:
            raise ValueError(f"Directory.Build.props must contain exactly one {name}")
        return elements[0].text

    baseline = property_value("CodexCliVersion")
    parse_version(baseline)
    version = property_value("VersionPrefix")
    current = parse_version(version)
    aligned = baseline == upstream and current[:2] == target[:2] and current >= target
    if check:
        if not aligned:
            raise ValueError("Package version is out of sync; run python scripts/sync-package-version.py")
        return version
    if bump_patch:
        if not aligned:
            raise ValueError("Sync the package version before bumping its patch")
        desired = (*current[:2], current[2] + 1)
    elif aligned:
        return version
    elif baseline != upstream and current[:2] == target[:2] and current >= target:
        # Upstream patches can collide with an already released SDK-only patch.
        desired = (*current[:2], current[2] + 1)
    else:
        desired = target

    version = ".".join(map(str, desired))
    for name, value in (("CodexCliVersion", upstream), ("VersionPrefix", version)):
        text, count = re.subn(rf"<{name}>[^<]*</{name}>", f"<{name}>{value}</{name}>", text)
        if count != 1:
            raise ValueError(f"Cannot update {name} in Directory.Build.props")
    path.write_text(text, encoding="utf-8")
    return version


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true", help="Validate without changing files; print the package version")
    mode.add_argument("--bump-patch", action="store_true", help="Increment the patch for an SDK-only release")
    args = parser.parse_args()
    try:
        print(update_version(Path(__file__).resolve().parents[1], check=args.check, bump_patch=args.bump_patch))
    except (ValueError, KeyError, OSError, ET.ParseError) as exc:
        print(str(exc), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
