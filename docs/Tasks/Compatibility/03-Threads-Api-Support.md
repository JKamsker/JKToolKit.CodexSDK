# Plan: Add App-Server Threads API Support

Upstream app-server has grown a robust “threads” API surface beyond `thread/start` and `thread/resume`.

This plan adds typed, ergonomic support in `JKToolKit.CodexSDK.AppServer` while preserving:

- minimal dependencies
- forward compatibility (unknown fields preserved as raw JSON)
- stable-only default behavior (see plan 01/02 for capability handling)

---

## 1) Goals

### Must-haves

- [x] Add first-class wrappers for common thread lifecycle operations:
   - `thread/list`
   - `thread/read`
   - `thread/fork`
   - `thread/archive`
   - `thread/unarchive`
   - `thread/name/set`
- [x] Preserve raw JSON for forward compatibility.
- [x] Keep API layering consistent with existing types:
   - options objects in `JKToolKit.CodexSDK.AppServer`
   - wire DTOs in `JKToolKit.CodexSDK.AppServer.Protocol.V2`

### Nice-to-haves

- [ ] `thread/loaded/list`
- [ ] `thread/compact/start`
- [ ] `thread/rollback`
- [ ] `thread/backgroundTerminals/clean` (experimental upstream)

---

## 2) Proposed public API additions

### 2.1 Client methods

Add methods to `src/JKToolKit.CodexSDK/AppServer/CodexAppServerClient.cs` (names illustrative):

- [x] `Task<CodexThreadListPage> ListThreadsAsync(ThreadListOptions options, CancellationToken ct = default)`
- [x] `Task<CodexThreadReadResult> ReadThreadAsync(string threadId, CancellationToken ct = default)`
- [x] `Task<CodexThread> ForkThreadAsync(ThreadForkOptions options, CancellationToken ct = default)`
- [x] `Task<CodexThread> ArchiveThreadAsync(string threadId, CancellationToken ct = default)`
- [x] `Task<CodexThread> UnarchiveThreadAsync(string threadId, CancellationToken ct = default)`
- [x] `Task SetThreadNameAsync(string threadId, string? name, CancellationToken ct = default)`

Design notes:

- Return types should carry both:
  - strongly typed convenience fields (thread id, name, archived, cwd/model if present)
  - `JsonElement Raw` so consumers can use new upstream fields immediately

### 2.2 Models / results

Add minimal models:

- [x] `CodexThreadSummary`
  - `string ThreadId`
  - `string? Name`
  - `bool? Archived`
  - `DateTimeOffset? CreatedAt`
  - `string? Cwd`
  - `string? Model`
  - `JsonElement Raw`

- [x] `CodexThreadListPage`
  - `IReadOnlyList<CodexThreadSummary> Threads`
  - `string? NextCursor` (or raw cursor object)
  - `JsonElement Raw`

### 2.3 Options

- [x] `ThreadListOptions`
  - Filters: `Archived`, `Cwd`, `Query` (if supported), etc.
  - Paging: `PageSize`, `Cursor`
  - Sorting: `SortKey`, `SortDirection`

- [x] `ThreadForkOptions`
  - Stable path: fork by `ThreadId`
  - Experimental path: fork by rollout `Path` (only if experimental enabled)

### 2.4 Method map (as-of upstream origin/main)

This is a “shape guide” for what to implement first. The SDK should treat unknown fields as additive and preserve `Raw`.

| Method | Purpose | Stable-only default | Experimental considerations |
|---|---|:---:|---|
| `thread/list` | list threads/rollouts with filters + paging | ✅ | None expected for core listing; keep filters raw-friendly. |
| `thread/read` | read a thread summary/history | ✅ | Some history reconstruction fields may be experimental upstream; preserve `Raw`. |
| `thread/fork` | fork a thread into a new thread | ✅ (by `threadId`) | `path`-based fork is experimental upstream; require opt-in. |
| `thread/archive` | move thread to archived sessions | ✅ | None expected. |
| `thread/unarchive` | restore archived thread | ✅ | None expected. |
| `thread/name/set` | set/clear thread name | ✅ | None expected. |
| `thread/compact/start` | trigger compaction | ✅ | Ensure we document side effects; may evolve. |
| `thread/rollback` | roll back to a prior point | ✅ | Params likely include turn ids; keep raw. |
| `thread/loaded/list` | list loaded threads in memory | ✅ | Mostly a debugging/UX helper. |
| `thread/backgroundTerminals/clean` | cleanup background terminals | ❌ by default | Mark as experimental; require opt-in. |

### 2.5 Incremental delivery (phased)

Deliver in a sequence that yields immediate user value with minimal schema churn:

- [x] Phase A — discovery + inspection:
  - [x] `thread/list`, `thread/read`
- [x] Phase B — lifecycle management:
  - [x] `thread/archive`, `thread/unarchive`, `thread/name/set`
- [x] Phase C — branching workflows:
  - [x] `thread/fork` (stable subset)
- [ ] Phase D — advanced operations:
  - [ ] `thread/compact/start`, `thread/rollback`, `thread/loaded/list`
- [ ] Phase E — experimental-only extras:
  - [ ] `thread/backgroundTerminals/clean`

---

## 3) Wire DTO strategy

Where:

- `src/JKToolKit.CodexSDK/AppServer/Protocol/V2/`

Approach:

- [x] Add per-method DTOs for request params.
- [x] For responses, prefer parsing into:
   - `JsonElement` raw
   - plus helper extractors (thread id) using existing patterns in `CodexAppServerClient`.

This minimizes the amount of schema churn that forces SDK updates.

### 3.1 DTOs to introduce (suggested)

Add to `src/JKToolKit.CodexSDK/AppServer/Protocol/V2/`:

- [x] `ThreadListParams`, `ThreadListResponse`
- [x] `ThreadReadParams`, `ThreadReadResponse`
- [x] `ThreadForkParams`, `ThreadForkResponse`
- [x] `ThreadArchiveParams`, `ThreadArchiveResponse`
- [x] `ThreadUnarchiveParams`, `ThreadUnarchiveResponse`
- [x] `ThreadSetNameParams`, `ThreadSetNameResponse`
- [ ] (later) `ThreadCompactStartParams`, `ThreadCompactStartResponse`
- [ ] (later) `ThreadRollbackParams`, `ThreadRollbackResponse`
- [ ] (later) `ThreadLoadedListParams`, `ThreadLoadedListResponse`

Implementation note:

- Keep response DTOs as minimal “envelope” types when possible:
  - e.g. a single `JsonElement Thread` property + `Raw`
- Only strongly-type fields that are used frequently in control flow (ids, cursors).

---

## 4) Compatibility and experimental gating

Upstream marks some fields as experimental (example: `thread/fork.path`).

Proposed behavior:

- Stable-only default:
  - Disallow experimental-only fields at options layer (throw with guidance).
- Experimental opt-in enabled:
  - Allow, but document as unstable.

See plan 02 for how the opt-in is configured.

### 4.1 “Raw-first” parsing rule

For all new thread methods:

- Always return `JsonElement Raw` containing the full server response.
- Extract only the minimal identifiers needed to wire SDK helpers:
  - `threadId`
  - (optional) `turnId` for rollback/compact flows
- Do not hard-fail if new fields appear.

---

## 5) Notifications related to threads

Existing SDK notifications include:

- `thread/started`
- `thread/name/updated`

Upstream adds additional notifications over time. Strategy:

- Keep `UnknownNotification` as the forward-compat default.
- Add mappings only for high-value, stable notifications.

---

## 6) Testing strategy

### 6.1 Contract tests (mapper + extractors)

Add JSON fixtures for representative responses:

- [x] thread/list response with cursor
- [x] thread/read response with history summary
- [x] thread/fork response
- [x] thread/archive/unarchive responses

Test:

- [x] correct id extraction
- [x] no exceptions on unknown fields
- [x] roundtrip “Raw” presence

### 6.2 Integration tests (optional)

If a real app-server is available:

- [ ] Start thread → list → read → archive → list archived → unarchive

Guard with env var.

---

## 7) Acceptance criteria

- [x] Users can do end-to-end thread lifecycle operations through typed methods.
- [x] Stable-only mode works without experimental opt-in.
- [x] Experimental-only request shapes are blocked unless opt-in enabled.
- [x] Unknown upstream fields do not break parsing; raw JSON preserved.
