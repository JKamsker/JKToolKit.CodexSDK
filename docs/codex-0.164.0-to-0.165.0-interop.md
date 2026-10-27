# Codex 0.164.0 -> 0.165.0 Interop Research

## Scope

- Verified that the `0.165.0` API marker and vendored `rust-v0.165.0` commit match.
- Audited the `rust-v0.164.0..rust-v0.165.0` release notes and source delta, focusing on app-server protocol/schema changes and exec behavior consumed by this SDK.
- Regenerated and checked the upstream app-server schema/DTO bundle; generated output was already current.

## Confirmed Upstream Changes

- Account reads add the stable `gcp` discriminator for Google Cloud Vertex AI with externally managed credentials.
- Managed network requirements add `reviewDomains`, whose host patterns are routed to network approval before ordinary allow rules.
- Experimental thread forks can be persisted as attached threads of their source, and `thread/list` can discover attached children that remain hidden from ordinary listings.
- App-server errors add the `modelNotFound` discriminator. The SDK's existing raw `codexErrorInfo` projection preserves it without a handwritten change.
- Experimental `thread/memory/read` and a sampling source override were added. They remain available through raw protocol interop until the SDK exposes those experimental families as handwritten APIs.
- GCP provider support, reasoning-effort override defaults, MCP cancellation, memory-v2 defaults, and session-picker grouping change upstream runtime behavior without changing the SDK's exec JSON or process-launch contract.

## SDK Changes

- Added typed GCP account-state parsing.
- Projected managed network review domains.
- Added attached-fork creation and attached-thread discovery options, wire serialization, experimental capability guards, and invalid ephemeral-fork validation.

## Validation

- `python3 scripts/sync-package-version.py --check`
- Focused tests for account parsing, config requirements, attached fork/list serialization, and experimental guardrails
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.165.0`
- Local source and schema diff from `rust-v0.164.0` to `rust-v0.165.0`
- Upstream app-server protocol types, request processors, and tests for GCP accounts, managed network review domains, attached threads, memory summary reads, and sampling source overrides
- Upstream core, MCP, rollout, and TUI source for provider errors, cancellation, memory defaults, and session discovery changes

## Remaining Drift

No actionable drift remains for the SDK's currently exposed handwritten surfaces in the `0.164.0 -> 0.165.0` window. Newly introduced standalone experimental memory and sampling fields remain intentionally raw.
