---
description: "Refactor oversized source files: max 500 LOC, aim <300, avoid partials"
---

# Tasks: Refactor File Size Limits

**Goal**: No `.cs` file under `src/` exceeds **500 lines** (aim **<300**). Prefer **composition** over partials.

**Scope**: `src/` only (tests and demos are out of scope for the size limit).

## Phase 0 — Tracking & guardrails

- [ ] T001 Add a unit-test guard that fails if any `src/**/*.cs` file exceeds 500 lines (and prints soft warnings for >300).
- [ ] T002 Verify the guard passes in CI (`dotnet test`).

## Phase 1 — Refactor oversized `src/` files

- [ ] T010 Refactor `CodexAppServerClient` (split into composed internal modules; keep static parsing helpers callable via `CodexAppServerClient.*`).
- [ ] T011 Refactor `JsonlEventParser` (split into internal parsing components; keep existing behavior and tests).
- [ ] T012 Refactor `CodexClient` to remove `partial` (replace `[GeneratedRegex]` usage; compose internal helpers).
- [ ] T013 Refactor `CodexSessionLocator` to remove `partial` (replace `[GeneratedRegex]`; extract helpers).
- [ ] T014 Refactor `CodexStructuredOutputExtensions` (split into internal helpers; keep public extensions).
- [x] T015 Refactor `ResilientCodexAppServerClient` (extract adapter types so the file is <500).
- [ ] T016 Refactor `CodexProcessLauncher` (extract IO/diagnostics helpers; keep tested internals).
- [ ] T017 Ensure all `src/` files now meet the 500-line max (aim <300 where reasonable).

## Phase 2 — Validation

- [ ] T020 Run `dotnet test` and confirm all tests pass.
