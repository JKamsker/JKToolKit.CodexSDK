# Codex 0.154.0 -> 0.155.1 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.155.1`.
- Verified `external/codex` is pinned to the `rust-v0.155.1` commit.
- Audited the local upstream delta from `rust-v0.154.0` to `rust-v0.155.1`, focusing on app-server protocol/schema drift, generated DTO output, feedback upload, thread attachments, notification mapping, and exec/resume behavior.
- Verified the `0.155.1` hotfix only restores a TUI reasoning-summary default and does not change SDK-relevant app-server, protocol, core, exec, or CLI surfaces.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.155.1`.
- Handwritten SDK wrappers and projections cover the stable app-server additions in this version window.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.155.1` after this parity pass.

## Confirmed Upstream Changes

### 1. App-server gained stored thread attachment APIs

Upstream added stable `thread/attachment/add`, `thread/attachment/list`, and `thread/attachment/remove` requests. Attachments are identified by a thread-local `(attachmentType, identityKey)` pair, carry an application-defined JSON payload, and are listed with cursor pagination. Adding the same identity is idempotent and returns either `created` or `existing`.

SDK impact:

- Added typed add, list, and remove options and results.
- Preserved application-defined payloads and raw response objects as `JsonElement` values.
- Added direct and resilient-client wrappers for all three methods.
- Added focused serialization, parsing, and resilient-surface coverage.

### 2. Attachment mutations emit a typed notification

Upstream added `thread/attachment/updated` after an attachment is created or deleted.

SDK impact:

- Added `ThreadAttachmentUpdatedNotification` and `ThreadAttachmentOperation`.
- Added strict notification mapping for the required identifiers and known `created`/`deleted` operations.
- Malformed or forward-unknown notification payloads continue through the existing `UnknownNotification` fallback.

### 3. Feedback upload returns the base-instructions prompt hash

Upstream added nullable `promptHash` to `feedback/upload`. It is the whitespace-normalized SHA-256 of the session base instructions and is absent when rollout prompt metadata is unavailable.

SDK impact:

- Added `FeedbackUploadResult.PromptHash`.
- Updated feedback response parsing and regression coverage while keeping the field nullable.

### 4. Other upstream changes did not require SDK changes

The version window includes substantial voice, daemon, Guardian, managed-worktree, credential-broker, TUI, and Python SDK work. Those changes do not alter the SDK's current stable contracts.

SDK impact:

- No exec resume, structured-output, JSONL, or process-start behavior drift requiring an SDK change was identified.
- New internal image-generation metadata remains available through existing raw JSON preservation where applicable.

## Validation

Validation run during this pass:

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "AppServerThreadManagementClientTests|AuthAccountConfigWrappersTests|AppServerNotificationMapperTests|ResilientCodexAppServerClientTests"`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable drift was identified for existing stable SDK surfaces in the `0.154.0 -> 0.155.1` window.

## Upstream Sources

- GitHub release tags `openai/codex` `rust-v0.155.0` and `rust-v0.155.1`
- Local upstream commits for `rust-v0.154.0`, `rust-v0.155.0`, and `rust-v0.155.1`
- `external/codex/codex-rs/app-server-protocol/src/protocol/common.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/feedback.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread_attachment.rs`
- `external/codex/codex-rs/app-server/src/request_processors/thread_attachments.rs`
- `external/codex/codex-rs/thread-store/src/thread_attachments.rs`
