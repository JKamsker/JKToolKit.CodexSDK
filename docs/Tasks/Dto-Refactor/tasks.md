---
description: "Schema-driven DTO refactor + upstream sync automation (Codex CLI)"
---

# Tasks: DTO Refactor (Schema-Driven) + Upstream Sync

**Goal:** Make `JKToolKit.CodexSDK` resilient to upstream Codex DTO/protocol drift by:

- Generating **internal** wire DTOs from upstream **JSON Schema** (app-server bundle).
- Keeping the public surface focused on **stable wrappers** + **raw JSON escape hatches**.
- Providing a **pluggable, method-based** request/response/notification pipeline so users can override parsing/serialization per command.
- Adding an **automation workflow** that detects new `@openai/codex` releases, regenerates DTOs, and opens a PR **only if build + tests succeed**.

## Decisions / Constraints

- **Compatibility target:** latest Codex CLI only (no long-lived compatibility shims).
- **Codegen shipping:** generated C# is **committed** into the repo (NuGet package ships only C#).
- **Visibility:** generated wire DTOs are **internal**; public API stays stable and forwards compatible via raw JSON + override hooks.
- **Overrides:** users can replace (per method / per event type) how DTOs are serialized/deserialized and how notifications/events are mapped.
- **Breaking changes:** allowed (repo is `0.0.x` / alpha); we can internalize wire DTO namespaces.

---

## Phase 0 — Tracking

- [x] T001 Add this plan + checkbox tasklist.

## Phase 1 — App-server override pipeline (DTO hooks)

- [x] T010 Add app-server override interfaces + options (no behavior change).
- [ ] T011 Implement request/response transformer pipeline in app-server core.
- [ ] T012 Implement notification transformer pipeline + pluggable notification mappers + raw notification stream.
- [ ] T013 Add `CallAsync<T>` + raw notification helpers on `CodexAppServerClient`/`CodexTurnHandle`.
- [ ] T014 Add unit tests covering override ordering + safety.

## Phase 2 — Align SDK requests/responses to upstream (reduce drift)

- [ ] T020 Fix known wire mismatches (e.g., `thread/list` uses `limit` upstream, not `pageSize`) + tests + demo updates.
- [ ] T021 Audit other high-traffic endpoints (threads/turns/skills/apps/config/mcp mgmt) for param/shape mismatches and harden parsers.

## Phase 3 — Generator tool (schema → internal wire DTOs)

- [ ] T030 Add `JKToolKit.CodexSDK.UpstreamGen` tool project + CLI skeleton.
- [ ] T031 Implement schema discovery + metadata output (Codex version + schema hash).
- [ ] T032 Generate internal C# wire DTOs from `codex_app_server_protocol.schemas.json` and commit output under `src/JKToolKit.CodexSDK/Generated/Upstream/`.
- [ ] T033 Refactor app-server internals to use generated DTOs where it reduces drift risk (while keeping public wrappers stable).
- [ ] T034 Add generator `--check` mode + CI guard (fails if generated output is stale).

## Phase 4 — Upstream release sync automation

- [ ] T040 Add `UPSTREAM_CODEX_VERSION.txt` pin + local developer docs.
- [ ] T041 Add GitHub workflow: detect new `@openai/codex` version → generate schema → run generator → build/test → open PR only if green.

## Phase 5 — Scope “Everything”: Exec + MCP override hooks

- [ ] T050 Add Exec-mode JSONL event override hooks (transform/map by event `type`) + tests.
- [ ] T051 Add McpServer parsing override hooks (tools list + reply parsing) + tests.
- [ ] T052 Document override hooks with examples (AppServer/Exec/McpServer).

## Phase 6 — Final validation

- [ ] T060 Run full `dotnet test` and ensure docs/examples compile.
