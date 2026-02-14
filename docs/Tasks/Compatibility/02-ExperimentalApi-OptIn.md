# Plan: Add Explicit `experimentalApi` Opt-In (App-Server)

Upstream Codex app-server increasingly gates fields and methods behind an initialize-time capability:

- `initialize.params.capabilities.experimentalApi = true`

This plan adds a *clear, explicit, safe-by-default* opt-in in `JKToolKit.CodexSDK.AppServer`.

---

## 1) Goals

### Must-haves

- [ ] **Opt-in is explicit** and disabled by default.
- [ ] **SDK can successfully use experimental-gated fields/methods** when enabled, including:
   - `thread/resume.history`
   - `thread/resume.path`
   - `turn/start.collaborationMode`
   - (and future experimental additions)
- [ ] **Good UX**:
   - Clear option naming
   - Clear exceptions when not enabled
   - Docs that explain tradeoffs (unstable surface)

### Non-goals

- Automatically enabling experimental mode.
- Guaranteeing API stability of experimental features (upstream explicitly does not).

---

## 2) Proposed public surface changes (SDK)

### 2.1 Extend initialize capabilities model

Current:

- `src/JKToolKit.CodexSDK/AppServer/Protocol/Initialize/InitializeCapabilities.cs`
  - has only `experimentalApi: bool`

Upstream also added capabilities like `optOutNotificationMethods` (list of method strings).

Proposed:

- [ ] Add optional properties:
  - [ ] `bool ExperimentalApi { get; init; }`
  - [ ] `IReadOnlyList<string>? OptOutNotificationMethods { get; init; }`

Design notes:

- Keep this as a wire DTO (raw string methods).
- Default should be null/empty → send nothing unless set.

### 2.2 Add capability configuration in options

Add to:

- `src/JKToolKit.CodexSDK/AppServer/CodexAppServerClientOptions.cs`

Proposed options field(s):

- [ ] `InitializeCapabilities? Capabilities { get; set; }`

Defaults:

- `Capabilities = null` (stable-only by default)

Alternatively (more discoverable):

- [ ] `bool ExperimentalApi { get; set; } = false;`
- [ ] plus optional `OptOutNotificationMethods`

Recommendation:

- Prefer `Capabilities` object so the options can evolve without adding many booleans.

### 2.3 Plumb capabilities through initialize

Update:

- `src/JKToolKit.CodexSDK/AppServer/CodexAppServerClient.cs`

Current behavior:

- `InitializeAsync(...): new InitializeParams { ClientInfo = clientInfo, Capabilities = null }`

Proposed behavior:

- [ ] Pass options capabilities:
  - [ ] `Capabilities = _options.Capabilities` (or derived from other option fields)

### 2.4 Interaction with “stable-only” guardrails

If we implement the guardrails from plan 01 (recommended), they must become conditional:

- If `Capabilities?.ExperimentalApi == true`:
  - allow experimental-only fields/methods (and document they may break).
- Else:
  - throw early with actionable guidance.

This keeps:

- safe defaults for typical users
- a clear “power user” knob for advanced/unstable features

### 2.5 Notification opt-out (capabilities)

Upstream supports an `optOutNotificationMethods` capability to reduce notification volume.

Proposed SDK behavior:

- Expose the field verbatim as a list of method strings.
- Do not attempt to validate the list (upstream can add/remove methods).

Suggested doc guidance:

- Use opt-out only if the client *does not* rely on the notification.
- Prefer opting out of high-volume deltas if the client only needs turn completion:
  - `item/agentMessage/delta`
  - `item/plan/delta`
  - `item/reasoning/textDelta`
  - etc. (keep examples illustrative; upstream evolves)

---

## 3) Backward/forward compatibility considerations

### 3.1 Older app-server versions

Potential risk:

- Very old app-server builds might reject unknown fields inside `capabilities`.

Mitigations:

- [ ] Keep capabilities omitted by default (null).
- [ ] Keep capability objects minimal and only include fields explicitly set.
- [ ] If initialize fails, surface the raw JSON-RPC error as-is plus add an optional “help” suffix.

### 3.2 Mixed capability usage

Upstream notes this is negotiated once per process lifetime; re-initialize is rejected.

SDK behavior:

- Continue to do a single initialize call during `StartAsync`.
- Do not attempt re-init.

### 3.3 Error handling strategy

Upstream error message shape typically includes:

- `"<descriptor> requires experimentalApi capability"`

SDK strategy:

1. Prefer client-side validation (plan 01) so we fail before sending.
2. Still add a server-error fallback:
   - If we receive the experimental-capability error from app-server, wrap it with:
     - the descriptor string (when extractable)
     - a hint: “Enable `CodexAppServerClientOptions.Capabilities.ExperimentalApi`”

---

## 4) Developer experience (how callers use it)

Example desired usage:

```csharp
var options = new CodexAppServerClientOptions
{
    Capabilities = new InitializeCapabilities
    {
        ExperimentalApi = true,
        OptOutNotificationMethods = new[]
        {
            // Example: reduce notification volume for clients that don’t need deltas
            "item/agentMessage/delta"
        }
    }
};

await using var client = await CodexAppServerClient.StartAsync(options);
```

### 4.1 Examples of features unlocked by opt-in (non-exhaustive)

- Resume by rollout path (`thread/resume.path`)
- Resume by raw history (`thread/resume.history`)
- Collaboration modes (`turn/start.collaborationMode`)
- Future experimental fields/methods as upstream evolves (thread background terminal cleanup, fuzzy search sessions, etc.)

---

## 5) Testing strategy

### 5.1 Unit tests (request serialization)

Validate that:

- [ ] When `Capabilities == null`, initialize params do not include it (or include null consistently).
- [ ] When `Capabilities.ExperimentalApi == true`, initialize params include:
  - [ ] `"capabilities": { "experimentalApi": true }`
- [ ] When `OptOutNotificationMethods` set, it serializes as expected.

Suggested approach:

- [ ] Unit-test the object being passed to `_rpc.SendRequestAsync("initialize", ...)` by
  extracting an internal helper that builds the `InitializeParams`.

### 5.2 Integration tests (optional)

With a real app-server:

- [ ] Start client with experimental enabled
- [ ] Use one experimental-gated field (e.g. resume by `path`) and assert it no longer errors.

Guard with env var so tests are opt-in.

---

## 8) Implementation milestones (experimental opt-in)

- [ ] **Wire capabilities through initialize**
  - [ ] Add `CodexAppServerClientOptions.Capabilities`
  - [ ] Plumb into `InitializeAsync(...)`
- [ ] **Update DTOs**
  - [ ] Extend `InitializeCapabilities` with `OptOutNotificationMethods`
- [ ] **Adjust guardrails**
  - [ ] Make stable-only checks conditional on `ExperimentalApi == true`
- [ ] **Docs**
  - [ ] Add a “Capabilities” section and examples
- [ ] **Tests**
  - [ ] Unit tests for initialize payload construction
  - [ ] Optional integration test for one experimental field

---

## 6) Acceptance criteria

- [ ] Default behavior remains stable-only (no opt-in).
- [ ] Enabling experimental:
  - [ ] sets the capability at initialize
  - [ ] unblocks known gated fields (`thread/resume.history`, `thread/resume.path`, `turn/start.collaborationMode`)
- [ ] Docs clearly warn experimental usage is unstable and may break with upstream updates.

---

## 7) Follow-ups (nice-to-haves)

- [ ] Parse upstream error messages and translate them into typed exceptions:
  - [ ] e.g. `CodexExperimentalApiRequiredException` with `Descriptor = "turn/start.collaborationMode"`
- [ ] Add a “capabilities compatibility” section in `docs/AppServer/README.md`:
  - [ ] stable vs experimental, and how to opt in.
