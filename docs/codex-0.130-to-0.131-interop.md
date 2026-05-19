# Codex 0.130.0 -> 0.131.0 Interop Research

## Scope

- Verified upstream target with `gh`: `rust-v0.131.0` is the latest stable release as of 2026-05-19.
- Reviewed the upstream delta from `rust-v0.130.0` to `rust-v0.131.0`.
- Focused on handwritten SDK surfaces where the generated schema bundle omitted experimental fields or where upstream protocol shapes changed behind existing SDK wrappers.

## Update Status

- `external/codex` is pinned to `rust-v0.131.0`.
- `UPSTREAM_CODEX_VERSION.txt` is pinned to `0.131.0`.
- App-server DTOs were already up to date from the upstream-version bump.
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check` passed.
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release` passed.

## Confirmed Upstream Changes That Mattered

### 1. Thread and turn start now carry runtime environments and permission profile ids

Upstream added experimental fields for runtime workspace roots, execution environments, and named permission profiles to thread start/resume/fork and turn start. These fields exist in Rust source but are not fully represented by the current JSON schema export.

SDK fix:

- Add options and wire DTO fields for `runtimeWorkspaceRoots`, `environments`, and `permissions`.
- Validate these fields behind `experimentalApi`.
- Reject conflicting sandbox override plus permission profile inputs locally before sending stale mixed payloads.
- Parse lifecycle `runtimeWorkspaceRoots`, `instructionSources`, and `activePermissionProfile`.

### 2. Initialize capabilities gained attestation support

Upstream v1 initialize capabilities now include `requestAttestation`.

SDK fix:

- Add `InitializeCapabilities.RequestAttestation`.
- Add `CodexAppServerClientOptions.RequestAttestation`.
- Normalize and serialize the capability only when requested.

### 3. Plugin APIs gained remote catalogs and sharing

Upstream changed `plugin/list` filtering from `forceRemoteSync` to `marketplaceKinds`, removed `forceRemoteSync` from plugin install/uninstall requests, made marketplace paths nullable for remote catalogs, added remote/source/share metadata to plugin summaries, and added plugin share save/list/update/checkout/delete requests.

SDK fix:

- Add `PluginListMarketplaceKind` and send `marketplaceKinds`.
- Keep legacy `ForceRemoteSync` properties as source-compatible no-ops and stop serializing removed fields.
- Support remote marketplace selectors on plugin read/install.
- Parse remote marketplace paths, remote source metadata, remote UI URLs, availability, keywords, local versions, and share context.
- Add plugin share save/update/list/checkout/delete wrappers and resilient-client forwarding.

### 4. Remote-control and environment APIs are now app-server requests

Upstream added experimental `remoteControl/enable`, `remoteControl/disable`, `remoteControl/status/read`, and `environment/add`.

SDK fix:

- Add remote-control status results and typed status values.
- Add remote-control enable/disable/status wrappers.
- Add environment add wrappers.
- Gate all new requests behind `experimentalApi`.
- Extend `remoteControl/status/changed` notification mapping with `serverName` and `installationId`.

### 5. Config and approval metadata drifted

Upstream added profile names to user config-layer sources, `allowManagedHooksOnly` to config requirements, and `auto_review` as an approvals reviewer.

SDK fix:

- Parse config layer `profile`.
- Parse requirements `allowManagedHooksOnly`.
- Add `CodexApprovalsReviewer.AutoReview`.

## Validation

- Focused parity coverage:
  - `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~InitializeCapabilitiesTests|FullyQualifiedName~ThreadStartParamsSerializationTests|FullyQualifiedName~ThreadResumeParamsSerializationTests|FullyQualifiedName~ThreadForkParamsSerializationTests|FullyQualifiedName~TurnStartParamsSerializationTests|FullyQualifiedName~AppServerClientGuardrailSeamTests|FullyQualifiedName~ThreadLifecycleEnvelopeTests|FullyQualifiedName~ConfigRequirementsParsingTests|FullyQualifiedName~ConfigReadWrapperTests|FullyQualifiedName~PluginClientTests|FullyQualifiedName~RemoteControlEnvironmentClientTests|FullyQualifiedName~AppServerNotificationMapperTests|FullyQualifiedName~SourceFileSizeGuardTests"`
  - Result: `115` passed
- Generator drift check:
  - `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
  - Result: up-to-date
- Full validation:
  - `dotnet test JKToolKit.CodexSDK.sln --configuration Release`
  - Result: `759` passed, `15` skipped

## Remaining Drift

No generated-schema drift remained after the check. The main handwritten risk from this pass was the experimental Rust-only fields that are not fully present in exported schemas; those now have focused tests so future upstream changes should fail loudly.

## Upstream Sources

- `0.131.0`: <https://github.com/openai/codex/releases/tag/rust-v0.131.0>
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/turn.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/plugin.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/remote_control.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/environment.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/common.rs`
