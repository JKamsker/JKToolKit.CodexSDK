# Codex 0.153.2 -> 0.153.3 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.153.3`.
- Verified `external/codex` is pinned to `rust-v0.153.3` and matches the `rust-v0.153.3` tag commit.
- Audited the local upstream delta from `rust-v0.153.2` to `rust-v0.153.3`, focusing on model-provider catalog changes, app-server protocol/schema drift, generated DTO drift, and the SDK `model/list` wrapper.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.153.3`.
- No handwritten SDK code changes were required for this upstream patch.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.153.3` after this parity pass.

## Confirmed Upstream Changes

### 1. GPT-6-Astra was added to Amazon Bedrock catalogs

Upstream added `openai.gpt-6-astra` to the Amazon Bedrock model-provider constants and the static Bedrock Mantle and Runtime catalogs. The new entry is inserted after `openai.gpt-5.6-sol`, inherits bundled OpenAI model metadata from `gpt-6-astra`, uses the Bedrock display name `GPT-6-Astra`, and shifts the priorities of later Bedrock models.

SDK impact:

- The app-server `model/list` protocol shape did not change in this window.
- The SDK `model/list` wrapper parses model IDs, model slugs, display names, descriptions, input modalities, supported reasoning efforts, default reasoning effort, specialty metadata, multi-agent version, and upgrade metadata from the server response dynamically.
- `ModelListEntry.Raw` continues to preserve the complete upstream model-list entry, so provider-specific model metadata and future catalog additions remain available without static SDK constants.
- No handwritten SDK update was required for the new Bedrock catalog entry.

### 2. GPT-6-Astra bundled guidance text was corrected

Upstream also corrected bundled guidance text in `codex-rs/models-manager/models.json` for `gpt-6-astra`.

SDK impact:

- The app-server `model/list` response does not expose `model_messages`.
- The SDK does not embed upstream bundled model guidance text.
- No SDK contract or generated DTO update was required.

### 3. No app-server or exec contract drift was introduced

The upstream diff did not touch `codex-rs/app-server-protocol`, `codex-rs/app-server`, `codex-rs/core`, or `codex-rs/exec` files that define SDK-facing app-server DTOs or exec behavior.

SDK impact:

- Existing app-server typed projections remain aligned for this version window.
- Existing exec resume, session discovery, and structured-output behavior did not require a parity change for `0.153.3`.

## Validation

Validation run during this pass:

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~AuthAccountConfigWrappersTests"`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable drift was identified for existing stable SDK surfaces in the `0.153.2 -> 0.153.3` window.

## Upstream Sources

- GitHub release `openai/codex` `rust-v0.153.3`
- Local upstream tags `rust-v0.153.2` and `rust-v0.153.3`
- `external/codex/codex-rs/model-provider-info/src/lib.rs`
- `external/codex/codex-rs/model-provider/src/amazon_bedrock/catalog.rs`
- `external/codex/codex-rs/model-provider/src/amazon_bedrock/runtime_catalog.rs`
- `external/codex/codex-rs/model-provider/src/provider.rs`
- `external/codex/codex-rs/models-manager/models.json`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerConfigClient.CatalogAndFeedback.cs`
- `tests/JKToolKit.CodexSDK.Tests/Unit/AuthAccountConfigWrappersTests.cs`
