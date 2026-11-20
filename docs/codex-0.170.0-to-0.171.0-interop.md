# Codex CLI 0.170.0 → 0.171.0 SDK Interop

This pass compares `rust-v0.170.0` to `rust-v0.171.0`. The API pin and
`external/codex` already match commit `ccf9be8cc458bd6b8336fe84d68d8893f2691a5e`.
The existing same-repository upstream-sync PR includes the generated artifacts
and package version `0.171.0`; this pass completes the handwritten integration
baseline rather than changing the API pin.

## Confirmed Upstream Changes

- MCP status entries and tool-call history now identify the contributing
  registration with optional `environmentId`. This is registration scope, not
  necessarily the environment executing the server. Status listing, resource
  reads, tool calls, and OAuth login can select an exact registration. Scoped
  status listing requires both `serverName` and `threadId`; omission preserves
  legacy name-only selection.
- `thread/list` adds `includedThreadIds`, applied before pagination. Omitted or
  null means unrestricted; an empty list matches nothing. Up to 100 identifiers
  are accepted, exclusions still apply, and callers must repeat filters on each
  page. The local store uses indexed metadata for this filter.
- `codex exec --remote` starts or resumes a noninteractive thread on an existing
  app server. Remote resume requires a UUID and does not accept local agent
  configuration, images, or output schemas. JSON output emits `thread.started`
  for the remote thread through the existing event format.
- Experimental search gains streaming partial notifications and separate cancel
  methods. Partials are provisional and may be dropped; the final response is
  authoritative. Realtime gains an independent persisted-history list, canonical
  voice frames, observation timestamps, and handoff/input-saved records; existing
  live/timeline projections continue presenting voice as transcript segments.
- App-server adds `modelProvider/default/set`, account update reason
  `workspaceRoutingReady`, and distinct `bioPolicy` errors rather than mapping
  those errors to `other`. Managed OTel requirements gain header-helper settings.
  The obsolete experimental `currentTime/read` server request is removed.
- Runtime changes include new model catalog defaults, the default TUI workspace,
  composer undo/redo, permission-profile preservation across cwd changes,
  enterprise MCP sign-in without refresh tokens, and turn/approval race fixes.

## SDK Changes

- Added optional `EnvironmentId` to existing status-list, resource-read, and MCP
  OAuth options and forwarded it through their wire parameters. Scoped status
  requests validate the upstream-required thread/server pair before sending.
  Omitted scope retains existing selection behavior.
- Projected registration identity into `McpServerStatusInfo.EnvironmentId` and
  `CodexThreadItemMcpToolCall.EnvironmentId`. The latter is an additive init
  property, preserving its existing constructor and deconstruction contract.
  Missing/null values remain optional for older servers. Malformed tool-call
  scope retains the whole item as unknown rather than losing scope silently.
- Added `ThreadListOptions.IncludedThreadIds` and corresponding wire forwarding.
  Null omission and an explicitly empty array remain distinct. Identifier limits,
  validity, filtering, and pagination are enforced by the server, consistent with
  the existing exclusion filter.
- Added regression tests for scoped requests and responses, status prerequisite
  validation, absent/null scope, malformed item fallback, and inclusion-filter
  serialization and forwarding alongside exclusions.
- Extracted MCP item parsing into its own helper and thread listing into a
  listing-specific client partial, following the existing client organization.
  This keeps both touched files within the repository's 500-line source guard.
- Generation produced no additional artifact changes. Package synchronization
  preserves `0.171.0`; no `Directory.Build.props` repair is necessary.

## Validation

- `python3 scripts/sync-package-version.py` and `--check`: package remains `0.171.0`.
- UpstreamGen Release `generate` and `check`: clean; no generated changes.
- Focused MCP, thread-item, thread-list, and source-size tests: 186 passed.
- `python -m unittest discover -s .github/scripts -p 'test_*.py'`: 104 passed.
- Solution restore and Release build: passed, zero build warnings/errors.
- Full Release solution tests with the CI coverage settings: 3,860 passed,
  16 opt-in tests skipped, no failures.
- `git diff --check`: clean. Integration advances to `0.171.0` after validation.

The initial full run caught source-size guard failures in the two enlarged
files; the responsibility-based extractions above resolve those failures.
Upstream source was inspected, not built or executed. Live remote sessions,
OAuth, voice, native platform runtime behavior, and opt-in end-to-end tests were
not exercised by this Linux run.

## Remaining Drift

- The SDK's local Exec session runner does not add a remote Exec mode: it relies
  on local session discovery and log files. Remote consumers can use the existing
  `CodexAppServerClient.ConnectWebSocketAsync` and thread/turn APIs instead. This
  is not a claim that local Exec options work against remote servers.
- Streaming search/cancellation, independent realtime history, provider-default
  selection, scoped MCP tool invocation and elicitation/status notifications,
  and account routing reasons remain available through generated DTOs and/or raw
  `CodexAppServerClient.CallAsync` and notification payloads, not new handwritten
  typed facades. Clients must correlate streaming search request IDs and replace
  provisional results with the final response.
- `CodexTurnError.CodexErrorInfo` already retains raw error JSON, including
  `bioPolicy`; no enum relaxation or policy implementation is needed. Older-server
  compatibility surfaces are not removed merely because upstream removed an
  experimental request.
- Model selection, TUI rendering, managed OTel helper execution, OAuth lifecycle,
  Guardian behavior, and permission/approval fixes remain runtime responsibilities.
  The SDK does not reimplement these policies or broaden permissions.
- No additional actionable drift was confirmed in the inspected handwritten
  surfaces. This bounded audit does not imply exhaustive typed coverage of
  experimental app-server methods.

## Upstream Sources

- [Official release notes](https://github.com/openai/codex/releases/tag/rust-v0.171.0).
- [Exact source comparison](https://github.com/openai/codex/compare/rust-v0.170.0...rust-v0.171.0).
- Vendored `codex-rs/app-server-protocol/src/protocol/common.rs` and
  `v2/{account.rs,config.rs,item.rs,mcp.rs,model_provider.rs,realtime.rs,realtime_history.rs,shared.rs,thread.rs,thread_data.rs}`.
- Vendored `codex-rs/app-server/src/request_processors/mcp_processor.rs` and
  `codex-rs/exec/src/{cli.rs,remote.rs,event_processor_with_jsonl_output.rs}`.
