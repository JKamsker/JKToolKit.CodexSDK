# Codex 0.142.3 -> 0.142.4 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.142.4` and `external/codex` is pinned to `rust-v0.142.4`.
- Reviewed local upstream source and test deltas from `rust-v0.142.3` to `rust-v0.142.4`.
- Focused on app-server protocol/schema drift, typed SDK wrappers, exec JSONL parsing, tool-search response projections, and approval/config surfaces.
- `git -C external/codex fetch --tags --force` was blocked by the workflow network boundary, but the required local tags were already present and resolved.

## Update Status

- `external/codex` is pinned to `rust-v0.142.4`.
- `UPSTREAM_CODEX_VERSION.json` `api` is `0.142.4`.
- Generated upstream schema/DTO output is up to date.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.142.4` after this parity pass.

## Confirmed Upstream Changes

### 1. Multi-agent spawn guidance was expanded

Upstream added guidance to the deferred `spawn_agent` tool description about when to delegate versus keep work local. The related upstream tests now expect the extra heading and authorization clarification in the tool-search-returned description.

SDK audit:

- The SDK does not hard-code or project upstream `spawn_agent` description text.
- Existing tool-search parsing preserves generic `tool_search_call` and `tool_search_output` payloads without interpreting the embedded prompt metadata.
- No SDK code change was needed.

### 2. Auto-review-specific on-request escalation prompt text was removed

Upstream removed the dedicated `on_request_auto_review.md` prompt template and now appends only the shorter `approvals_reviewer = auto_review` suffix to the normal on-request approval policy text.

SDK audit:

- The SDK exposes approval and reviewer config values but does not generate upstream permissions prompt text.
- Existing approval/config wrappers do not depend on the deleted template or its wording.
- No SDK code change was needed.

### 3. Release and package metadata advanced to 0.142.4

Upstream bumped the Rust workspace package version and published `rust-v0.142.4`. The release notes state that no user-facing changes were identified.

SDK audit:

- The vendored submodule commit matches `rust-v0.142.4`.
- Generated app-server schema and DTO output remains current.
- No API marker or submodule alignment fix was needed.

## Audited Changes That Required No SDK Code

- No files under upstream `codex-rs/app-server-protocol`, `codex-rs/app-server`, exec protocol event definitions, or generated app-server schema inputs changed in this delta.
- The SDK's `tool_search_call` and `tool_search_output` response item parsing remains intentionally generic and is unaffected by upstream description wording changes.
- The SDK's approval-policy model and generated config DTOs preserve data values, not upstream prompt text.

## Validation

- Generator drift check:
  - `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
  - Result: generated output is up to date
- Full suite:
  - `dotnet test JKToolKit.CodexSDK.sln --configuration Release`
  - Result: `792` passed, `15` skipped

## Remaining Drift

No actionable SDK drift was identified for the `0.142.3 -> 0.142.4` window.

Pre-existing backlog remains for stable RPCs that were already noted before this pass, including `modelProvider/capabilities/read` and `account/sendAddCreditsNudgeEmail`.

## Upstream Sources

- GitHub release `openai/codex` tag `rust-v0.142.4`
- `external/codex` local tags `rust-v0.142.3` and `rust-v0.142.4`
- `external/codex/codex-rs/core/src/tools/handlers/multi_agents_spec.rs`
- `external/codex/codex-rs/core/tests/suite/search_tool.rs`
- `external/codex/codex-rs/core/tests/suite/spawn_agent_description.rs`
- `external/codex/codex-rs/prompts/src/permissions_instructions.rs`
- `external/codex/codex-rs/prompts/src/permissions_instructions_tests.rs`
