# Plan: Sandbox/Permissions + Read-Only Access (App-Server)

Upstream Codex has expanded sandbox and permissions concepts across:

- sandbox policies (disk read/write boundaries)
- network access and proxy/runtime behavior
- explicit read-only access control (`ReadOnlyAccess`) for sandbox modes
- permissions-related events/notifications and config requirements

This plan focuses on exposing *the high-value subset* in `JKToolKit.CodexSDK.AppServer` while keeping the SDK forward compatible.

---

## 1) Goals

### Must-haves

- [x] Expose upstream read-only access controls in the app-server `SandboxPolicy` types:
   - `ReadOnlyAccess` union (`fullAccess` vs `restricted`)
   - attach to:
     - `SandboxPolicy.ReadOnly` (as `access`)
     - `SandboxPolicy.WorkspaceWrite` (as `readOnlyAccess`)
- [x] Keep defaults backward compatible:
  - [x] do not send new fields unless explicitly set
- [x] Improve docs around sandbox/permissions tradeoffs.

### Nice-to-haves

- [x] Surface network requirements/proxy runtime details as typed models
- [x] Add helpers that map high-level SDK options to sandbox policy objects

---

## 2) Current SDK state

Existing wire sandbox types live under:

- `src/JKToolKit.CodexSDK/AppServer/Protocol/SandboxPolicy/`

Today:

- `SandboxPolicy.ReadOnly` has only `{ type: "readOnly" }`
- `SandboxPolicy.WorkspaceWrite` supports:
  - `writableRoots`
  - `networkAccess`
  - `excludeTmpdirEnvVar`, `excludeSlashTmp`
- `SandboxPolicy.ExternalSandbox` supports `networkAccess` string

---

## 3) Proposed model additions

### 3.1 Add `ReadOnlyAccess` model (wire-compatible)

Add under:

- `src/JKToolKit.CodexSDK/AppServer/Protocol/SandboxPolicy/` (or `Protocol/V2/` if you prefer)

Model shape (conceptual):

- [x] `ReadOnlyAccess.FullAccess` → `{ "type": "fullAccess" }`
- [x] `ReadOnlyAccess.Restricted` → `{ "type": "restricted", "includePlatformDefaults": true, "readableRoots": [] }`

Ensure:

- serialization uses camelCase to match app-server.
- defaults do not force a field to be sent unless set by the caller.

### 3.2 Extend sandbox policies

Update:

- `SandboxPolicy.ReadOnly`:
  - add optional `ReadOnlyAccess? Access` serialized as `"access"`
- `SandboxPolicy.WorkspaceWrite`:
  - add optional `ReadOnlyAccess? ReadOnlyAccess` serialized as `"readOnlyAccess"`

Design notes:

- Keep the existing policies source-compatible where possible.
- Prefer nullable properties so stable-only callers don’t accidentally start sending new fields.

### 3.3 Server version compatibility (important)

`ReadOnlyAccess` is an upstream addition. Older Codex app-server builds may not accept the new fields on the `readOnly` / `workspaceWrite` variants.

Plan to keep compatibility:

- [x] **Do not send new fields by default**
  - [x] Keep `Access` / `ReadOnlyAccess` nullable and omit when null.
- [x] **Document minimum Codex version expectations**
  - [x] “If you set `ReadOnlyAccess`, you need a Codex build new enough to understand it.”
- [x] **Fail with actionable guidance**
  - [x] If app-server returns a JSON-RPC “invalid params” error, include:
    - [x] the sandbox policy shape we sent (redacted as needed)
    - [x] the server `userAgent` (available from `initialize`) to help diagnose mismatch

Optional enhancement:

- [x] Parse a version identifier from `AppServerInitializeResult.UserAgent` when present and gate features client-side.
  - [x] This is best-effort only; user agents can change format.

---

## 4) How this composes with existing high-level options

Callers today set:

- `TurnStartParams.SandboxPolicy` (wire)

We should document recommended patterns:

1. Stable “workspace write” with default read access:
   - `SandboxPolicy.WorkspaceWrite { ... }`
2. Restrict read-only access explicitly (advanced):
   - `SandboxPolicy.ReadOnly { Access = ReadOnlyAccess.Restricted { readableRoots = [...] } }`

If additional SDK-level convenience types exist (`CodexSandboxMode` etc), consider adding helpers later:

- `CodexSandboxPolicyBuilder` (non-breaking addition)

### 4.1 Mapping guidance (exec / app-server / mcp)

This repo already has multiple ways to influence sandboxing:

- Exec mode (`codex exec`): via CLI flags/config and JSONL events
- App-server: via `turn/start.sandboxPolicy`
- MCP server tool calls: via tool arguments (approval/sandbox/cwd/etc.)

Plan:

- Keep `ReadOnlyAccess` **app-server specific** initially.
- Add cross-mode helpers only after we have:
  - stable, tested app-server wire behavior
  - clear user demand for unified configuration

---

## 5) Permissions / requirements surface (future)

Upstream introduced more explicit permissions/config requirements signals.

Plan for a follow-up iteration:

- [x] Add typed wrappers for:
  - [x] `configRequirements/read`
  - [x] structured network requirements (if present in schema)
- [x] Add typed notifications/events where stable and valuable.

Key design constraint:

- Do not force consumers to upgrade every time upstream adds a field; preserve `JsonElement Raw`.

### 5.1 Likely follow-up protocol areas (as upstream evolves)

These are intentionally not in the first delivery, but should be considered when designing types:

- [x] Network requirements (proxy, allowlists, “network required” failures)
- [x] Permission prompts/messages (new event variants)
- [x] Config requirements read/write APIs (policy negotiation)

Design stance:

- Prefer additive wrappers that keep raw payloads.
- Avoid deep enums without an “unknown” fallback.

---

## 6) Testing strategy

- [x] Unit tests:
  - [x] `ReadOnlyAccess` serialization matches upstream shapes
  - [x] `SandboxPolicy.ReadOnly` includes `"access"` only when set
  - [x] `SandboxPolicy.WorkspaceWrite` includes `"readOnlyAccess"` only when set
- [x] Integration tests (optional):
  - [x] start app-server and run a turn with restricted readable roots (if practical)

---

## 7) Acceptance criteria

- [x] SDK can express upstream read-only access controls without breaking existing callers.
- [x] Defaults remain stable and do not send new fields unintentionally.
- [x] Docs clearly explain when and why to use read-only access restrictions.

---

## 8) Implementation milestones

- [x] Phase A — wire models
  - [x] Introduce `ReadOnlyAccess` union type (wire DTO)
  - [x] Extend `SandboxPolicy.ReadOnly` / `SandboxPolicy.WorkspaceWrite` with nullable fields
- [x] Phase B — docs
  - [x] Update `docs/AppServer/README.md` with examples and version notes
- [x] Phase C — tests
  - [x] Unit tests for serialization/omission
  - [x] Optional integration test (guarded by env var)
