# Codex 0.159.0 -> 0.159.1 Interop Research

## Scope

- Verified the `0.159.1` API marker matches the vendored `rust-v0.159.1` commit.
- Regenerated the upstream schema and DTO bundle.
- Audited the upstream release notes and the local source delta from `rust-v0.159.0` to `rust-v0.159.1`.
- Focused on the bundled model catalog, Amazon Bedrock model catalogs, provider fallback behavior, and the app-server thread-start tests touched by the release.

## Confirmed Upstream Changes

- GPT-6.1 Sol was added to the bundled model catalog and made the default bundled model.
- Amazon Bedrock Mantle and Runtime catalogs now include GPT-6.1 Sol.
- Amazon Bedrock provider fallback now selects `openai.gpt-6.1-sol` (or its global cross-region variant) ahead of GPT-6 Sol.
- Existing model priorities were shifted to keep GPT-6.1 Sol first in catalog ordering.

## No Additional SDK Drift

- The release does not change the app-server protocol schema, so regeneration produced no DTO changes.
- The SDK forwards model identifiers and provider configuration to Codex rather than maintaining its own bundled or Bedrock model catalog.
- The upstream app-server test changes only assert the new provider-owned default and fallback model IDs; no request or response contract changed.
- Exec session behavior, structured outputs, MCP projections, and handwritten app-server parsers were not touched by this upstream delta.

## Validation

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.159.1`
- Local commit, source, and schema diff from `rust-v0.159.0` to `rust-v0.159.1`
- Upstream bundled catalog, Amazon Bedrock catalog, provider fallback, and app-server thread-start tests

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.159.0 -> 0.159.1` window.
