# Codex 0.150.1 -> 0.151.0 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.151.0`.
- Verified `external/codex` is pinned to `rust-v0.151.0` and matches the `rust-v0.151.0` tag commit.
- Audited the local upstream delta from `rust-v0.150.1` to `rust-v0.151.0`, focusing on app-server schema/protocol changes, generated DTO drift, turn-start wiring, lifecycle hydration, raw-response notifications, and thread-history item parsing.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.151.0`.
- Handwritten SDK app-server parity was updated for confirmed stable drift.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.151.0` after this parity pass.

## Confirmed Upstream Changes

### 1. Paginated lifecycle hydration is stable and preferred

Upstream removed experimental gating from `thread/resume.excludeTurns`, `thread/fork.excludeTurns`, and the `thread/resume` backwards cursors. Full-history hydration for paginated threads now emits deprecation notices, and clients are expected to request metadata-only lifecycle responses before paging turns/items separately.

SDK impact:

- Added `ExcludeTurns` to `ThreadResumeOptions`, `ThreadForkOptions`, and the handwritten v2 wire params.
- Preserved `turnsBackwardsCursor` and `itemsBackwardsCursor` on `CodexThread`.
- Added regression coverage for serialization, public option mapping, and lifecycle parsing.

### 2. `turn/start` gained stable standalone-output and one-turn tier fields

Upstream added stable `turnTrigger`, `toolOutput`, and `serviceTierForTurn` fields to `turn/start`. `toolOutput` starts or joins a turn with a named standalone function-call output and must be sent with an empty `input` array. `serviceTierForTurn` applies only to the newly started turn and does not update the thread's sticky service tier.

SDK impact:

- Added `TurnStartOptions.TurnTrigger`, `TurnStartOptions.ToolOutput`, and `TurnStartOptions.ServiceTierForTurn`.
- Added matching handwritten v2 wire fields.
- Added a `TurnToolOutput` public model that preserves upstream's string-or-content-items `output` body as raw JSON.
- Added client-side validation for the upstream-invalid `toolOutput` plus non-empty `input` combination and invalid output body shapes.

### 3. Raw response completion notifications now include usage metadata

Upstream added `usageMetadata` to `rawResponse/completed` notifications, preserving upstream response usage metadata separately from token usage.

SDK impact:

- Added `RawResponseCompletedNotification.UsageMetadata`.
- Updated notification mapping tests to verify the raw metadata payload is preserved.

### 4. `ThreadItem` includes standalone function-call output

Upstream added a `functionCallOutput` thread-history item for standalone named output submitted through `turn/start`.

SDK impact:

- Added `CodexThreadItemFunctionCallOutput`.
- Updated the thread item parser and regression tests to preserve name, namespace, and raw output.

### 5. Additional upstream changes did not require handwritten SDK changes

Upstream also added experimental `turn/settings/update`, experimental `turn/start.cyberAccessProgram`, stable `thread/turns/list` and `thread/items/list` schema exports, `rateLimitExceeded` error info, plugin/catalog config-scope fixes, MCP error-preservation fixes, executor/sandbox telemetry changes, and broad TUI/code-mode/Guardian updates.

SDK impact:

- Generated DTOs already include the new schema artifacts.
- Existing SDK error parsing preserves `codexErrorInfo` as raw JSON, so `rateLimitExceeded` is not dropped.
- Existing plugin/catalog public wrappers parse result shapes generically enough for the upstream config-scope behavior changes.
- Experimental new methods/fields remain accessible through raw JSON-RPC/generated internal DTO surfaces, but no stable handwritten wrapper was added in this pass.

## Validation

Validation run during this pass:

- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~ThreadResumeParamsSerializationTests|FullyQualifiedName~ThreadForkParamsSerializationTests|FullyQualifiedName~TurnStartParamsSerializationTests|FullyQualifiedName~AppServerClientGuardrailSeamTests|FullyQualifiedName~AppServerNotificationMapperTests|FullyQualifiedName~ThreadApiParsingTests"`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable drift was identified for existing stable SDK surfaces in the `0.150.1 -> 0.151.0` window.

## Upstream Sources

- Local upstream tags `rust-v0.150.1` and `rust-v0.151.0`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/turn.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/item.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/shared.rs`
- `external/codex/codex-rs/app-server/src/request_processors/thread_processor.rs`
- `external/codex/codex-rs/app-server/src/request_processors/turn_processor.rs`
- `external/codex/codex-rs/app-server/README.md`
