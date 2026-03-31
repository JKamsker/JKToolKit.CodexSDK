# Codex 0.106.0 -> 0.118.0 Interop Research

## Scope

- Verified upstream target with `gh`: `rust-v0.118.0` is the latest stable release as of `2026-03-31`.
- Reviewed changes from `rust-v0.106.0` through `rust-v0.118.0`.
- Focused on changes that matter for `JKToolKit.CodexSDK` interop, not TUI-only behavior.

## Update Status

- `external/codex` updated to `rust-v0.118.0`.
- `UPSTREAM_CODEX_VERSION.txt` updated to `0.118.0`.
- Upstream DTOs regenerated from the vendored schema bundle.
- Validation passed with `dotnet test -c Release`.

## Implementation Tasklist

- [x] Add first-class `ServiceTier` support across thread start/resume/fork, turn start, and typed thread summaries.
- [ ] Add exec prompt-plus-stdin support while keeping the legacy stdin-prompt API working.
- [ ] Add typed app-server account login start/cancel wrappers for API key, browser ChatGPT login, and device-code login.
- [x] Extend app/network projections with `pluginDisplayNames`, canonical domain permissions, canonical unix-socket permissions, and newer network flags.
- [ ] Re-run focused unit coverage and full validation after the interop changes land.

## Highest-Value SDK Gaps

### 1. App-server auth/login is still under-modeled

The biggest `0.118.0` gap is app-server account login. Upstream now supports device-code ChatGPT login on `account/login/start`, but the SDK only exposes completion notifications, not a first-class request/response API.

Relevant code:

- `src/JKToolKit.CodexSDK/AppServer/CodexAppServerClient.McpAndConfig.cs`
- `src/JKToolKit.CodexSDK/AppServer/Notifications/AppServerNotificationMapper.cs`
- `src/JKToolKit.CodexSDK.UpstreamGen/AppServerV2DtoGenerator.Fixups.cs`

Recommended work:

1. Add typed wrappers for `account/login/start` and related auth flows.
2. Fix generator handling for important auth unions so they do not collapse to extension-data placeholders.

### 2. Exec-mode does not support prompt-plus-stdin

Upstream `0.118.0` added `codex exec` support for passing a prompt argument while still streaming separate stdin content. The SDK still models the older `-` prompt-only workflow.

Relevant code:

- `src/JKToolKit.CodexSDK/Exec/CodexSessionOptions.cs`
- `src/JKToolKit.CodexSDK/Infrastructure/ProcessStartInfoBuilder.cs`
- `src/JKToolKit.CodexSDK/Infrastructure/Internal/CodexProcessLauncherIo.cs`

Recommended work:

1. Split exec inputs into `PromptArgument` and `StdinPayload` or equivalent.
2. Keep the current API as a compatibility path.

### 3. Service tier / fast mode is not first-class

`serviceTier` entered app-server surfaces in the `0.108.x` line and became more important once fast mode became the default in `0.111.0`. Generated DTOs know about it, but the public thread/turn option types do not.

Relevant code:

- `src/JKToolKit.CodexSDK/AppServer/ThreadStartOptions.cs`
- `src/JKToolKit.CodexSDK/AppServer/ThreadResumeOptions.cs`
- `src/JKToolKit.CodexSDK/AppServer/TurnStartOptions.cs`
- `src/JKToolKit.CodexSDK/AppServer/Protocol/V2/ThreadStartParams.cs`
- `src/JKToolKit.CodexSDK/AppServer/Protocol/V2/ThreadResumeParams.cs`
- `src/JKToolKit.CodexSDK/AppServer/Protocol/V2/TurnStartParams.cs`

Recommended work:

1. Add a public `ServiceTier` abstraction.
2. Wire it through thread start, resume, fork, and turn start.

### 4. App-server `command/exec` exists in generated schema, not in public API

`0.113.0` introduced standalone app-server `command/exec` with write/resize/terminate and streaming output. The SDK only exposes raw escape hatches here.

Relevant code:

- `src/JKToolKit.CodexSDK/Generated/Upstream/AppServer/V2/CommandExec*.g.cs`
- `src/JKToolKit.CodexSDK/AppServer/CodexAppServerClient.JsonRpc.cs`
- `src/JKToolKit.CodexSDK/AppServer/Notifications/AppServerNotificationMapper.cs`

Recommended work:

1. Add public wrappers for `command/exec`, `command/exec/write`, `command/exec/resize`, and `command/exec/terminate`.
2. Add typed mapping for `command/exec/outputDelta`.

### 5. Plugin support is mostly latent, not public

From `0.110.0` onward, plugin install/list/read/install-policy/auth metadata became materially more important. The generated types are present, but the public app-server client still does not expose plugin APIs. `pluginDisplayNames` also is not projected into the public app model.

Relevant code:

- `src/JKToolKit.CodexSDK/Generated/Upstream/AppServer/V2/Plugin*.g.cs`
- `src/JKToolKit.CodexSDK/Generated/Upstream/AppServer/V2/AppInfo.g.cs`
- `src/JKToolKit.CodexSDK/AppServer/AppDescriptor.cs`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerClientSkillsAppsParsers.cs`

Recommended work:

1. Add public `plugin/list`, `plugin/read`, `plugin/install`, and `plugin/uninstall` wrappers.
2. Surface `pluginDisplayNames` on `AppDescriptor`.

### 6. Filesystem watch and shell-command surfaces are missing

`0.115.0` and `0.117.0` added filesystem RPCs and `thread/shellCommand`. The SDK has generated DTOs but no public wrappers or typed notifications.

Relevant code:

- `src/JKToolKit.CodexSDK/Generated/Upstream/AppServer/V2/Fs*.g.cs`
- `src/JKToolKit.CodexSDK/Generated/Upstream/AppServer/V2/ThreadShellCommand*.g.cs`
- `src/JKToolKit.CodexSDK/AppServer/Notifications/AppServerNotificationMapper.cs`

Recommended work:

1. Add public `fs/watch` and `fs/unwatch`.
2. Add typed `fs/changed`.
3. Add `thread/shellCommand` only after verifying its real sandbox behavior against a live CLI.

### 7. Permission, hook, and guardian review flows are not typed enough

`0.113.0` to `0.115.0` added `request_permissions`, hook notifications, `approvalsReviewer`, and guardian approval review. The SDK is mostly forward-compatible through raw JSON, but not pleasant to consume.

Relevant code:

- `src/JKToolKit.CodexSDK/Models/CodexAskForApproval.cs`
- `src/JKToolKit.CodexSDK/AppServer/ApprovalHandlers/AlwaysApproveHandler.cs`
- `src/JKToolKit.CodexSDK/AppServer/ApprovalHandlers/AlwaysDenyHandler.cs`
- `src/JKToolKit.CodexSDK/AppServer/Notifications/AppServerNotificationMapper.cs`
- `src/JKToolKit.CodexSDK/Infrastructure/Internal/JsonlEventParsing/JsonlEventEnvelopeParsers.cs`

Recommended work:

1. Add typed permission-request request/response support, including `scope`.
2. Add typed hook notifications.
3. Add `ApprovalsReviewer` and guardian review public models.

### 8. Network/provider auth projection is stale

The public `NetworkRequirements` model still exposes the legacy allow/deny view, while newer upstream schemas use canonical domain and unix-socket maps. Custom provider auth also needs better typed support after `0.118.0`.

Relevant code:

- `src/JKToolKit.CodexSDK/AppServer/NetworkRequirements.cs`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerClientConfigRequirementsParser.cs`
- `src/JKToolKit.CodexSDK/AppServer/CodexConfigOverridesBuilder.cs`

Recommended work:

1. Extend public network requirement projection to match canonical upstream fields.
2. Add examples or typed helpers for `model_providers.*.auth`.

## Lower-Priority Gaps

- Type `TurnContextEvent.TraceId` once a `0.112.0+` fixture is captured.
- Consider typed image-generation items instead of raw fallback only.
- Add `serverRequest/resolved` and `skills/changed` typed notifications.
- Surface `CodexHome` and other initialize metadata from app-server initialize results.
- Revisit model-list exposure once the generator emits cleaner model metadata types.

## Suggested Implementation Order

1. Auth and generator unions: `account/login/start`, device-code flow, auth union fixups.
2. Exec and tiering: prompt-plus-stdin support, `ServiceTier` public API.
3. App-server power surfaces: `command/exec`, plugins, filesystem watch, `skills/changed`.
4. Typed operational flows: hooks, request-permissions, guardian review, network/provider auth.

## Source Notes

- `0.108.0` and `0.109.0` did not both have normal GitHub Release bodies. For those, tag/compare data was taken from `gh` and repository history.
- `0.109.0` appears to be mostly a release-note/version bump on top of the `0.108.0` code line.

## Upstream Sources

- `0.107.0`: <https://github.com/openai/codex/releases/tag/rust-v0.107.0>
- `0.110.0`: <https://github.com/openai/codex/releases/tag/rust-v0.110.0>
- `0.111.0`: <https://github.com/openai/codex/releases/tag/rust-v0.111.0>
- `0.112.0`: <https://github.com/openai/codex/releases/tag/rust-v0.112.0>
- `0.113.0`: <https://github.com/openai/codex/releases/tag/rust-v0.113.0>
- `0.114.0`: <https://github.com/openai/codex/releases/tag/rust-v0.114.0>
- `0.115.0`: <https://github.com/openai/codex/releases/tag/rust-v0.115.0>
- `0.116.0`: <https://github.com/openai/codex/releases/tag/rust-v0.116.0>
- `0.117.0`: <https://github.com/openai/codex/releases/tag/rust-v0.117.0>
- `0.118.0`: <https://github.com/openai/codex/releases/tag/rust-v0.118.0>
