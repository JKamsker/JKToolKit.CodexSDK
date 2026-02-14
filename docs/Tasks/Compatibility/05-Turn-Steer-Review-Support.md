# Plan: Add `turn/steer` and `review/start` Support (App-Server)

Upstream app-server includes:

- `turn/steer`: send additional user input to a running turn with an expected-turn-id precondition
- `review/start`: orchestrate code reviews inline or detached to another thread

This plan adds support while keeping consistency with:

- existing per-turn handle (`CodexTurnHandle`)
- existing input item API (`TurnInputItem`)
- stable-only default behavior

---

## 1) Goals

### Must-haves

- [x] Add a wrapper for `turn/steer`.
- [x] Add a wrapper for `review/start`.
- [x] Provide an ergonomic C# API that matches current patterns.

### Nice-to-haves

- [x] Add convenience helpers to build review targets (uncommitted/base/commit/custom).
- [x] Tie `turn/steer` to `CodexTurnHandle` directly (e.g. `handle.SteerAsync(...)`).
- [ ] Align terminology with exec-mode review (`CodexClient.ReviewAsync`) where practical.

---

## 2) `turn/steer` design

### 2.1 Proposed public API

Option A (client-level):

- `Task<string> SteerTurnAsync(TurnSteerOptions options, CancellationToken ct = default)`
  - returns new/confirmed turn id

Option B (turn-handle-level, preferred ergonomics):

- `Task SteerAsync(IReadOnlyList<TurnInputItem> input, CancellationToken ct = default)`
  - uses internal `ThreadId` + `TurnId` from the handle
  - requires the handle to know the active turn id (“expected_turn_id”)

### 2.2 Wire DTO and mapping

Add wire params DTO matching upstream:

- `threadId`
- `expectedTurnId`
- `input` (v2 `UserInput[]`)

Implementation steps:

- [x] Add protocol DTO `TurnSteerParams` under `Protocol/V2`.
- [x] Add `CodexAppServerClient.Steer...` method calling `_rpc.SendRequestAsync("turn/steer", ...)`.
- [x] Decide whether to expose this on `CodexTurnHandle` (follow-up).

### 2.3 Failure modes

Upstream rejects steer requests if:

- expectedTurnId does not match the active turn

SDK behavior:

- Throw with a clear message that includes:
  - expected turn id
  - server error payload (raw)

### 2.4 Concurrency + lifecycle semantics

Open questions to resolve up-front (documented behavior matters here):

- [ ] **Can multiple steer requests be in-flight concurrently?**
  - [ ] Recommendation: allow, but callers should serialize themselves.
- [ ] **What happens if the turn completes between building params and sending?**
  - [ ] Expect server to reject; surface as a typed exception or raw JSON-RPC error.
- [ ] **Cancellation behavior**
  - [ ] Cancel should:
    - [ ] stop waiting for the response
    - [ ] not assume the steer request was not applied (server may still apply)

SDK mitigations:

- [ ] Keep `Raw` response/error available for diagnosis.
- [ ] If we add a handle-level API, document that steer is “best effort” and may race with completion.

---

## 3) `review/start` design

### 3.1 Review target modeling

Upstream supports multiple target shapes (conceptual):

- Uncommitted changes
- Base branch diff
- Commit diff (optionally includes title)
- Custom instructions

SDK approach:

- Provide a typed discriminated union (record hierarchy) **or**
- Provide a single options object with mutually exclusive properties.

Recommendation:

- Use a small record hierarchy:
  - `ReviewTarget.UncommittedChanges`
  - `ReviewTarget.BaseBranch(string branch)`
  - `ReviewTarget.Commit(string sha, string? title)`
  - `ReviewTarget.Custom(string instructions)`

### 3.2 Delivery modeling

Upstream supports inline vs detached delivery.

- Inline: review runs on current thread
- Detached: review runs on new thread (server returns `reviewThreadId`)

SDK options:

- `ReviewDelivery` enum: `Inline`, `Detached`

### 3.3 Proposed API

- `Task<ReviewStartResult> StartReviewAsync(ReviewStartOptions options, CancellationToken ct = default)`

Where:

- `ReviewStartOptions`
  - `string ThreadId`
  - `ReviewTarget Target`
  - `ReviewDelivery? Delivery`
- `ReviewStartResult`
  - `CodexTurnHandle Turn` (or raw `JsonElement` + extracted ids)
  - `string ReviewThreadId`
  - `JsonElement Raw`

Implementation detail:

- Upstream returns a `turn` object and `reviewThreadId`.
- We can reuse `CodexTurnHandle` creation logic if it exists; if not, return `CodexTurnHandle`-like wrapper or keep it as raw for first iteration.

### 3.4 Relationship to exec-mode reviews

The SDK already supports non-interactive reviews via:

- `JKToolKit.CodexSDK.Exec.CodexClient.ReviewAsync(...)` (CLI `codex review`)

App-server review (`review/start`) differs:

- runs inside the thread/turn model
- can be inline or detached
- emits normal app-server notifications during execution

Plan for minimizing user confusion:

- [ ] Document when to use which:
  - [ ] exec-mode: simple one-off review with stdout/stderr result
  - [ ] app-server: review as a first-class turn, with streaming events/notifications
- [ ] Consider a facade helper later:
  - [ ] `CodexSdk.ReviewAsync(...)` that can route to exec vs app-server based on configuration.

---

## 4) Notifications and streaming implications

`review/start` will produce the normal per-turn notification stream.

SDK work:

- [ ] Ensure per-turn stream plumbing can handle:
  - [ ] detached reviews (turn belongs to a different thread id)
  - [ ] review mode transitions (if surfaced via notifications)

Strategy:

- [x] Preserve raw notifications.
- [ ] Add typed mappings only for stable/high-value events.

### 4.1 Detached reviews and turn handles

Detached reviews will return:

- a `reviewThreadId` that may differ from the original thread

SDK implications:

- `ReviewStartResult` should always include the effective thread id for the returned turn handle.
- If we create a `CodexTurnHandle` directly from the response:
  - ensure its internal bookkeeping keys (`_turnsById`) handle the returned turn id regardless of thread.

---

## 5) Testing strategy

- [x] Unit tests:
  - [x] request param serialization for `turn/steer`
  - [x] request param serialization for `review/start` for each target variant
- [ ] Mapper tests (if new notifications are added)
- [ ] Integration tests (optional):
  - [ ] start thread → start turn → steer turn
  - [ ] start review inline and detached

---

## 6) Acceptance criteria

- [x] Users can steer an in-progress turn safely.
- [x] Users can start reviews using the app-server API with clear, typed targets.
- [x] Detached review returns the correct review thread id.

---

## 7) Implementation milestones

- [x] Phase A — add request wrappers
  - [x] `turn/steer` request + minimal result parsing
  - [x] `review/start` request + minimal result parsing
- [x] Phase B — ergonomic APIs
  - [x] optional `CodexTurnHandle.SteerAsync(...)`
  - [x] typed `ReviewTarget` helpers
- [x] Phase C — docs + examples
  - [x] add to `docs/AppServer/README.md` with sample flows
- [x] Phase D — tests
  - [x] unit tests for serialization + extractor helpers
  - [ ] optional integration tests guarded by env var
