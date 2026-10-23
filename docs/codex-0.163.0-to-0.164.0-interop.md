# Codex 0.163.0 -> 0.164.0 Interop Research

## Scope

- Verified that the `0.164.0` API marker and vendored `rust-v0.164.0` commit match.
- Audited the `rust-v0.163.0..rust-v0.164.0` release notes and source delta, focusing on app-server protocol/schema changes and exec behavior consumed by this SDK.
- Regenerated and checked the upstream app-server schema/DTO bundle; generated output was already current.

## Confirmed Upstream Changes

- Turn start and steer requests can patch Responses API client metadata without replacing existing unprotected entries.
- Prediction forks can disable inherited MCP servers, and MCP status entries preserve the first tool-list page's opaque metadata.
- Realtime start accepts a distinct client-generated voice-session identifier.
- Image-generation history items now use an inline-result-or-file-reference union and can report observed local input images.
- Managed application requirements add Desktop workspace storage defaults; account plans add `sci` and `sci_plus`.
- Stable feedback APIs add `feedback/save`, and feedback payloads support tags.
- Managed hooks add pre/post-model events. The SDK's existing raw hook projection preserves these fields without a handwritten change.
- Experimental `thread/sample` and `thread/sample/cancel` were introduced. They remain available through generated/raw protocol interop until the SDK exposes that experimental family as a handwritten API.
- Exec changes in this window improve rollout-read cancellation and MCP elicitation approval behavior inside the CLI; they do not change the SDK's exec JSON or process-launch contract.

## SDK Changes

- Added metadata-patch options, serialization, and experimental capability guards for turn start and steer.
- Added prediction-mode MCP disablement with upstream dependency validation.
- Added realtime voice-session wiring, MCP tool-list metadata, managed application storage projections, and the new plan values.
- Updated image-generation history parsing for file references and observed input images while retaining inline results.
- Added typed `feedback/save` support and feedback tags across direct and resilient clients.

## Validation

- `python3 scripts/sync-package-version.py --check`
- Focused tests for turn metadata, prediction forks, realtime start, MCP status, config requirements, image-generation history, feedback, and wire values
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.164.0`
- Local source and schema diff from `rust-v0.163.0` to `rust-v0.164.0`
- Upstream app-server protocol types and tests for feedback saving, metadata patching, prediction forks, MCP catalog metadata, managed storage, realtime voice sessions, and image-generation history
- Upstream exec source and tests for rollout reads and MCP elicitation handling

## Remaining Drift

No actionable drift remains for the SDK's currently exposed handwritten surfaces in the `0.163.0 -> 0.164.0` window. The newly introduced standalone experimental thread-sampling RPCs remain intentionally raw.
