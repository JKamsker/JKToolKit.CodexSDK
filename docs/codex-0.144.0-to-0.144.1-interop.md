# Codex 0.144.0 -> 0.144.1 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.144.1`.
- Verified `external/codex` is pinned to `rust-v0.144.1` and matches the `rust-v0.144.1` tag commit.
- Reviewed the local upstream delta from `rust-v0.144.0` to `rust-v0.144.1`, focusing on SDK protocol/schema, exec, app-server, and generated DTO impact.

## Update Status

- Generated upstream schema/DTO output is up to date.
- No handwritten SDK code changes were needed for this upstream window.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.144.1` after this parity pass.

## Confirmed Upstream Changes

### 1. Code-mode host fallback now uses in-process V8

Upstream changed code-mode session creation so a missing external `codex-code-mode-host` binary falls back to the in-process code-mode session instead of failing tool execution.

SDK impact:

- No public SDK surface currently wraps upstream code-mode session providers or the code-mode host binary.
- The app-server protocol, exec behavior, and generated DTO contracts did not change for this behavior.

### 2. macOS installer exposes `codex-code-mode-host`

Upstream changed `scripts/install/install.sh` to install a visible `codex-code-mode-host` symlink beside `codex` for package-based macOS installs.

SDK impact:

- This repository does not ship or wrap upstream `scripts/install/install.sh`.
- No SDK install, app-server, or exec contract changes were required.

### 3. Installer release metadata parsing was hardened

Upstream replaced ad hoc release-metadata extraction in `install.sh` with a compact-JSON-tolerant parser and added tests for field ordering and nested decoy fields.

SDK impact:

- This is limited to upstream shell installer behavior.
- No generated schema or handwritten SDK API drift was identified.

## Audited Changes That Required No SDK Code

- `codex-rs/app-server-protocol` had no diff between `rust-v0.144.0` and `rust-v0.144.1`.
- Upstream generated DTO/schema output remains clean with `UpstreamGen check`.
- Repo search found no SDK code-mode or installer-facing public surface that corresponds to the changed upstream files.

## Validation

Validation was run after implementation:

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No actionable SDK drift was identified for the `0.144.0 -> 0.144.1` window during this pass.

## Upstream Sources

- `external/codex` local tags `rust-v0.144.0` and `rust-v0.144.1`
- `external/codex/codex-rs/code-mode/src/remote_session.rs`
- `external/codex/codex-rs/code-mode/src/remote_session/connection.rs`
- `external/codex/codex-rs/core/src/tools/code_mode/mod.rs`
- `external/codex/scripts/install/install.sh`
