# Codex 0.159.3 -> 0.160.0 Interop Research

## Scope

- Verified the `0.160.0` API marker and vendored `rust-v0.160.0` commit match exactly.
- Regenerated the upstream app-server schema and DTO bundle and confirmed it is unchanged.
- Audited the `rust-v0.159.3..rust-v0.160.0` release notes and source delta, focusing on app-server, protocol, configuration, model-provider, session-history, and thread-store changes.

## Confirmed Upstream Changes

- The TUI now resolves its history provider from explicit session/profile choices or the effective configuration for the selected working directory. This fixes TUI resume and fork history when provider defaults differ by project.
- Explicit provider model catalogs are authoritative. When remote catalog discovery is gated off, app-server `model/list` no longer falls back to bundled models for that provider.
- App-server now tracks its running-turn count incrementally and avoids synchronous span enter/exit logging around database work.
- Guardian gained opt-in conversation-history and root-handoff context, plus configurable history prompt and output-budget settings.
- Content-filter failures now have a dedicated internal error detail and optional model-catalog recovery guidance.
- Thread metadata timestamp-only updates avoid unnecessary full metadata rewrites, and SQLite maintenance and initialization behavior was hardened.
- The remaining release changes are confined to the TUI, plugins, analytics, sandboxing, Guardian internals, build infrastructure, or other upstream-only surfaces.

## No Actionable SDK Drift

- The app-server protocol schema did not change, and regeneration produced no schema or DTO changes.
- Provider selection for TUI history is presentation-client behavior. The SDK's exec-mode most-recent lookup continues to match the CLI path it wraps; app-server callers already control provider filters through upstream request options.
- `model/list` catalog selection is performed by app-server. The SDK projects the returned list without supplying a competing fallback and already supports an empty result.
- Running-turn accounting and stderr span logging are app-server implementation details; the existing thread status wire union is unchanged.
- The new Guardian configuration and content-filter guidance fields are core/model-catalog internals and are not added to an SDK-owned typed configuration contract. Generic config writes and raw response projections remain forward-compatible.
- Thread-store and SQLite changes do not change rollout JSONL parsing, session ordering, or public app-server contracts.

## Validation

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.160.0`
- Local commit and source diff from `rust-v0.159.3` to `rust-v0.160.0`
- Upstream app-server thread status and model-list tests
- Upstream TUI provider selection and resume/fork history code
- Upstream protocol error, model message, configuration schema, and thread-store changes

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.159.3 -> 0.160.0` window.
