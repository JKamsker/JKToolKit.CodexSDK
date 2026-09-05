# Codex 0.153.3 -> 0.153.4 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.153.4`.
- Verified `external/codex` is pinned to `rust-v0.153.4` and matches the `rust-v0.153.4` tag commit.
- Audited the local upstream delta from `rust-v0.153.3` to `rust-v0.153.4`, focusing on bundled model catalog changes, app-server `model/list` behavior, generated DTO/schema drift, and SDK model-list parsing.

## Update Status

- No generated upstream schema/DTO changes were introduced in this version window.
- No handwritten SDK code changes were required for this upstream patch.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.153.4` after this parity pass.

## Confirmed Upstream Changes

### 1. GPT-6-Astra is now visible in the bundled picker

Upstream changed the bundled `gpt-6-astra` catalog entry in `codex-rs/models-manager/models.json` from `visibility: "hide"` to `visibility: "list"`. The TUI model selection snapshot now shows `gpt-6-astra` as the default bundled model, followed by `gpt-5.6-sol`, `gpt-5.6-terra`, `gpt-5.6-luna`, and `gpt-5.5`.

SDK impact:

- The app-server `model/list` protocol shape did not change.
- Upstream app-server model projection already maps picker visibility to `hidden` through `hidden: !preset.show_in_picker` and filters by `includeHidden || preset.show_in_picker`.
- The SDK `model/list` wrapper already sends `includeHidden`, parses `hidden` and `isDefault` from the server response, and preserves each full entry in `ModelListEntry.Raw`.
- No SDK update was required for the catalog visibility change.

### 2. GPT-6-Astra bundled guidance was qualified

Upstream updated the bundled `gpt-6-astra` guidance text so asynchronous-question guidance is conditional on the tool being available in the session.

SDK impact:

- The app-server `model/list` response does not expose bundled `model_messages`.
- The SDK does not embed upstream bundled model guidance text.
- No SDK contract, parser, or generated DTO update was required.

### 3. No app-server or exec contract drift was introduced

The upstream diff did not touch `codex-rs/app-server-protocol`, `codex-rs/app-server` protocol definitions, `codex-rs/core`, or `codex-rs/exec` files that define SDK-facing app-server DTOs or exec behavior.

SDK impact:

- Existing app-server typed projections remain aligned for this version window.
- Existing exec resume, session discovery, and structured-output behavior did not require a parity change for `0.153.4`.

## Validation

Validation run during this pass:

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable drift was identified for existing stable SDK surfaces in the `0.153.3 -> 0.153.4` window.

## Upstream Sources

- GitHub release `openai/codex` `rust-v0.153.4`
- Local upstream tags `rust-v0.153.3` and `rust-v0.153.4`
- `external/codex/codex-rs/Cargo.toml`
- `external/codex/codex-rs/models-manager/models.json`
- `external/codex/codex-rs/tui/src/chatwidget/snapshots/codex_tui__chatwidget__tests__model_selection_popup.snap`
- `external/codex/codex-rs/app-server/src/models.rs`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerConfigClient.CatalogAndFeedback.cs`
- `src/JKToolKit.CodexSDK/AppServer/ModelListTypes.cs`
