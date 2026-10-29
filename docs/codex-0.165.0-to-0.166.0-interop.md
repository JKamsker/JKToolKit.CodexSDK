# Codex 0.165.0 -> 0.166.0 Interop Research

## Scope

- Verified that the `0.166.0` API marker, vendored `rust-v0.166.0` tag, and tag commit match.
- Audited the `rust-v0.165.0..rust-v0.166.0` release notes and source delta, focusing on app-server protocol/schema changes and exec behavior consumed by this SDK.
- Regenerated and checked the upstream app-server schema/DTO bundle.

## Confirmed Upstream Changes

- Reviews now run inline only; the deprecated `detached` delivery value was removed.
- Turn additional-context entries can carry an optional name used to label untrusted context tool output.
- ChatGPT account state can include a selected workspace display name, and its email remains required-but-nullable on the wire.
- Thread usage distinguishes native dollar usage from estimated credit and USD amounts.
- Thread items now carry optional start and completion timestamps directly on their flat wire payloads.
- Managed configuration requirements can expose OpenTelemetry logging, exporter, span-attribute, and tracestate policy.
- App-server also added experimental GCP setup and account-session switching variants, hook environment ownership, process output-drain status, structured provider admission errors, and new raw error discriminators.
- Runtime-only changes include provider validation on resume/input, reasoning-token preservation across resume/fork, thread runtime shutdown fixes, and exec completion backfill through paginated history.

## SDK Changes

- Removed detached review delivery and the detached-review demo/test paths.
- Added named additional-context serialization.
- Projected nullable ChatGPT email and selected workspace names.
- Projected native USD thread usage and thread-item lifecycle timestamps.
- Added a typed managed OpenTelemetry requirements projection while preserving exporter unions and the full payload as raw JSON.

## Validation

- `python3 scripts/sync-package-version.py --check`
- Focused tests for review routing, account/config parsing, usage parsing, additional-context serialization, and thread-item timestamps
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.166.0`
- Local source and schema diff from `rust-v0.165.0` to `rust-v0.166.0`
- Upstream app-server protocol types and tests for account state, config requirements, reviews, turn context, item lifecycle timestamps, process notifications, provider errors, and thread usage
- Upstream core, exec, and app-server source for resume/provider validation, reasoning accounting, runtime shutdown, and completion backfill

## Remaining Drift

No actionable drift remains for the SDK's currently exposed handwritten surfaces in the `0.165.0 -> 0.166.0` window. Newly introduced experimental GCP setup, account-session switching, hook-list thread selection, and process-spawn details remain available through raw protocol interop until the SDK exposes those method families as handwritten APIs.
