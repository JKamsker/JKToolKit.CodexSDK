# Repo Map

## Upstream pin and schema generation

- `UPSTREAM_CODEX_VERSION.txt`
- `external/codex/`
- `docs/upstreamgen.md`
- `.github/workflows/upstream-sync.yml`
- `src/JKToolKit.CodexSDK.UpstreamGen/UpstreamSchemaDiscovery.cs`
- `src/JKToolKit.CodexSDK.UpstreamGen/AppServerV2DtoGenerator.Fixups.cs`
- `src/JKToolKit.CodexSDK/Generated/Upstream/appserver.v2.schema.json`
- `src/JKToolKit.CodexSDK/Generated/Upstream/AppServer/V2/`

Use these when the upstream version pin, vendored submodule, or generated DTO bundle changes.

## Exec parity hotspots

- `src/JKToolKit.CodexSDK/Exec/Internal/CodexSessionRunner.cs`
- `src/JKToolKit.CodexSDK/Exec/Internal/CodexSessionRunnerLogHelpers.cs`
- `src/JKToolKit.CodexSDK/Exec/Internal/CodexResumeTargetResolver.cs`
- `src/JKToolKit.CodexSDK/Infrastructure/CodexSessionLocator.cs`
- `src/JKToolKit.CodexSDK/Infrastructure/Internal/CodexSessionThreadNameIndex.cs`
- `src/JKToolKit.CodexSDK/Infrastructure/ProcessStartInfoBuilder.cs`
- `src/JKToolKit.CodexSDK/StructuredOutputs/Internal/StructuredOutputExecCapture.cs`
- `src/JKToolKit.CodexSDK/StructuredOutputs/Internal/StructuredOutputRetryRunner.cs`

Most useful tests:

- `tests/JKToolKit.CodexSDK.Tests/Integration/CodexClientResumeSessionTests.cs`
- `tests/JKToolKit.CodexSDK.Tests/Integration/CodexClientLiveResumeBootstrapTests.cs`
- `tests/JKToolKit.CodexSDK.Tests/Unit/CodexSessionLocatorHelpersTests.cs`
- `tests/JKToolKit.CodexSDK.Tests/Unit/ProcessStartInfoBuilderTests.cs`
- `tests/JKToolKit.CodexSDK.Tests/Unit/CodexStructuredOutputResumeTests.cs`
- `tests/JKToolKit.CodexSDK.Tests/Unit/CodexStructuredOutputRetryTests.cs`

Useful search patterns:

- `rg -n "ResumeSessionAsync|WaitForNewSessionFileAsync|session_index|NormalizedPath|MostRecent|output schema|review" src tests external/codex`

## App-server and typed interop hotspots

- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerClientMcpParsers.cs`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerClientThreadResponseParsers.cs`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerSkillsAppsClient.cs`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerConfigClient.CatalogAndFeedback.cs`
- `src/JKToolKit.CodexSDK/AppServer/CodexTurnHandle.cs`
- `src/JKToolKit.CodexSDK/AppServer/CodexThread.cs`
- `src/JKToolKit.CodexSDK/AppServer/CodexThreadSummary.cs`
- `src/JKToolKit.CodexSDK/AppServer/ThreadRead/`
- `src/JKToolKit.CodexSDK/AppServer/Notifications/`

Most useful tests:

- `tests/JKToolKit.CodexSDK.Tests/Unit/McpParsersTests.cs`
- `tests/JKToolKit.CodexSDK.Tests/Unit/McpToolArgumentGatingTests.cs`
- `tests/JKToolKit.CodexSDK.Tests/Unit/AppServerClientGuardrailSeamTests.cs`
- `tests/JKToolKit.CodexSDK.Tests/Unit/AppServerNotificationMapperFaultToleranceTests.cs`
- `tests/JKToolKit.CodexSDK.Tests/Unit/AppServerOverridePipelineTests.cs`
- `tests/JKToolKit.CodexSDK.Tests/Unit/AuthAccountConfigWrappersTests.cs`

Useful search patterns:

- `rg -n "turn/start|thread/read|inputSchema|skills/list|app/list|plugin|account|configRequirements|approval" src tests external/codex`

## Research and validation docs

- `docs/codex-0.106-to-0.118-interop.md`
- `docs/Runbooks/Manual-Testing/Exec.md`
- `docs/Runbooks/Manual-Testing/AppServer.md`
- `docs/AppServer/README.md`

Use these when you need the previous upgrade research, manual smoke-test flows, or the public app-server surface description.

## Practical command set

- Discover current state:
  - `python .codex/skills/codex-sdk-parity-pass/scripts/parity_context.py`
  - `git log --oneline --decorate -n 30`
  - `git -C external/codex describe --tags --always`
- Research upstream:
  - `gh release list -R openai/codex --limit 20`
  - `gh release view rust-v<version> -R openai/codex`
  - `git -C external/codex log --oneline rust-v<from>..rust-v<to>`
  - `git -C external/codex diff --stat rust-v<from>..rust-v<to>`
- Regenerate when schemas changed:
  - `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
  - `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- Validate:
  - focused `dotnet test ... --filter ...`
  - `dotnet test JKToolKit.CodexSDK.sln --configuration Release`
  - `gh pr checks <number>`
