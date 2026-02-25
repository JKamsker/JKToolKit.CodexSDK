# Manual Testing Checklist

Use this as a release / PR verification checklist. Commands assume PowerShell and repo root.

> Tip: `dotnet run --project src/JKToolKit.CodexSDK.Demo -- --help` lists all demo commands.

## Build + Tests

- [ ] `dotnet test` passes

## Exec mode (`codex exec`)

- [ ] `exec` streams events and completes (no hang)
- [ ] `exec` prints session id + log path
- [ ] `exec` resumes the session (follow-up) and completes
- [ ] `exec-list` lists recent sessions
- [ ] `exec-attach` can attach to a printed log path and stream events

## Structured outputs

- [ ] `structured-review` completes and renders a summary + issues/fix-tasks (or “clean”)

## Non-interactive review (`codex review`)

- [ ] `review --commit <sha>` runs and prints review output
- [ ] `review --uncommitted` runs when you have local changes

## App-server mode (`codex app-server`)

- [ ] `appserver-stream` streams deltas and completes
- [ ] `appserver-notifications` observes typed + raw notifications (counts > 0)
- [ ] `appserver-turn-control` successfully sends `turn/steer` and `turn/interrupt`
- [ ] `appserver-approval` requests approval and creates `test.txt` when approved
- [ ] `appserver-thread start --seed ...` creates a thread and a seed turn
- [ ] `appserver-thread list` shows threads + next cursor
- [ ] `appserver-thread read` prints thread summary fields
- [ ] `appserver-thread set-name` updates name
- [ ] `appserver-thread fork` creates a forked thread id
- [ ] `appserver-thread archive` and `unarchive` work
- [ ] `appserver-thread compact` works
- [ ] `appserver-thread rollback` works
- [ ] `appserver-thread clean-bg-terminals --experimental-api` works (or throws experimental-api-required without flag)
- [ ] `appserver-skills-apps` lists skills successfully
- [ ] `appserver-config` prints config + requirements; remote skills may require ChatGPT auth
- [ ] `appserver-config-write` works against a temp `--codex-home` (skills config write)
- [ ] `appserver-mcp` reloads MCP servers and lists status
- [ ] `appserver-fuzzy --experimental-api` produces at least one update
- [ ] `appserver-review --target custom ...` starts a review and completes
- [ ] `appserver-resilient-stream --restart-between-turns` emits a restart marker and completes both turns

## MCP-server mode (`codex mcp-server`)

- [ ] `mcpserver` lists tools and can start a session
- [ ] `mcpserver --low-level` exercises `CallAsync("tools/list")` and `CallToolAsync("codex-reply", ...)`

## DI + Overrides

- [ ] `di-overrides` runs and prints `ok` (and shows observer/transformer markers)
- [ ] (Optional) Scratch app scenarios in `DI-and-Overrides.md` still work
