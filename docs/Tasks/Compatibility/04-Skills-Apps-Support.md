# Plan: Add App-Server Skills & Apps Support

Upstream app-server added stable APIs for:

- `skills/list` (+ related skills management)
- `app/list` and notifications like `app/list/updated`

This plan adds first-class support to `JKToolKit.CodexSDK.AppServer` with typed wrappers and forward compatibility.

---

## 1) Goals

### Must-haves

- [ ] Add `skills/list` wrapper and typed results.
- [ ] Add `app/list` wrapper and typed results.
- [ ] Map `app/list/updated` notification (and keep unknown fallback).

### Nice-to-haves

- [ ] `skills/remote/read`, `skills/remote/write`
- [ ] `skills/config/write`
- [ ] richer models for skill/app descriptors (icons, disabled reasons, requirements)
- [ ] fuzzy file search sessions (experimental upstream)

---

## 2) Proposed public API additions

### 2.0 Method map (as-of upstream origin/main)

This table is mainly to drive prioritization and to keep the SDK aligned with upstream method naming.

| Area | Method / notification | Kind | Stable-only default | Notes |
|---|---|---|:---:|---|
| Skills | `skills/list` | request/response | ✅ | Core discovery method. |
| Skills | `skills/remote/read` | request/response | ✅ | Read a skill definition by reference (if enabled upstream). |
| Skills | `skills/remote/write` | request/response | ✅ | Write/update remote skill definitions. |
| Skills | `skills/config/write` | request/response | ✅ | Persist skills-related configuration. |
| Apps | `app/list` | request/response | ✅ | Enumerate apps/connectors available to the thread/workspace. |
| Apps | `app/list/updated` | notification | ✅ | Emitted when apps list changes (e.g. login/config changes). |
| Fuzzy search | `fuzzyFileSearch/sessionStart` | request/response | ❌ | Experimental upstream; require opt-in (plan 02). |
| Fuzzy search | `fuzzyFileSearch/sessionUpdate` | request/response | ❌ | Experimental upstream; require opt-in (plan 02). |
| Fuzzy search | `fuzzyFileSearch/sessionStop` | request/response | ❌ | Experimental upstream; require opt-in (plan 02). |
| Fuzzy search | `fuzzyFileSearch/sessionUpdated` | notification | ✅* | Likely emitted only when fuzzy session APIs are in use. Treat as “opt-in feature”. |

### 2.1 Skills

Add methods on `CodexAppServerClient`:

- [ ] `Task<SkillsListResult> ListSkillsAsync(SkillsListOptions options, CancellationToken ct = default)`

Add models:

- `SkillDescriptor`
  - `string Name`
  - `string? Description`
  - `string? Path`
  - `JsonElement Raw`

- `SkillsListResult`
  - `IReadOnlyList<SkillDescriptor> Skills`
  - `JsonElement Raw`

Add options:

- `SkillsListOptions`
  - `string? Cwd`
  - `IReadOnlyList<string>? ExtraRootsForCwd` (upstream supports additional roots)

#### 2.1.1 Design notes: typing vs raw JSON

Skills are an area where upstream can add fields quickly (capabilities, sources, versioning).

Recommended strategy:

- Strongly type only the fields needed for a good UX:
  - name/title/description
  - source (local vs remote)
  - filesystem path or identifier
- Preserve `JsonElement Raw` on every descriptor/result for forward compatibility.

#### 2.1.2 Remote skill read/write (nice-to-have)

If implemented, keep the API “wire friendly”:

- `ReadRemoteSkillAsync(RemoteSkillRef ref, ...)` → returns `JsonElement Raw` + extracted content if present
- `WriteRemoteSkillAsync(RemoteSkillWriteOptions options, ...)` → returns ack + `Raw`

Avoid hard-coding storage semantics; upstream may evolve what “remote” means.

### 2.2 Apps

Add methods:

- [ ] `Task<AppsListResult> ListAppsAsync(AppsListOptions options, CancellationToken ct = default)`

Add models:

- `AppDescriptor`
  - `string Id` / `string Name` (depending on upstream schema)
  - `string? Title`
  - `bool? Enabled`
  - `string? DisabledReason`
  - `JsonElement Raw`

- `AppsListResult`
  - `IReadOnlyList<AppDescriptor> Apps`
  - `JsonElement Raw`

Add notification mapping:

- `app/list/updated` → `AppListUpdatedNotification`

#### 2.2.1 Design notes: app identity and disabled reasons

Upstream apps can be disabled for multiple reasons (policy, auth, environment).

SDK strategy:

- Keep `DisabledReason` as a string (or a small enum with unknown fallback).
- Preserve the full raw app object.

#### 2.2.2 Apps list scoping

Upstream APIs may scope apps to:

- the current workspace / cwd
- the authenticated account
- host-specific availability

SDK approach:

- Include optional `Cwd` / scoping fields in `AppsListOptions` if upstream supports them.
- Otherwise keep `AppsListOptions` empty and rely on server defaults.

### 2.3 Fuzzy file search sessions (experimental upstream)

If we add support, keep it explicitly behind experimental opt-in (plan 02).

Proposed API:

- `Task StartFuzzyFileSearchSessionAsync(FuzzyFileSearchSessionStartOptions options, CancellationToken ct = default)`
- `Task UpdateFuzzyFileSearchSessionAsync(FuzzyFileSearchSessionUpdateOptions options, CancellationToken ct = default)`
- `Task StopFuzzyFileSearchSessionAsync(string sessionId, CancellationToken ct = default)`

Plus notification mapping:

- `fuzzyFileSearch/sessionUpdated` → `FuzzyFileSearchSessionUpdatedNotification`

Design notes:

- session ids should be caller-provided and opaque (string).
- results should preserve `Raw` and provide a typed list of `{ path, score }` if present.

---

## 3) Wire DTO strategy

Add protocol DTOs under:

- `src/JKToolKit.CodexSDK/AppServer/Protocol/V2/`

However, for faster compatibility:

- Keep request params typed and minimal.
- Keep responses as `JsonElement Raw` + helper extractors.

This avoids chasing frequent upstream schema edits.

### 3.1 Incremental delivery (phased)

- [ ] Phase A — stable discovery:
  - [ ] `skills/list`
  - [ ] `app/list`
  - [ ] `app/list/updated` notification mapping
- [ ] Phase B — management:
  - [ ] `skills/config/write`
  - [ ] `skills/remote/read` / `skills/remote/write`
- [ ] Phase C — experimental-only extras:
  - [ ] fuzzy file search session APIs + notification

---

## 4) Notification mapping strategy

Update:

- [ ] `src/JKToolKit.CodexSDK/AppServer/Notifications/AppServerNotificationMapper.cs`

Add:

- [ ] `"app/list/updated"` mapping

Ensure:

- [ ] unknown notifications still map to `UnknownNotification` with raw params preserved.

---

## 5) Testing strategy

- [ ] Mapper tests:
  - [ ] Add fixtures for `app/list/updated`
- [ ] Response parsing tests:
  - [ ] skills/list response sample
  - [ ] app/list response sample
- [ ] Integration tests (optional):
  - [ ] list skills/apps on a local Codex install (guarded by env var)

---

## 6) Acceptance criteria

- [ ] Callers can list skills and apps via typed client methods.
- [ ] `app/list/updated` notification appears as a typed notification.
- [ ] Unknown fields/methods do not break the SDK.
