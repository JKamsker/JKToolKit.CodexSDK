# Codex 0.167.1 -> 0.167.2 Interop Research

## Scope

- Verified that API version `0.167.2` and the vendored `rust-v0.167.2` tag resolve to commit `ce21072c90d32c40af7a55b60b2fb35c84a43a6a`.
- Reviewed the official release notes and all seven changed paths in `rust-v0.167.1..rust-v0.167.2`, including feature registration, provider selection, configuration schema, and upstream regression tests.
- Both release tags were already available locally; no network git fetch was needed. Regenerating the upstream app-server bundle produced no changes.

## Confirmed Upstream Changes

- Google Cloud Vertex AI now uses incremental tool declarations by default on regular Responses routes. The new stable, default-enabled `features.incremental_tools_legacy_gcp` flag controls this behavior independently of the Bedrock flag.
- The existing `features.incremental_tools_legacy` flag is now stable but remains disabled by default and scoped to Amazon Bedrock and Amazon Bedrock Runtime. Enabling it does not override the GCP opt-out. Responses Lite still selects the separate `incremental_tools` feature before provider-specific gating.
- Upstream tests cover GCP defaults and opt-out, independent Bedrock/GCP flags, changed tool catalogs without rewriting history, Vertex namespace/tool-search preservation, and incremental tool inputs during remote compaction.
- No exec argument contract, app-server schema, notification, or SDK DTO changed in this patch.

## SDK Changes

- No handwritten runtime changes are required: Codex constructs provider requests, tool catalogs, and compaction inputs; the SDK launches that runtime rather than duplicating its behavior.
- Exec callers can opt out with `AdditionalOptions = ["--disable", "incremental_tools_legacy_gcp"]`. App-server callers can use `Launch = CodexLaunch.CodexOnPath().WithArgs("app-server", "--disable", "incremental_tools_legacy_gcp")`. The equivalent persisted setting is `incremental_tools_legacy_gcp = false` under `[features]` in `config.toml`.
- Preserved SDK package version `0.167.3` against CLI baseline `0.167.2`; package synchronization confirms this valid SDK patch increment.
- Advanced the integration marker to `0.167.2` after validation.

## Validation

- `python3 scripts/sync-package-version.py` and `--check`
- `dotnet restore JKToolKit.CodexSDK.sln`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release --no-restore -- check`
- Focused `ProcessStartInfoBuilderTests` and `StdioProcessStartInfoBuilderTests` for the existing exec and app-server launch paths
- `dotnet build JKToolKit.CodexSDK.sln --configuration Release --no-restore`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release --no-restore`

All checks passed: 24 focused tests; 3,791 full-suite tests passed and 16 opt-in tests skipped. The Release build completed with zero warnings and zero errors. The test runner emitted a non-failing fallback notice for its artifacts directory.

## Remaining Drift

No actionable drift remains for the SDK's currently exposed surfaces in this patch. Incremental model-request tools, provider gating, and compaction remain upstream runtime responsibilities. Feature flags are configurable through existing launch arguments or Codex configuration rather than a new typed SDK property. Upstream Rust tests were reviewed, not executed; live provider and opt-in end-to-end tests are outside this validation.

## Upstream Sources

- [Release notes](https://github.com/openai/codex/releases/tag/rust-v0.167.2)
- [Source comparison](https://github.com/openai/codex/compare/rust-v0.167.1...rust-v0.167.2)
- Upstream changes `#60617` and `#60848`
- Vendored `codex-rs/features/src/lib.rs`, `codex-rs/core/config.schema.json`, `codex-rs/core/src/session/step_context.rs`, and `codex-rs/Cargo.toml`
- Vendored `codex-rs/core/tests/suite/{gcp,compact_remote,scenarios_incremental_tools}.rs`
