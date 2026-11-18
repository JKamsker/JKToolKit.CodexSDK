# Codex CLI 0.169.0 → 0.170.0 SDK Interop

This pass compares the durable `0.169.0` integration baseline with vendored
`rust-v0.170.0` (`29eb79e9866b3262a777fdf8195c1b9e63684c91`). The API pin and
submodule already match. Upstream-sync PR #341 already supplies the schema,
generated DTOs, and `0.170.0` package version. Research is bounded to the official
release notes, changed-file list, changed protocol contracts, and corresponding
SDK projections; no unrelated cleanup is included.

## Confirmed Upstream Changes

- Answer moderation display instructions now use a versioned discriminated union,
  replacing the previous open-ended `action` object. Version 1 supports `show`
  with required `trustedContacts` (`check`/`skip`) and optional wellbeing helpline
  annotation (`answer`/`prompt`); `hide` with a wellbeing prompt helpline
  replacement; and `block` with either a protection type or an optional cyber
  trusted-access hint. Additional fields within supported instructions are
  tolerated. Unknown versions, modes, or nested variants are not supported.
- Upstream stored-history deserialization leaves unsupported display payloads
  restricted: it sets `moderated` to true and discards the unreadable instruction.
  App-server history conversion now directly preserves the shared versioned
  display contract rather than converting it to the old action-based shape.
- `userVerification/verify` adds optional `biometricType`, currently `fingerprint`.
  This is native authentication context, not part of the signed proof. Older
  servers may omit it and unknown native methods produce null.
- Experimental MCP environment scoping keeps identically named server
  registrations separate. Server-side resolution and registration identity are
  used for OAuth and status handling; no new app-server status field is introduced.
- Permission updates preserve inherited restrictions and legacy sandbox templates.
  Model-facing history/item identifiers survive replay. Guardian reviews can reuse
  reviewer history on a loaded thread, independently of the actor conversation.
- The runtime adds TUI realtime WebRTC negotiation and refreshable OTLP HTTP
  headers, immediately falls back to HTTP for oversized model WebSocket requests,
  improves MCP OAuth cancellation safety and Cloud recovery, and fixes file/thread
  search and Windows persistence/sandbox registration. These are upstream runtime
  behaviors, not new SDK transport or filesystem implementations.

## SDK Changes

- Added `CodexAgentMessageModeration.VersionedDisplay`, projected by the existing
  shared thread-item parser used for thread history and collected turn results.
  `CodexAgentMessageDisplayShow`, `CodexAgentMessageDisplayHide`,
  `CodexAgentMessageDisplayProtection`, and `CodexAgentMessageDisplayCyber` expose
  the supported union as typed records. Trusted-contact and helpline-subject
  values are enums; absent/null optional annotations and trusted-access hints
  remain optional. `Version` is 1 for all supported variants.
- Preserved the existing `Display` property and its legacy action-based public
  model for older servers. Versioned responses populate `VersionedDisplay` and
  leave legacy `Display` null; callers must inspect the new property rather than
  interpreting a null legacy property as permission to show an answer.
- Unsupported or malformed versioned instructions retain the entire agent item
  as `CodexThreadItemUnknown`, with raw JSON intact. They never fall through to
  the legacy action parser, even when an `action` field is present. This follows
  the SDK's existing conservative item-parser fallback rather than presenting an
  unreadable instruction as an ordinary unmoderated typed answer. The SDK does
  not execute presentation instructions or infer authorization from hints.
- Added regression coverage for all supported variants, optional/null fields,
  additional-field retention, required version/mode, unknown versions/variants,
  malformed nested data, and prevention of legacy fallback. Existing legacy
  moderation tests continue to pass.
- Regeneration produced no additional changes. Package synchronization preserves
  `0.170.0`; no additional `Directory.Build.props` edit is necessary. The integration
  marker advances to `0.170.0` only after validation completes.

## Validation

- `python3 scripts/sync-package-version.py` and `--check`.
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`.
- Focused `AppServerVersionedDisplayTests` and `AppServerThreadItemContractTests`:
  129 passed, no failures or skips.
- `dotnet restore JKToolKit.CodexSDK.sln` completed.
- Generated DTO check passed; Release build completed with zero warnings/errors.
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`: 3,846 passed,
  16 opt-in tests skipped, no failures.
- The first full run hit an unrelated WebSocket connection-timeout test failure.
  The test passed in isolation and the full rerun passed; no transport changes
  were made to hide the failure.
- `git diff --check`.

Upstream source and tests were reviewed, not executed. Live voice, OAuth,
biometric verification, Windows-specific runtime behavior, and opt-in end-to-end
tests are not exercised by this Linux validation run.

## Remaining Drift

- Native user verification, including the new optional `biometricType`, remains
  raw protocol interop through `CodexAppServerClient.CallAsync`. This pass does
  not introduce a typed verification facade or treat biometric context as proof.
- Experimental MCP environment identity, Cloud recovery, Guardian persistence,
  permission normalization, OTLP header refresh, voice negotiation, and model HTTP
  fallback remain enforced by the vendored runtime. The SDK does not duplicate
  these policies or broaden filesystem/network permissions.
- Consumers implementing presentation must handle `VersionedDisplay`, keep unknown
  raw items conservative, and leave legacy unknown actions to the producing
  client. This pass exposes metadata; it does not render, hide, or block UI text.
- No other actionable drift was confirmed in the inspected changed SDK surfaces.
  Advancing the baseline is not a claim of complete typed coverage of every
  experimental app-server method.

## Upstream Sources

- [Official release notes](https://github.com/openai/codex/releases/tag/rust-v0.170.0).
- [Exact source comparison](https://github.com/openai/codex/compare/rust-v0.169.0...rust-v0.170.0).
- Vendored `codex-rs/protocol/src/items/display.rs`, `items.rs`, `protocol.rs`,
  `models.rs`, `permissions.rs`, and `error.rs`.
- Vendored `codex-rs/app-server-protocol/src/protocol/v2/{item.rs,thread.rs,tests.rs,user_verification.rs}`
  and `src/protocol/thread_history.rs`.
- Vendored `codex-rs/app-server/src/request_processors/mcp_processor.rs` and
  `codex-rs/app-server/README.md`.
