#!/usr/bin/env python3
"""Reject conflicting NuGet version reuse before publishing any SDK package."""

import argparse
from io import BytesIO
from pathlib import Path
import re
import sys
from urllib.error import HTTPError
from urllib.request import urlopen
import xml.etree.ElementTree as ET
from zipfile import ZipFile


SDK_PACKAGES = (
    "JKToolKit.CodexSDK",
    "JKToolKit.CodexSDK.SemanticKernel",
    "JKToolKit.CodexSDK.AgentFramework",
)


def package_contents(data: bytes, package_id: str, version: str) -> dict[str, bytes]:
    """Validate the identity and compare ZIP entries independently of signing/timestamps."""
    with ZipFile(BytesIO(data)) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise ValueError(f"Duplicate ZIP entries in {package_id} {version}")
        nuspecs = [name for name in names if name.endswith(".nuspec")]
        if len(nuspecs) != 1:
            raise ValueError(f"Expected one nuspec in {package_id} {version}")
        metadata = ET.fromstring(archive.read(nuspecs[0])).find("{*}metadata")
        if metadata is None or (
            metadata.findtext("{*}id") != package_id
            or metadata.findtext("{*}version") != version
        ):
            raise ValueError(f"Package identity does not match {package_id} {version}")
        # NuGet.org adds a repository signature; every other file must match.
        return {name: archive.read(name) for name in names if name != ".signature.p7s"}


def published_package(package_id: str, version: str) -> bytes | None:
    """Fetch the published archive; only an HTTP 404 means it is unpublished."""
    package = package_id.lower()
    url = f"https://api.nuget.org/v3-flatcontainer/{package}/{version}/{package}.{version}.nupkg"
    try:
        with urlopen(url, timeout=60) as response:
            return response.read()
    except HTTPError as error:
        if error.code == 404:
            return None
        raise


def plan_release(package_dir: Path, version: str) -> list[Path]:
    """Check all three SDK identities before returning the unpublished package paths."""
    if not re.fullmatch(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)", version):
        raise ValueError("Expected a stable major.minor.patch SDK version")
    pending = []
    for package_id in SDK_PACKAGES:
        path = package_dir / f"{package_id}.{version}.nupkg"
        local = package_contents(path.read_bytes(), package_id, version)
        published = published_package(package_id, version)
        if published is None:
            pending.append(path)
        elif package_contents(published, package_id, version) != local:
            raise ValueError(
                f"{package_id} {version} is already published with different contents. "
                "Run python3 scripts/sync-package-version.py --bump-patch and commit the new version."
            )
        else:
            print(f"Already published with identical contents: {package_id} {version}")
    return pending


def main() -> int:
    """Write a publish manifest only after every SDK package passes verification."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", required=True, type=Path)
    parser.add_argument("--version", required=True)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    args.output.unlink(missing_ok=True)
    try:
        pending = plan_release(args.packages, args.version)
        args.output.write_text("".join(f"{path}\n" for path in pending), encoding="utf-8")
    except (ValueError, OSError, ET.ParseError) as error:
        print(str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
