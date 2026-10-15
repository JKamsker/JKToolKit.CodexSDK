# Codex 0.162.1 -> 0.163.0 Interop Research

## Scope

- Verified that the `0.163.0` API marker, vendored `rust-v0.163.0` commit, and SDK package version match.
- Audited the `rust-v0.162.1..rust-v0.163.0` release notes and source delta, focusing on app-server protocol/schema changes and exec behavior consumed by this SDK.
- Regenerated the upstream app-server schema and DTO bundle after repairing the new tagged-union projections.

## Confirmed Upstream Changes

- Stable `thread/list` requests now accept `excludedThreadIds`, applied before the page limit with a maximum of 100 valid IDs.
- Stable `turn/start.toolOutput` accepts `retain`, which asks Codex to retain the supplied tool output in model history.
- Experimental app-server contracts add durable thread read state, `thread/readState/update`, `thread/items/read`, read-state notifications, prediction-mode forks, and WebSocket request IDs for `environment/add`.
- Realtime voice metadata adds a `v3` catalog and additional voice identifiers.
- Human-readable `codex exec` output suppresses unchanged repeated turn diffs. Resume permission restoration, process-output draining, proxy hardening, and the remaining release changes are implemented inside the upstream CLI/server and do not alter the SDK's exec JSON or process-launch contract.

## SDK Changes

- Added `ThreadListOptions.ExcludedThreadIds` and forwarded it through the handwritten v2 request DTO.
- Added `TurnToolOutput.Retain`, omitting the default `false` value to match upstream serialization.
- Added `EnvironmentAddOptions.WebSocketRequestId` for the already-exposed experimental environment operation.
- Added `ThreadForkOptions.ExperimentalPredictionMode`, including capability and required-ephemeral validation.
- Repaired upstream generation for read-state tagged unions. Generated DTOs now preserve the discriminator and optional turn ID instead of emitting empty placeholder classes and incomplete one-value enums.
- Kept the new standalone experimental read-state and item-read RPCs on the existing raw/generated interop path; the SDK does not currently expose the related `thread/items/list` family as handwritten public operations. Experimental response fields and notifications remain available in preserved raw JSON.

## Validation

- `python3 scripts/sync-package-version.py --check`
- Focused tests for thread-list serialization/wiring, turn tool output, environment wiring, prediction-fork validation, and generator output
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.163.0`
- Local source and schema diff from `rust-v0.162.1` to `rust-v0.163.0`
- Upstream app-server tests for thread exclusions, durable read state, environment WebSocket request IDs, prediction forks, and retained turn tool output
- Upstream exec human-output regression tests for repeated turn diffs

## Remaining Drift

No actionable drift remains for the SDK's currently exposed handwritten surfaces in the `0.162.1 -> 0.163.0` window. Newly introduced standalone experimental RPCs remain intentionally raw until the SDK exposes their surrounding experimental API family.
