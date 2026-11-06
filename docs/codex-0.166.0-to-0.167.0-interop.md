# Codex 0.166.0 -> 0.167.0 Interop Research

## Scope

- Verified that the `0.167.0` API marker, vendored `rust-v0.167.0` tag, and tag commit match.
- Audited the `rust-v0.166.0..rust-v0.167.0` release notes and source delta, focusing on app-server protocol changes and resume behavior consumed by this SDK.
- Regenerated and checked the upstream app-server schema/DTO bundle.

## Confirmed Upstream Changes

- Resume discovery now searches saved chats across model providers. Provider filtering remains appropriate for fork lookup, but a most-recent resume must not hide a newer chat from another provider.
- App-server broadcasts `thread/section/changed` after a thread moves into, within, or out of a section. Its required `sectionId` field is nullable when the thread leaves a section.
- Managed OpenTelemetry requirements can control successful server-sent event delta logging through `log_sse_deltas`.
- Runtime and TUI changes also added cross-backend session switching, provider-error read-only history, bounded Code Mode output, system proxy setup, improved network approval handling, and several transcript/rendering fixes. These do not alter the SDK's current handwritten wire contracts beyond the changes above.

## SDK Changes

- Removed configured-provider filtering from most-recent exec resume resolution while preserving working-directory filtering.
- Added a typed `ThreadSectionChangedNotification` projection with strict required/nullable field validation.
- Added `OpenTelemetryRequirements.LogSseDeltas` and parser coverage for the managed policy field.

## Validation

- `python3 scripts/sync-package-version.py --check`
- Focused tests for resume resolution, section-change notification parsing, and managed OpenTelemetry requirements
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.167.0`
- Local source and schema diff from `rust-v0.166.0` to `rust-v0.167.0`
- Upstream changes for cross-provider resume discovery (`#58202`), thread-section broadcasts (`#58380`), and SSE delta logging policy (`#58034`)
- Upstream app-server protocol types and tests for thread sections and configuration requirements

## Remaining Drift

No actionable drift remains for the SDK's currently exposed handwritten surfaces in the `0.166.0 -> 0.167.0` window. Provider-error transcript browsing and cross-backend switching remain TUI concerns; newer experimental app-server and Code Mode details remain available through raw protocol interop until the SDK exposes those method families as handwritten APIs.
