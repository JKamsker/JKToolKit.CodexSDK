# Codex 0.131.0 -> 0.134.0 Interop Research

## Scope

- Verified upstream target with `gh`: `rust-v0.134.0` is the latest stable release as of 2026-05-27.
- Reviewed release notes for `rust-v0.132.0`, `rust-v0.133.0`, and `rust-v0.134.0`.
- Reviewed source/schema deltas in `external/codex` from `rust-v0.131.0` to `rust-v0.134.0`.
- Focused on handwritten SDK surfaces where generated DTOs existed but public wrappers, parsers, or notifications were still missing.

## Update Status

- `external/codex` is pinned to `rust-v0.134.0`.
- `UPSTREAM_CODEX_VERSION.txt` is pinned to `0.134.0`.
- App-server DTOs are up to date.
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check` passed.

## Confirmed Upstream Changes That Mattered

### 1. Conversation history search is now a first-class app-server API

Upstream added `thread/search` with paged content search results and snippets.

SDK fix:

- Add `SearchThreadsAsync`.
- Add typed `ThreadSearchOptions`, `ThreadSearchPage`, and `ThreadSearchResult`.
- Parse result snippets and thread summaries.

### 2. Permission profiles gained a list API

Upstream added `permissionProfile/list` for available named permission profiles.

SDK fix:

- Add `ListPermissionProfilesAsync`.
- Add typed `PermissionProfileListOptions`, `PermissionProfileListPage`, and `PermissionProfileSummary`.

### 3. Goals are default-on and exposed through thread goal APIs

Upstream added stable goal storage plus `thread/goal/set`, `thread/goal/get`, and `thread/goal/clear` APIs, with update/clear notifications.

SDK fix:

- Add goal set/get/clear wrappers and typed result models.
- Add known `ThreadGoalStatus` values including `usageLimited` and `budgetLimited`.
- Map `thread/goal/updated` and `thread/goal/cleared` notifications.

### 4. Thread runtime settings can be updated through app-server

Upstream added experimental `thread/settings/update` and `thread/settings/updated`.

SDK fix:

- Add `UpdateThreadSettingsAsync`.
- Gate the request behind experimental API capability.
- Preserve upstream sandbox-policy vs permission-profile conflict validation.
- Map `thread/settings/updated` with raw settings plus common typed fields.

### 5. Config requirements gained newer enterprise and computer-use gates

Upstream extended `configRequirements/read` with `allowedApprovalsReviewers`, `allowedPermissions`, `allowAppshots`, `computerUse`, and managed hook requirements.

SDK fix:

- Parse allowed approval reviewers, including `auto_review`.
- Parse allowed permission profile ids.
- Parse `allowAppshots`, `computerUse.allowLockedComputerUse`, and raw `hooks`.

## Validation

- Focused coverage:
  - `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~AppServerThreadManagementClientTests|FullyQualifiedName~AppServerNotificationMapperTests|FullyQualifiedName~ConfigRequirementsParsingTests|FullyQualifiedName~ResilientCodexAppServerClientTests"`
  - Result: `46` passed
- Generator drift check:
  - `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
  - Result: generated output is up to date

## CI Note

The failing `master` CI run after the automated `0.134.0` merge failed in `NugetUpload` because Bitwarden Secrets Manager returned `503 Service Unavailable`; build and test jobs had already passed in the automation branch. The fix still needs to be pushed and monitored through hosted CI.

## Remaining Drift

No compile-breaking generated drift remains. Broader app-server long-tail drift still exists where upstream exposes experimental or highly nested raw shapes; this pass covered the stable and high-value surfaces introduced between `0.131.0` and `0.134.0`.

## Upstream Sources

- `0.132.0`: <https://github.com/openai/codex/releases/tag/rust-v0.132.0>
- `0.133.0`: <https://github.com/openai/codex/releases/tag/rust-v0.133.0>
- `0.134.0`: <https://github.com/openai/codex/releases/tag/rust-v0.134.0>
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/permissions.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/config.rs`
- `external/codex/codex-rs/app-server/src/request_processors/thread_goal_processor.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/thread_settings_update.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/permission_profile_list.rs`
