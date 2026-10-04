# Upstream Sync (Schema + DTO Generation)

This repo keeps Codex app-server wire DTOs in sync with upstream by generating **internal** C# types from the upstream JSON Schema bundle.

## Version Pin

`UPSTREAM_CODEX_VERSION.json` tracks upstream Codex versions:

- `api`: the version used for generated upstream schema/DTO artifacts. The upstream sync workflow updates this field.
- `integration`: the version whose deeper handwritten SDK parity pass is complete. The gh-aw parity workflow updates this field after validation.

### SDK package versions

`Directory.Build.props` supplies the same version to the SDK, Semantic Kernel,
and Agent Framework packages for local builds and CI. The initial release for
a Codex CLI version uses that version (for example, `0.160.0`). For additional
SDK releases against the same CLI, bump only the patch:

```bash
python scripts/sync-package-version.py --bump-patch
```

Commit that change with the SDK release (`0.160.1`, then `0.160.2`, etc.).
The CLI pin stays unchanged. Multiple changes in one SDK release need only one
patch bump. CI publishes the committed version; reruns reuse that version.

Upstream sync and the gh-aw parity workflow run
`python scripts/sync-package-version.py` to align the package version with the
API pin. Synchronization preserves SDK patches for an unchanged CLI baseline.
A new major/minor CLI line resets to the CLI version, such as `0.161.0`.
If a CLI patch release collides with an SDK patch already used on the same
major/minor line, the package advances to the next patch instead of reusing or
downgrading a NuGet version. `CodexCliVersion` records the exact CLI baseline
independently of `VersionPrefix`.

`python scripts/sync-package-version.py --check` validates alignment and prints
the package version; CI and gh-aw enforce this check. Runtime packages continue
to use the exact bundled CLI version, independently of SDK-only patches.

When bumping the API version, also update the `external/codex` submodule to the matching tag:

- `rust-v<version>` (example: `rust-v0.104.0`)

## Generator

The generator lives in `src/JKToolKit.CodexSDK.UpstreamGen/`.

Common workflows:

```bash
# (Optional) inspect schema metadata
dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen -- schema-info

# Regenerate internal DTOs into src/JKToolKit.CodexSDK/Generated/Upstream/
dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen -- generate

# Verify generated output is up-to-date (used by CI)
dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen -- check
```

The scheduled upstream-sync workflow runs generation and the generated-output
check deterministically before it creates the update PR. If an upstream schema
change breaks the generator, it restores the generated tree before creating the
bootstrap PR so the bounded parity-repair workflow can repair the generator
without inheriting partial output. The AI parity pass is reserved for generator
repairs and confirmed handwritten SDK drift. GitHub cannot subscribe directly
to releases in another repository, so the workflow polls the stable npm release
hourly and resumes any existing update PR until it validates, merges, and its
NuGet upload succeeds. Failure issues record exhausted repair cycles but do not
permanently pause future scheduled attempts.

## Manual Bump Checklist

1. Update `UPSTREAM_CODEX_VERSION.json` `api`
2. Update `external/codex` to the matching `rust-v<version>` tag
3. Run `UpstreamGen generate`
4. Run the deeper parity pass
5. Update `UPSTREAM_CODEX_VERSION.json` `integration` after parity is complete
6. Run `dotnet test -c Release`
7. Commit the changes
