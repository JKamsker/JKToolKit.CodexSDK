Below is a concrete integration plan that folds **`JKToolKit.CodexSDK.AppServer`** and **`JKToolKit.CodexSDK.McpServer`** into the **core `JKToolKit.CodexSDK`** package/assembly, while keeping the existing **`codex exec`** (“exec approach”) working exactly as it does today.

I’m basing this on the current repo layout where:

* `src/JKToolKit.CodexSDK` already contains shared infra like **`StdioProcess`**, **`StdioProcessFactory`**, and **`JsonRpcConnection`** (currently shared via `InternalsVisibleTo`).
* `src/JKToolKit.CodexSDK.AppServer` and `src/JKToolKit.CodexSDK.McpServer` are separate projects primarily for packaging / API surface, not because they need distinct infra.

---

## 1) Goals and constraints

### Must-haves

1. **Single “core” library/package contains all three modes**:

   * `codex exec` (existing `CodexClient` pipeline)
   * `codex app-server`
   * `codex mcp-server`
2. **Exec approach remains available exactly as before**:

   * No breaking changes to `CodexClient`, `CodexSessionHandle`, `CodexSessionOptions`, streaming JSONL tailing, etc.
3. **Shared infrastructure**:

   * No duplicated process launch logic, JSON-RPC plumbing, logging/cancellation patterns.
4. **Similar developer experience across modes**:

   * Similar startup patterns (`StartAsync` / factory), option naming consistency, cancellation + disposal semantics.

### Nice-to-haves

* Keep the existing add-on NuGet package IDs as compatibility shims (so downstream users don’t break immediately).
* Improve option type reuse and DI wiring so “configure Codex path once” works for all modes.

---

## 2) Target end state: one assembly/package, same namespaces

### Recommended approach

**Move AppServer and McpServer code into the core `JKToolKit.CodexSDK` project** *without changing namespaces*:

* Keep public namespaces:

  * `JKToolKit.CodexSDK.Exec` (exec)
  * `JKToolKit.CodexSDK.AppServer`
  * `JKToolKit.CodexSDK.McpServer`

That way, user code that already does:

```csharp
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.Exec;
```

doesn’t need to change; only the **package references** change.

### Repo structure after move

Inside `src/JKToolKit.CodexSDK`:

```
src/JKToolKit.CodexSDK/
  Public/                 // exec API (unchanged)
  AppServer/              // moved from JKToolKit.CodexSDK.AppServer
  McpServer/              // moved from JKToolKit.CodexSDK.McpServer
  Infrastructure/
    JsonRpc/              // already present
    Stdio/                // already present
    ...                   // existing exec infra
```

---

## 3) Packaging strategy (and keeping compatibility)

You have two viable strategies; I recommend **B**.

### A) Hard merge (breaking packaging change)

* Delete/stop packing `NCodexSDK.AppServer` and `NCodexSDK.McpServer`
* Only ship `NCodexSDK`
* Users must remove the add-on package references

This is simplest but will break people who reference the add-ons explicitly.

### B) Soft merge (best UX)

* Ship **one “real” package**: `NCodexSDK` containing everything.
* Keep shipping `NCodexSDK.AppServer` and `NCodexSDK.McpServer` as **shim packages** that:

  * depend on `NCodexSDK`
  * contain **type-forwarders** to the types now living in `JKToolKit.CodexSDK`

This preserves:

* source compatibility (same namespaces)
* binary compatibility (assemblies that referenced the old package still resolve)

**Implementation detail**: In the shim projects, remove all code except `TypeForwardedTo` attributes (plus minimal readme + package metadata). This avoids duplicating anything.

---

## 4) Share infrastructure properly (remove duplicated “StartAsync” bootstrapping)

Right now both `CodexAppServerClient.StartAsync(...)` and `CodexMcpServerClient.StartAsync(...)`:

* construct their own `RealFileSystem`, `DefaultCodexPathProvider`, `StdioProcessFactory`
* create `JsonRpcConnection`
* run handshake

And the DI factories do a very similar thing again.

### Plan: introduce a shared internal “stdio JSON-RPC client bootstrap”

Add a single internal helper in core, e.g.:

* `Infrastructure/StdioJsonRpc/CodexStdioJsonRpcBootstrap.cs`

It should:

1. Start the stdio process via `StdioProcessFactory`
2. Create a `JsonRpcConnection`
3. Attach a standard server-request dispatcher
4. Return `(process, rpc)` to the mode-specific client for handshake + routing

#### Common options interface (internal)

Create an internal interface implemented by both option types:

```csharp
internal interface ICodexStdioJsonRpcOptions
{
    CodexLaunch Launch { get; }
    string? CodexExecutablePath { get; }
    TimeSpan StartupTimeout { get; }
    TimeSpan ShutdownTimeout { get; }
    JsonSerializerOptions? SerializerOptionsOverride { get; }
    int NotificationBufferCapacity { get; }
}
```

Then both:

* `CodexAppServerClientOptions`
* `CodexMcpServerClientOptions`

implement it (no public surface change needed).

#### Common bootstrap entry point

Something like:

```csharp
internal static class CodexStdioJsonRpcBootstrap
{
    public static async Task<(StdioProcess process, JsonRpcConnection rpc)> StartAsync(
        ICodexStdioJsonRpcOptions options,
        bool includeJsonRpcHeader,
        StdioProcessFactory stdioFactory,
        ILoggerFactory loggerFactory,
        CancellationToken ct);
}
```

This removes duplication in:

* both clients’ `StartAsync`
* both DI factories

---

## 5) Unify “server initiated request” handling (approval vs elicitation)

Today you have:

* `IAppServerApprovalHandler`
* `IMcpElicitationHandler`

They are already identical signatures:

```csharp
ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct);
```

### Plan: extract a shared base interface

In core (new):

```csharp
public interface ICodexServerRequestHandler
{
    ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct);
}
```

Then:

* `IAppServerApprovalHandler : ICodexServerRequestHandler`
* `IMcpElicitationHandler : ICodexServerRequestHandler`

Internally, the JSON-RPC dispatcher only depends on `ICodexServerRequestHandler`.

This achieves two things:

* shared infra for handling server requests
* “similar to use” across modes (same conceptual hook)

You still keep the mode-specific names (`ApprovalHandler` vs `ElicitationHandler`) if you want the semantic clarity, but the machinery is unified.

---

## 6) Unify ClientInfo models (optional but recommended)

You currently have two identical records:

* `AppServerClientInfo(name,title,version)`
* `McpClientInfo(name,title,version)`

### Plan

Create one shared public record:

* `JKToolKit.CodexSDK.Exec.CodexClientInfo`

Then choose one of:

* **Non-breaking**: keep both existing records, but allow implicit conversion or map internally
* **Breaking (minor)**: replace both with `CodexClientInfo` and update options to use it

Given you want “similar usage”, I’d do **non-breaking** initially:

* Add `CodexClientInfo`
* Keep old ones but mark `[Obsolete("Use CodexClientInfo")]` later

---

## 7) Make DI registration shared and consistent

Right now you have 3 separate DI entry points:

* `AddCodexClient()`
* `AddCodexAppServerClient()`
* `AddCodexMcpServerClient()`

Each repeats registration of:

* `IFileSystem`
* `ICodexPathProvider`
* `StdioProcessFactory`

### Plan: introduce one internal “AddCodexCoreInfrastructure”

In core, create:

```csharp
internal static class CodexServiceRegistration
{
    internal static void AddCoreInfrastructure(IServiceCollection services)
    {
        services.TryAddSingleton<IFileSystem, RealFileSystem>();
        services.TryAddSingleton<ICodexPathProvider, DefaultCodexPathProvider>();
        services.TryAddSingleton<StdioProcessFactory>();
        // any other shared bits
    }
}
```

Then:

* `AddCodexClient` calls `AddCoreInfrastructure` before registering JSONL/exec bits
* `AddCodexAppServerClient` calls `AddCoreInfrastructure` before registering factory
* `AddCodexMcpServerClient` calls `AddCoreInfrastructure` before registering factory

### Optional: add a unified DI method

Add:

* `services.AddCodexSdk(...)`

that registers **everything**, for people who want “one call and done”.

Keep the existing methods too.

---

## 8) Align “similar to use” at the API level

You can’t make `exec`, `app-server`, and `mcp-server` identical because they are different protocols. But you can make them feel consistent:

### Consistency rules

1. **Startup**: all should support

   * `StartAsync(options, ct)`
   * DI factory `ICodex…ClientFactory.StartAsync(ct)`
2. **Escape hatch**: all should expose

   * `CallAsync(method, params, ct)` for forward compatibility
3. **Lifecycle**: all are `IAsyncDisposable` and dispose closes stdin then kills after timeout.

### Suggested small refinements

* Ensure both server clients expose `Notifications(...)` (even if MCP mostly doesn’t need it). If MCP doesn’t have meaningful notifications, it can just surface raw JSON-RPC notifications for parity.
* Consider normalizing the default option property names:

  * `ClientInfo` (same property name in both options)
  * `ServerRequestHandler` (alias property that maps to Approval/Elicitation)
  * Keep old properties as aliases so you don’t break current users.

---

## 9) Work plan (implementation checklist)

This is the step-by-step breakdown you can hand to someone and execute in PRs.

### PR 1 — Move code into core project

* Move files:

  * `src/JKToolKit.CodexSDK.AppServer/**.cs` → `src/JKToolKit.CodexSDK/AppServer/**.cs`
  * `src/JKToolKit.CodexSDK.McpServer/**.cs` → `src/JKToolKit.CodexSDK/McpServer/**.cs`
* Update `JKToolKit.CodexSDK.csproj` if needed (SDK-style includes files by default).
* Remove `InternalsVisibleTo("JKToolKit.CodexSDK.AppServer")` and `InternalsVisibleTo("JKToolKit.CodexSDK.McpServer")` from `JKToolKit.CodexSDK/Properties/AssemblyInfo.cs` (they’ll be in the same assembly now).
* Fix build errors (mostly namespace/usings and internal visibility that’s no longer needed).

✅ Exec remains untouched.

---

### PR 2 — Introduce shared stdio JSON-RPC bootstrap + refactor both clients/factories

* Add:

  * `ICodexStdioJsonRpcOptions` (internal)
  * `CodexStdioJsonRpcBootstrap` (internal)
* Refactor:

  * `CodexAppServerClient.StartAsync` and `CodexAppServerClientFactory.StartAsync`
  * `CodexMcpServerClient.StartAsync` and `CodexMcpServerClientFactory.StartAsync`
* Ensure the only mode-specific logic left is:

  * includeJsonRpcHeader true/false
  * handshake payload + initialized notification name
  * app-server’s per-turn routing

✅ Infra now truly shared.

---

### PR 3 — DI unification

* Add internal `AddCoreInfrastructure(...)`
* Update:

  * `AddCodexClient`
  * `AddCodexAppServerClient`
  * `AddCodexMcpServerClient`
* Optionally add:

  * `AddCodexSdk(...)` that calls all three

✅ One consistent infrastructure registration.

---

### PR 4 — Packaging strategy

If you choose **soft merge**:

* Change `JKToolKit.CodexSDK.AppServer.csproj` and `JKToolKit.CodexSDK.McpServer.csproj`:

  * remove all compiled sources
  * add type-forwarders
  * keep package metadata/readme
  * add dependency on `NCodexSDK`
  * mark package as deprecated (NuGet metadata)
* Update solution + demos to reference only `NCodexSDK`

If you choose **hard merge**:

* Mark add-on projects `<IsPackable>false</IsPackable>`
* Remove “optional add-ons” from root README and docs

✅ Consumers get everything from core; old add-ons can still work if you keep shims.

---

### PR 5 — Docs and demos

* Update root README:

  * installation: only `dotnet add package NCodexSDK`
  * still show 3 modes
* Update `*.Demo` projects to reference only `JKToolKit.CodexSDK` project (or just the package).
* Ensure examples look consistent (same general patterns).

---

### PR 6 — Tests

* Update tests to reference the new locations/namespaces if required (ideally no change if namespaces stay).
* Ensure E2E tests for app-server/mcp-server still run when Codex CLI is available.

---

## 10) What stays exactly the same for `exec`

To explicitly satisfy your constraint:

* `CodexClient`, `CodexClientOptions`, `CodexSessionOptions`, JSONL tailing/parsing, session locating/resuming—**do not change**.
* The `codex exec` “pipeline” remains the primary “core session” API and continues to work as before.
* Integration is additive: app-server and mcp-server become **additional capabilities inside the same package**.

---

## 11) Resulting developer experience

After integration, a user installs only:

```bash
dotnet add package NCodexSDK
```

And can do either:

### Exec (unchanged)

```csharp
await using var client = new CodexClient(new CodexClientOptions());
await using var session = await client.StartSessionAsync(new CodexSessionOptions(repo, prompt));
await foreach (var e in session.GetEventsAsync()) { ... }
```

### App-server (same API, same namespace)

```csharp
await using var codex = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions());
...
```

### MCP server (same API, same namespace)

```csharp
await using var codex = await CodexMcpServerClient.StartAsync(new CodexMcpServerClientOptions());
...
```

Same package; similar start/dispose; shared infra.

---

If you want, I can also outline a “nice” optional façade (`CodexRuntime`/`CodexSdk`) that lets you configure the Codex binary path + logger once and then start any mode from the same root object—but the plan above already achieves your integration goals without forcing an API redesign.
