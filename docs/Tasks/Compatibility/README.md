# Compatibility Plans (Upstream Codex)

This folder contains *implementation plans* (not code changes yet) for keeping `JKToolKit.CodexSDK` compatible with upstream `openai/codex`, and for selectively adding support for newer Codex capabilities.

## Snapshot (captured 2026-02-13)

- Local Codex checkout previously targeted by this SDK: `dd80e332c`
- Upstream `origin/main` after fetch: `e00080cea` (275 commits ahead)

## Key upstream changes driving these plans

1. **App-server experimental API gating expanded**
   - More fields/methods are explicitly “experimental” and will be rejected unless the client opts in at initialize time with:
     - `initialize.params.capabilities.experimentalApi = true`
   - This affects some features that the SDK currently exposes as optional parameters (example: `thread/resume.history`, `thread/resume.path`, and `turn/start.collaborationMode`).

2. **App-server v2 surface area grew significantly**
   - Thread lifecycle (list/read/fork/archive/unarchive/name/etc.)
   - Skills and apps list + updates
   - Turn steering (`turn/steer`) and review orchestration (`review/start`)
   - Richer sandbox / permissions constructs (read-only access control, network/proxy details, etc.)

## Plans in this folder

- [x] [01 — Stable-only strategy](./01-StableOnly-Strategy.md)
- [x] [02 — Experimental API opt-in](./02-ExperimentalApi-OptIn.md)
- [x] [03 — Threads API support](./03-Threads-Api-Support.md)
- [x] [04 — Skills & apps support](./04-Skills-Apps-Support.md)
- [x] [05 — Turn steer & review/start support](./05-Turn-Steer-Review-Support.md)
- [x] [06 — Sandbox/permissions & read-only access support](./06-Sandbox-Permissions-ReadOnlyAccess.md)

## Default direction (recommended)

- Keep the SDK’s *default* behavior **stable-only** (no experimental opt-in).
- Add an **explicit** `experimentalApi` opt-in for users who want bleeding-edge features, with clear docs and guardrails.
