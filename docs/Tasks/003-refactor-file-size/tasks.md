---
description: "Refactor oversized source files: max 500 LOC, aim <300, avoid partials"
---

# Tasks: Refactor File Size Limits

**Goal**: No `.cs` file under `src/` exceeds **500 lines** (aim **<300**). Prefer **composition** over partials.

**Scope**: `src/` only (tests and demos are out of scope for the size limit).

## Phase 0 - Tracking & guardrails

- [x] T001 Add a unit-test guard that fails if any `src/**/*.cs` file exceeds 500 lines (and prints soft warnings for >300).
- [x] T002 Verify the guard passes in CI (`dotnet test`).

## Phase 1 - Refactor oversized `src/` files

- [x] T010 Refactor `CodexAppServerClient` (split into composed internal modules; keep static parsing helpers callable via `CodexAppServerClient.*`).
  - [x] T010a Extract JSON/parsing helpers into internal modules; keep `CodexAppServerClient.*` entrypoints stable.
  - [x] T010b Extract thread/skills/apps/config API methods into composed services.
  - [x] T010c Extract turn/review API methods into composed services.
  - [x] T010d Extract RPC notifications, disconnect handling, and lifecycle/dispose into core.
  - [x] T010e Reduce `CodexAppServerClient.cs` to <=500 lines and run focused app-server tests.
- [x] T011 Refactor `JsonlEventParser` (split into internal parsing components; keep existing behavior and tests).
- [x] T012 Refactor `CodexClient` to remove `partial` and shrink file size (<=500, aim <300).
- [x] T012a Keep `[GeneratedRegex]` by extracting regex generators into a small internal helper type.
- [x] T012b Extract CodexClient workflows into internal services (sessions/review/rate-limits/diagnostics) and make `CodexClient.cs` a thin facade.
- [x] T012c Extract review execution into `CodexReviewRunner`.
- [x] T012d Extract rate-limit scanning/caching into `CodexRateLimitsReader`.
- [x] T012e Extract session start/resume/attach into `CodexSessionRunner` + `CodexSessionDiagnostics`.
- [x] T013 Refactor `CodexSessionLocator` to remove `partial` (replace `[GeneratedRegex]`; extract helpers).
- [x] T014 Refactor `CodexStructuredOutputExtensions` (split into internal helpers; keep public extensions).
- [x] T015 Refactor `ResilientCodexAppServerClient` (extract adapter types so the file is <500).
- [x] T016 Refactor `CodexProcessLauncher` (extract IO/diagnostics helpers; keep tested internals).
- [x] T017 Ensure all `src/` files now meet the 500-line max (aim <300 where reasonable).

## Phase 2 - Validation

- [x] T020 Run `dotnet test` and confirm all tests pass.

## Phase 3 - CodeRabbit follow-ups

- [x] T030 Redact and truncate JSONL parser logging (avoid logging raw JSONL lines).
- [x] T031 Harden JSONL event parsing against non-object payload shapes.
- [x] T032 Remove completed turns from app-server turn tracking (avoid unbounded growth).
- [x] T033 Tighten ReadOnlyAccess override rejection detection (avoid false negatives/poisoning).
- [ ] T034 Sanitize remote error `Data` and sandboxPolicy JSON before embedding in exception messages.
- [ ] T035 Reduce session scanning cost in rate limits lookup (avoid enumerating/parsing all sessions).
