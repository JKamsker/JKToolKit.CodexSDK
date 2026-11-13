# Codex 0.168.0 to 0.169.0 Interop

## Scope

- API target and vendored CLI: `rust-v0.169.0`, commit `d32063f96c2dff1dc96af839b3952abb6acfeda4`.
- Previous deeper integration baseline: `0.168.0`.
- Reviewed official release notes and the local tag-to-tag changed-file list, then bounded the source audit to exec, app-server protocol changes, model catalog behavior, history reconstruction, and corresponding SDK projections. Both required tags were already available locally; no network Git operations were necessary.

## Confirmed Upstream Changes

- `model/list.modelProvider` selects an already configured provider for the catalog request without changing the app-server default or a thread's provider. Upstream catalog selection, ordering, and default-model calculation remain server responsibilities.
- `account/read` adds nullable `userId`, identifying the ChatGPT user associated with that response. This is distinct from workspace/account routing identifiers.
- Agent-message items add optional `moderation`, with a required `moderated` boolean and optional `display` instructions. Display actions are open-ended strings with optional block type and trusted-access/contact hints. History reconstruction retains these instructions and uses the moderation event's item identifier when supplied. Unknown display actions must be left to the producing client.
- Gateway OAuth reads and logins can select a configured provider; device authorization adds a prompt and login-attempt identifiers. `account/securitySetup/read` returns an optional account-bound reminder without changing account security.
- Thread history gains attached-thread search inclusion and attachment ownership metadata. Queue submissions retain Responses API client metadata. Experimental thread/turn settings can replace environments, and MCP text output can be deferred in item history. Approved-plan preparation supports the fresh-thread handoff.
- Exec/review project trust is explicit: workspace-write alone no longer enables untrusted project configuration or MCP servers. Interrupted history now closes its matching turn, and stale aborts no longer interrupt an unrelated active turn. These are CLI/server runtime fixes, not client-side policy to emulate.
- Plugin starter prompts now permit up to 512 characters per entry. The inference-hook denial error gains a distinct wire value. Terminal workspace, Cloud, clipboard, diagnostics, marketplace caching, and platform-specific fixes remain runtime behavior.

## SDK Changes

- Added `ModelListOptions.ModelProvider` and forward its exact value through the existing generated request DTO. The direct and resilient APIs share this request path; omitted selection preserves the existing request shape.
- Added `AccountReadResult.UserId`, retaining compatibility with older responses where the field is absent or null. No authentication material is read to infer it.
- Added typed `CodexAgentMessageModeration` and `CodexAgentMessageDisplay` projections and an optional init-only `CodexThreadItemAgentMessage.Moderation` property, preserving the existing positional constructor. Thread reads and collected turn results share the item parser. Open-ended actions and nullable boolean hints are preserved without applying display actions or changing security policy. Malformed metadata falls back to the raw unknown-item representation rather than silently presenting it as an ordinary unmoderated answer.
- Added regression cases for provider selection through direct/resilient transport, optional user identifiers, moderation/display projection, unknown actions, and malformed payload fallback with raw retention.
- Regeneration produced no additional changes: the upstream-sync branch already contains the 0.169.0 schema and generated DTOs. Package synchronization preserves the existing `0.169.0` package/CLI baseline; `Directory.Build.props` needs no additional edit. Advanced the durable integration marker to `0.169.0` after successful validation.

## Validation

- `python3 scripts/sync-package-version.py` and `--check`.
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`.
- Focused `AppServerThreadItemContractTests`, `AuthAccountConfigWrappersTests`, and `AppServerResilientTransportCoverageTests`: 595 passed, no failures or skips.
- `dotnet restore JKToolKit.CodexSDK.sln`.
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`.
- `dotnet build JKToolKit.CodexSDK.sln --configuration Release --no-restore`: zero warnings and errors.
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release --no-restore`: 3,826 passed, 16 opt-in tests skipped, no failures.
- `git diff --check`.

Upstream source/tests were reviewed, not executed. Live account/provider and opt-in end-to-end tests were not enabled. The test runner's artifacts-directory fallback notice is non-failing.

## Remaining Drift

- Provider-scoped gateway OAuth, device prompts, security-setup reminders, and approved-plan preparation remain raw protocol interop via `CodexAppServerClient.CallAsync`, not new public typed facades. Raw clients must bind account-specific notices to the response's account/user and leave unknown display actions to their producing client.
- New attached-thread inclusion/ownership fields, queue metadata, settings environment replacement, and MCP item-output deferral remain raw interop. Existing typed settings wrappers do not expose environment replacement. Omission/null and empty-list semantics differ: environment omission/null preserves selection (with the upstream cwd exception); an empty environment list clears it. Raw history clients must honor `outputDeferred` and fetch full items with `thread/items/read`.
- The SDK does not automatically trust a project or change trust to restore older exec behavior. Project configuration, MCP startup, provider authentication, abort targeting, terminal/Cloud/platform behavior, and plugin prompt limits are enforced by the vendored runtime. There is no new typed exec/review trust switch in this pass.
- The new inference-hook denial value remains available through existing raw error payloads. No broad expansion of experimental/public wrappers is claimed by advancing the integration baseline.

## Upstream Sources

- [Official release notes](https://github.com/openai/codex/releases/tag/rust-v0.169.0).
- [Exact source comparison](https://github.com/openai/codex/compare/rust-v0.168.0...rust-v0.169.0).
- Vendored `codex-rs/app-server-protocol/src/protocol/{common.rs,thread_history.rs,v2/account.rs,v2/item.rs,v2/model.rs,v2/thread.rs,v2/thread_data.rs,v2/turn.rs,v2/shared.rs,v2/plugin.rs}`.
- Vendored `codex-rs/app-server/src/model_catalog.rs`, `codex-rs/app-server/tests/suite/v2/model_list.rs`, and `codex-rs/exec/src/{cli.rs,lib.rs}`.
