# Codex 0.149.0 -> 0.149.1 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.149.1`.
- Verified `external/codex` is pinned to `rust-v0.149.1` and matches the `rust-v0.149.1` tag commit.
- Audited the local upstream delta from `rust-v0.149.0` to `rust-v0.149.1`, focusing on exec thread metadata, generated DTO drift, config schema changes, and existing SDK surfaces.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.149.1`.
- Handwritten SDK parity changes were required for the new exec thread source classification option.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.149.1` after this parity pass.

## Confirmed Upstream Changes

### 1. Exec callers can classify newly created threads

Upstream added a global `codex exec --thread-source <SOURCE>` option. The CLI defaults new sessions to `user`, but callers can now classify new or forked exec-created threads with a feature source such as `automated_review`. The upstream TypeScript SDK added a matching `threadSource` option and only forwards it when a new thread is created, not when resuming an existing `threadId`.

SDK impact:

- Added `CodexSessionOptions.ThreadSource`.
- Emitted `--thread-source <value>` for new `codex exec` launches.
- Suppressed the typed option for `codex exec resume` so existing thread classifications are not overwritten.
- Added validation to prevent conflicting typed `ThreadSource` and raw `--thread-source` entries in `AdditionalOptions`.

### 2. Detached memory requests are now marked as memory consolidation

Upstream now includes `thread_source = "memory_consolidation"` in detached memory request metadata. This is internal upstream request metadata and did not require a public SDK change in this pass.

### 3. Remote compaction gained image-budget handling behind an experimental feature

Upstream added the under-development `compaction_image_budget` feature and changed remote compaction internals so retained images can be charged against the compaction budget. Existing SDK JSONL/app-server parsing already preserves compaction and image payloads generically, and no public SDK surface was affected.

## Validation

Validation run during this pass:

- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~CodexExecParityDriftTests"`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.149.0 -> 0.149.1` window.

## Upstream Sources

- `external/codex` local tags `rust-v0.149.0` and `rust-v0.149.1`
- `external/codex/codex-rs/exec/src/cli.rs`
- `external/codex/codex-rs/exec/src/lib.rs`
- `external/codex/sdk/typescript/src/exec.ts`
- `external/codex/sdk/typescript/src/threadOptions.ts`
- `external/codex/codex-rs/core/src/turn_metadata.rs`
- `external/codex/codex-rs/features/src/lib.rs`
