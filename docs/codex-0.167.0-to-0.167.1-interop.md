# Codex 0.167.0 -> 0.167.1 Interop Research

## Scope

- Verified that API version `0.167.1` and the vendored `rust-v0.167.1` tag resolve to commit `0a94398fa60c4fac0e7659e4213a0a36e613dd46`.
- Reviewed the release notes and all 11 changed paths in `rust-v0.167.0..rust-v0.167.1`, including feature registration, request construction, compaction, and upstream regression tests.
- Regenerated the upstream app-server bundle; no generated files changed. Existing local tags supplied both baselines, without a network git fetch.

## Confirmed Upstream Changes

- Added the under-development, disabled-by-default `features.incremental_tools_legacy` flag. On non-Responses-Lite routes, incremental tool declarations are restricted to the Amazon Bedrock and Amazon Bedrock Runtime provider IDs. Responses Lite continues to use the existing `incremental_tools` feature.
- Incremental tool catalogs omit hosted web search declarations from history while retaining web search in the top-level request tools. Remote compaction follows the same route-aware tool-history handling and preserves hosted web search configuration.
- Upstream tests cover provider-specific opt-in, changed catalogs, tool removals, preserved request prefixes, and compaction. The final patch restricts the broader regular-Responses behavior introduced by the preceding commit; ordinary OpenAI Responses does not gain this opt-in.
- No exec command contract, app-server schema, notification, or handwritten SDK DTO changed in this patch.

## SDK Changes

- No handwritten runtime changes are required: the SDK launches Codex rather than constructing model requests or maintaining incremental tool history.
- Exec callers can pass `AdditionalOptions = ["--enable", "incremental_tools_legacy"]`; app-server launch arguments can likewise configure the upstream feature. Provider eligibility and the default-disabled behavior remain enforced by Codex, not duplicated in the SDK.
- Preserved SDK package version `0.167.2` against CLI baseline `0.167.1`; the package synchronization script confirms this valid SDK patch increment.
- Advanced the integration marker to `0.167.1` after validation.

## Validation

- `python3 scripts/sync-package-version.py` and `--check`
- `dotnet restore JKToolKit.CodexSDK.sln`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release --no-restore -- check`
- Focused `ProcessStartInfoBuilderTests` for existing exec, resume, and review argument forwarding
- `dotnet build JKToolKit.CodexSDK.sln --configuration Release --no-restore`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

All checks passed: 23 focused tests; 3,791 full-suite tests passed and 16 opt-in tests skipped. The Release build completed with zero warnings and zero errors.

## Remaining Drift

No actionable drift remains for the SDK's currently exposed surfaces in this patch. Incremental model-request tools, provider gating, world-state history, and compaction remain upstream runtime responsibilities; the experimental flag is available through existing launch arguments rather than a new typed SDK property. Upstream Rust tests were reviewed, not executed; validation covers the .NET SDK and generated artifacts.

## Upstream Sources

- [Release notes](https://github.com/openai/codex/releases/tag/rust-v0.167.1)
- [Source comparison](https://github.com/openai/codex/compare/rust-v0.167.0...rust-v0.167.1)
- Upstream changes `#59832` and `#60040`
- Vendored `codex-rs/features/src/lib.rs`, `codex-rs/core/config.schema.json`, `codex-rs/core/src/session/{step_context,turn,world_state}.rs`, and `codex-rs/core/src/compact_remote_v2_attempt.rs`
- Vendored `codex-rs/core/tests/suite/{base_instructions,scenarios_incremental_tools}.rs` and the changed regular-Responses snapshots
