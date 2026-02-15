# Plan: Stable-only Strategy (Keep Working Without Experimental Opt-In)

This plan describes how to keep `JKToolKit.CodexSDK` working against newer upstream Codex versions **without** requiring `experimentalApi` opt-in.

It assumes:

- The default app-server initialization keeps `capabilities.experimentalApi = false` (or omitted).
- The SDK avoids unintentionally “using” experimental fields/methods (which upstream app-server rejects).
- When callers *attempt* to use experimental-only features, the SDK fails early with clear guidance.

---

## 1) Goals

### Must-haves

- [x] **No regressions for the common stable path** against newer Codex:
   - `initialize` + `initialized`
   - `thread/start` (stable subset)
   - `thread/resume` by `threadId` (stable subset)
   - `turn/start` (stable subset; no `collaborationMode`)
   - `turn/interrupt`
- [x] **No silent “experimental usage”**:
   - Don’t send experimental fields by default in a way that triggers upstream gating.
- [x] **Helpful failures** when users set experimental-only inputs:
   - Throw an exception that names the missing capability and how to enable it.

### Non-goals

- Adding new app-server APIs (covered in separate plans).
- Automatically enabling experimental mode (must remain an explicit choice).

---

## 2) Current SDK surfaces that can hit experimental gating (known)

These are *capability-sensitive* when talking to upstream Codex app-server:

1. `thread/resume.history` and `thread/resume.path`
   - The SDK currently exposes these in:
     - `src/JKToolKit.CodexSDK/AppServer/ThreadResumeOptions.cs`
     - `src/JKToolKit.CodexSDK/AppServer/Protocol/V2/ThreadResumeParams.cs`
   - Upstream treats both as experimental fields (requires `experimentalApi` when present).

2. `turn/start.collaborationMode`
   - The SDK currently exposes this as raw JSON in:
     - `src/JKToolKit.CodexSDK/AppServer/Protocol/V2/TurnStartParams.cs`
   - Upstream treats this as an experimental field (requires `experimentalApi` when set/non-null).

3. Experimental booleans on `thread/start` (not gated unless enabled)
   - Example: `experimentalRawEvents`
   - Upstream gating generally triggers only when `true`, but we should validate the exact behavior.

> Note: Upstream’s experimental gating logic is field-aware; for booleans it typically triggers only when the value is `true`.

### 2.1 Compatibility matrix (stable vs experimental)

This table is intended to drive *documentation*, *guardrails*, and *tests*.

| Capability / behavior | Upstream descriptor (example) | SDK surface today | Stable-only supported? | Notes |
|---|---|---:|:---:|---|
| Initialize app-server without experimental | (none) | `CodexAppServerClient.InitializeAsync(...)` | ✅ | Default path. |
| Start a thread | (stable) | `CodexAppServerClient.StartThreadAsync(...)` | ✅ | Must avoid accidental experimental fields. |
| Resume a thread by threadId | (stable) | `CodexAppServerClient.ResumeThreadAsync(threadId)` | ✅ | Safe stable subset. |
| Resume a thread by history | `thread/resume.history` | `ThreadResumeOptions.History` | ❌ | Must guard/throw unless experimental opt-in enabled. |
| Resume a thread by rollout path | `thread/resume.path` | `ThreadResumeOptions.Path` | ❌ | Must guard/throw unless experimental opt-in enabled. |
| Fork a thread by rollout path | `thread/fork.path` | `ThreadForkOptions.Path` | ❌ | Must guard/throw unless experimental opt-in enabled. |
| Start a turn normally | (stable) | `CodexAppServerClient.StartTurnAsync(...)` | ✅ | Stable path: no collaboration mode. |
| Start a turn with collaboration mode | `turn/start.collaborationMode` | `TurnStartParams.CollaborationMode` | ❌ | Must guard/throw unless experimental opt-in enabled. |
| Emit raw response items | `thread/start.experimentalRawEvents` (when true) | `ThreadStartOptions.ExperimentalRawEvents` | ✅* | Stable-only should allow `false` (default). `true` should require opt-in or be treated as experimental. |

`✅*` = supported only in its stable configuration (typically the default / “off” value).

---

## 3) Proposed “stable-only” behavior and guardrails

### 3.1 Validate and fail fast (recommended)

Add client-side validation so the SDK throws before sending a request that is known to require `experimentalApi`.

Example checks (conceptual):

- If `ThreadResumeOptions.History` is not null → throw `NotSupportedException`:
  - “`thread/resume.history` requires app-server experimental API; enable `experimentalApi` in initialize capabilities.”
- If `ThreadResumeOptions.Path` is not null/empty → same.
- If `TurnStartParams.CollaborationMode` is not null → same.

Why this helps:

- Users get an actionable error without depending on server-specific error message parsing.
- Stable-only behavior is predictable.

Where to implement:

- At *options layer*:
  - `src/JKToolKit.CodexSDK/AppServer/ThreadResumeOptions.cs`
  - (and/or) at request construction in `src/JKToolKit.CodexSDK/AppServer/CodexAppServerClient.cs`
- Optionally in protocol DTOs (less ideal; DTOs should be dumb).

### 3.2 Keep “escape hatch” available

The SDK already exposes:

- `CodexAppServerClient.CallAsync(string method, object? params, ...)`

Stable-only strategy should keep this intact, but document that:

- Calling experimental methods/fields via `CallAsync` still requires experimental opt-in.

### 3.3 Don’t accidentally “activate” experimental fields

Audit serialization defaults:

- Ensure null properties aren’t serialized when null (or if they are, ensure server doesn’t treat `"foo": null` as “field used”).
- Ensure experimental bools default to `false` and do not trip gating.

Concrete audit items:

- [x] `ThreadStartParams.ExperimentalRawEvents` is currently a non-nullable bool.
  - [x] Confirm upstream gating triggers only when true.
  - [x] If upstream gating changes to “field present”, consider making it nullable (`bool?`) or adding `JsonIgnore(WhenWritingDefault)` logic.

### 3.4 Implementation milestones (stable-only)

- [x] **Document stable vs experimental**
  - [x] Add a section to `docs/AppServer/README.md` with the matrix above.
- [x] **Add client-side guardrails**
  - [x] Throw early when callers set experimental-only fields without opt-in.
- [x] **Add tests**
  - [x] Unit tests verifying guardrails and default request shapes.

---

## 4) Documentation changes (stable-only guidance)

Update app-server docs to clearly separate:

- **Stable-only supported usage** (works without experimental opt-in)
- **Experimental-only usage** (requires opt-in; see plan 02)

Places to update:

- [x] `docs/AppServer/README.md`
- [x] Public XML docs on:
  - `ThreadResumeOptions.History`
  - `ThreadResumeOptions.Path`
  - `TurnStartParams.CollaborationMode`

Add a “Troubleshooting” entry:

- [x] Error: `"<descriptor> requires experimentalApi capability"`
  - [x] Explanation: upstream gating
  - [x] Fix: enable opt-in (link to plan 02)

---

## 5) Testing strategy

### 5.1 Unit tests (preferred)

Add tests around client-side validation:

- [x] When experimental-only properties are set, the SDK throws with a clear message *before* sending JSON-RPC.

Best test seam:

- [x] For `CodexAppServerClient`, inject a fake `JsonRpcConnection` or wrap request building behind an interface.
- [x] If that is too heavy, validate at options level and unit test the options validation method(s).

### 5.2 Integration tests (optional / gated)

If your CI or local env can run Codex:

- [x] Start `codex app-server` and validate stable flows:
  - [x] Start thread → start turn → observe notifications → interrupt
  - [x] Resume thread by threadId

Guard with env var (consistent with existing style in repo).

---

## 6) Acceptance criteria

- [x] Against latest upstream Codex:
  - [x] Stable path works end-to-end with no experimental opt-in.
- [x] SDK fails fast (client-side) for:
  - [x] `thread/resume.history`
  - [x] `thread/resume.path`
  - [x] `turn/start.collaborationMode`
- [x] Docs clearly explain:
  - [x] What is stable
  - [x] What is experimental
  - [x] How to enable experimental safely

---

## 7) Open questions / decisions

- [x] Do we want a single “capabilities” knob in `CodexAppServerClientOptions`, or separate boolean `EnableExperimentalApi`?
- [x] Decide client-side validation behavior: strict (throw) vs permissive (silently ignore). Chosen: **strict** (throw).
