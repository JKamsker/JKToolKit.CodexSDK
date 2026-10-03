# Thread, turn, and result API

```csharp
using JKToolKit.CodexSDK;

await using var sdk = await CodexSdk.StartAsync();
var thread = await sdk.Threads.StartAsync(new() { Cwd = repo });
var result = await thread.RunAsync("Fix the failing tests");
Console.WriteLine(result.FinalResponse);
```

`CodexSdk.Create()` remains lazy; `StartAsync()` initializes the shared app-server
connection immediately. The SDK owns that connection and disposes it even when
its exec client was supplied by dependency injection. Existing `Exec`,
`AppServer`, and `McpServer` entry points remain available with their existing
ownership rules.

Use `sdk.Threads.ResumeAsync(threadId)` for a persisted conversation. Sessions
for the same thread on this SDK share an active-turn guard: overlapping runs
are rejected; use the active handle's `SteerAsync` intentionally instead.
`RunAsync(TurnStartOptions)` and `RunStreamedAsync(TurnStartOptions)` expose the
existing model, sandbox, approvals, output-schema and other turn settings.

## Results and streaming

`RunAsync` returns `CodexTurnResult`, including `FinalResponse`, typed `Items`,
typed `Usage`, `Status`, `Error`, `Diff`, `ThreadId`, `TurnId`, and `TerminalTurn`.
Unknown item types preserve their raw JSON. The last `final_answer` message is
preferred; older servers' unlabelled messages are a fallback. Commentary alone
is not treated as a final answer. Missing usage or diff remains null.
`Usage.Last` means the last model request, not the sum of a multi-request turn;
`Usage.Total` is cumulative thread usage. Counters are nullable 64-bit values.

Failed and interrupted turns return their server status/error. Transport
failures throw; disposing an unfinished handle or its connection cancels
collection. The result accumulator is independent of `Events()` and observer
queues. Extremely early notifications can overflow the pre-registration
buffer; such a result sets `IsPartial` so consumers can detect missing data and
read the persisted thread through the raw client if necessary.

```csharp
await using var turn = await thread.RunStreamedAsync("Explain this repository");
await using var ui = turn.Subscribe();
await using var logger = turn.Subscribe(capacity: 2048);
// Enumerate ui.Events() and logger.Events() independently/concurrently.
var result = await turn.RunAsync(); // Repeatable; does not consume their events.
```

`Subscribe()` registers immediately and broadcasts **future** typed events; it
does not replay notifications from before subscription. Every subscription has
its own bounded drop-oldest queue and `DroppedEvents` counter. Late subscriptions
finish immediately. Dispose subscriptions when observers leave. Each
subscription should have one reader. `Events()` and `EventsRaw()` keep their
existing competing-reader semantics.

Canceling `turn.RunAsync(ct)` only cancels that wait. Canceling the high-level
`thread.RunAsync(..., ct)` requests interruption (with a five-second timeout),
then disposes the handle. After `RunStreamedAsync` returns, its startup token
has no effect on the running turn; call `InterruptAsync` explicitly. Disposing
a raw turn handle does not interrupt the server. Await server completion before
starting another turn on a thread whose handle you disposed.

If cancellation arrives while startup is in flight, the caller returns promptly
and the SDK retains the startup response so it can interrupt the accepted turn.
The thread remains reserved until the server reports completion or the connection
closes, including when interruption fails. Disposing a high-level handle also
preserves this guard until server completion.

## Trust-labelled external input

```csharp
var result = await thread.RunAsync(
    CodexInput.ExternalMessage("github", untrustedIssueText));
```

This sends standalone `toolOutput` with an empty user-input list, preserving
tool-level authority. It does not merely wrap content in prompt delimiters.
The source name is a label, not authentication or user authorization. One
external message is the complete input to a turn; mixing it with user input is
not supported. Raw `TurnToolOutput` supports namespaces and structured content.
Keep the thread's existing sandbox and approval policy appropriate for the task.

## Runtime packages and preflight

Opt into the package matching the application's target process architecture:

```xml
<PackageReference Include="JKToolKit.CodexSDK.Runtime.linux-x64" Version="0.160.0" />
```

Available RIDs: `linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`, `osx-x64`,
`osx-arm64`. Runtime package versions follow the Codex pin, independently from
the SDK's release number. These are large optional packages containing the
native runtime, bundled helper programs, and upstream notices. No runtime is
downloaded during application startup. Runtime targets copy their contents to
build and publish output and restore Unix executable permissions. Include the
runtime directory alongside single-file deployments; it is not embedded into
the application executable. Cross-publishing from Windows to Unix requires
preserving/restoring executable permissions during deployment.

Resolution is **explicit executable path → bundled matching version/RID →
PATH**. Custom `CodexLaunch` commands and remote endpoints retain their explicit
launch behavior. Bundles for other SDK pins or architectures are ignored.
The runtime packaging script verifies a committed SHA-512 lock before extracting
any executable and preserves the official package's relative resource layout.

```csharp
await using var sdk = CodexSdk.Create();
var info = await sdk.Runtime.GetInfoAsync();
Console.WriteLine($"Expected {info.ExpectedVersion}; found {info.ActualVersion}");
foreach (var diagnostic in info.Diagnostics) Console.WriteLine(diagnostic);
```

Use `includeServer: false` for executable-only preflight. The default also
initializes the shared connection and reads account status without refreshing
tokens or starting a model turn. Missing binaries, version mismatches, probe
failures, and unavailable probes appear in `Diagnostics`; caller cancellation
propagates. Version differences are not an automatic compatibility verdict.
Remote/custom launches report unknown local executable/version rather than
probing an unrelated PATH binary. Initialize metadata is preserved;
`Capabilities == null` means the server did not advertise capabilities, not that
it supports none. Account information may contain personal data; avoid logging
it indiscriminately.

Executable preflight supports Windows `.cmd` and `.bat` shims through the system
command interpreter, with AutoRun and delayed expansion disabled. Native binaries
are launched directly. The probe retains the selected shim path in `ExecutablePath`.

## Middleware and telemetry

Register `ICodexTurnMiddleware` through `builder.Use(middleware)`. Middleware
wraps both collected and streaming high-level startup in registration order
(first registered is outermost). Call `next` once, inspect/configure
`CodexTurnContext.Options`, or attach observers to the returned handle. Exceptions
propagate; if middleware fails after starting a turn, the SDK requests interruption
and retains ownership until server completion. Return the handle obtained from
`next`. Observers attached after startup do not replay early notifications.
These hooks do not replace the raw clients' response/notification transformers.

Register `JKToolKit.CodexSDK` with OpenTelemetry's `AddSource` and `AddMeter`.
`CodexTelemetry.ActivitySource` creates `codex.turn` client spans that end on
terminal completion, transport failure, or handle disposal, including raw app-server calls made by AgentFramework and
SemanticKernel adapters. Spans inherit the current activity and contain model,
thread/turn IDs, status, and exception type. Prompts, responses, paths, account
details, and error messages are excluded. `codex.turn.duration` (seconds) and
`codex.turn.count` metrics have bounded outcome tags. The SDK does not install an
exporter or require an OpenTelemetry dependency.

## Compatibility and validation

`PublicAPI.Shipped.txt` baselines the existing facade/turn-handle surface;
`PublicAPI.Unshipped.txt` records additions. PublicApiAnalyzers fail builds for
unrecorded additions and removed signatures. Generated protocol DTOs have their
own upstream-generation check and are outside this consumer API baseline.
For intentional additions, run:

```sh
dotnet format analyzers src/JKToolKit.CodexSDK/JKToolKit.CodexSDK.csproj --diagnostics RS0016 --severity error
```

Review the API diff; do not delete shipped declarations to bypass compatibility
errors. CI runs the suite on Linux, Windows, and macOS, builds all six runtime
packages, and exercises restored and published consumers on all three OSes.
Deterministic random-input tests exercise notification parsing and fragmented
Unicode transport frames without network access or model credentials.

To refresh runtime artifacts after a pin change, run
`python3 scripts/pack-codex-runtime.py --update-lock`, review the registry URLs
and integrity hashes, update the smoke sample's runtime version, then pack.
See [the executable smoke sample](../samples/HighLevelSmoke/Program.cs) for a
preflight-only run or opt into live model calls with `--live`.
