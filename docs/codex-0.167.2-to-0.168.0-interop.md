# Codex 0.167.2 -> 0.168.0 Interop Research

## Scope

- Verified API pin `0.168.0` and vendored `rust-v0.168.0` both resolve to `9480ec100c3edee9fc69172e8e80a4730444094d`.
- Reviewed the official release notes, the changed-path list for `rust-v0.167.2..rust-v0.168.0`, and the touched exec and app-server protocol contracts and corresponding upstream tests. Both tags were available locally; no authenticated git fetch was needed.
- Confirmed same-repository upstream-sync PR #331 targets the checked-out `automation/upstream-codex-0.168.0` branch.

## Confirmed Upstream Changes

- `thread/list` and `thread/search` accept `currentAccountOnly`, defaulting to false. When enabled, upstream filters account-scoped history by the current ChatGPT identity while retaining visible shared/legacy history, and refills pages after filtering. The CLI uses account-visible history for its most-recent exec resume selection; explicit selectors retain their separate lookup path.
- `mcpResource/read` accepts an optional `_meta` object. Caller metadata can contain structured JSON; account and turn routing keys remain reserved and are validated by the server.
- `item/completed` adds optional nullable `contentTokenCount`, an unsigned 64-bit count for native assistant-message content including a sampled footer. It is notification-envelope telemetry, not a stored thread-item field.
- New `modelProvider/list` and `modelProvider/config/write` endpoints support provider discovery and supported custom gateway, Bedrock, and GCP settings patches. Omitted patch fields preserve values; supported explicit nulls clear them. These edits do not themselves select a new default or authenticate a provider.
- Experimental `thread/items/list.deferOutput.commandExecution` allows successful nonempty command output to be deferred. Entries indicate `outputDeferred`; clients must fetch the complete item through `thread/items/read` rather than treating the partial entry as a full item. Ordinary requests do not opt in.
- Config projections add the effective client search provider hint. Exec binds its last-message output before starting the agent and propagates final-output errors. Provider persistence, steering delivery, audio retention, animated image generation, fuzzy ranking, Windows MCP I/O, and TUI workspace/notification/display changes are upstream runtime behavior.

## SDK Changes

- Added `ThreadListOptions.CurrentAccountOnly`, the corresponding public wire field, and `ThreadSearchOptions.CurrentAccountOnly`. Both typed calls forward opt-in filtering; false preserves the existing unfiltered request shape.
- Added `McpResourceReadOptions.Meta` as an optional dictionary of JSON values and forward it under the exact `_meta` wire name without flattening nested values. Server-side routing-key validation remains authoritative.
- Added nullable `ItemCompletedNotification.ContentTokenCount` without changing its constructor. Absent, null, malformed, negative, fractional, and overflowing counts safely project to null; zero and the full unsigned 64-bit range are preserved.
- Added direct/resilient transport regression cases for both account filters and structured MCP metadata, serialization coverage for omitted/default filtering, and completion-telemetry mapping and boundary tests.
- Regeneration produced no additional changes: the bootstrap branch already contains the 0.168.0 generated schema and internal DTOs. Package synchronization preserves package/CLI baseline `0.168.0`.
- Advanced the durable integration marker to `0.168.0` after validating the deeper parity pass.

## Validation

- `python3 scripts/sync-package-version.py` and `--check`
- `dotnet restore JKToolKit.CodexSDK.sln`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- Focused `ItemCompletedContentTokenCountTests`, `AppServerResilientTransportCoverageTests`, and `ThreadListParamsSerializationTests`
- `dotnet build JKToolKit.CodexSDK.sln --configuration Release --no-restore`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release --no-restore`
- `git diff --check`

Release build: zero warnings and errors. Focused tests: 475 passed. Full suite: 3,806 passed, 16 opt-in tests skipped. The runner's artifacts-directory fallback notice is non-failing. Upstream Rust tests were reviewed, not run; no live account/provider or opt-in end-to-end validation was performed.

## Remaining Drift

- Provider discovery/configuration endpoints and experimental item-output deferral remain raw protocol interop via `CodexAppServerClient.CallAsync`; this pass does not add public typed facades for them. Raw callers must preserve omitted-versus-null patch semantics, enable experimental capabilities for deferred output, and honor `outputDeferred` before consuming command output.
- The new effective client search hint remains available through raw config results. Provider request construction, history filtering/pagination, output-file binding, steering, audio/image tools, and UI behavior remain implemented by the vendored CLI runtime.
- SDK offline JSONL session discovery remains account-agnostic and is not a substitute for the runtime's account-visible resume selection. Use the app-server account filter for account-visible history; live CLI resume enforces its own runtime policy. The SDK does not read authentication material to reproduce that policy locally.
- No additional confirmed drift was found in the audited typed surfaces. The explicit raw-only and offline-discovery limitations above remain; this is not a claim that every new upstream capability has a typed SDK wrapper.

## Upstream Sources

- [Official release notes](https://github.com/openai/codex/releases/tag/rust-v0.168.0)
- [Exact source comparison](https://github.com/openai/codex/compare/rust-v0.167.2...rust-v0.168.0)
- Vendored `codex-rs/app-server-protocol/src/protocol/{common.rs,event_mapping.rs,v2/thread.rs,v2/item.rs,v2/mcp.rs,v2/config.rs,v2/model_provider.rs}`
- Vendored `codex-rs/exec/src/{lib.rs,event_processor_with_jsonl_output.rs}`
- Vendored `codex-rs/app-server/tests/suite/v2/{thread_list.rs,model_provider_list_tests.rs,model_provider_config_tests.rs,mcp_resource/caller_metadata.rs}`
