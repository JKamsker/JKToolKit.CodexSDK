# Manual Testing: App-Server Mode (`codex app-server`)

This validates:

- JSON-RPC handshake (`initialize` / `initialized`)
- threads and turns lifecycle
- streaming text deltas
- typed + raw notifications
- approvals
- steer/interrupt
- skills/apps/config endpoints
- resilient auto-restart wrapper
- experimental API flows (fuzzy search, background terminals clean)

All commands use the demo app. Add `--timeout-seconds <N>` to avoid getting stuck.

## 0) Basic streaming (turn deltas)

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-stream --timeout-seconds 60
```

Verify:

- You see streamed text (a repo summary).
- It ends with `Done: completed`.

## 1) Global notifications (typed + raw)

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-notifications --timeout-seconds 60
```

Verify:

- It prints both `[typed] ...` and `[raw] ...` notifications.
- Final counts `typed=...` and `raw=...` are both > 0.

## 2) Turn control: steer + interrupt

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-turn-control --timeout-seconds 60
```

Verify:

- You see `[steer] ok (turnId=...)`
- You see `[interrupt] sent`
- It ends with `Done: interrupted`

## 3) Approvals (server-initiated request handling)

This demo intentionally creates a temp working directory and asks Codex to create `test.txt`.

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-approval --timeout-seconds 120
```

Verify:

- You see an approval log line like: `[approval] method=item/commandExecution/requestApproval approve=True`
- The demo prints `Created: <...>\test.txt`

## 4) Threads: start/list/read/lifecycle operations

### Start a thread and create an initial turn

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-thread start --seed "Say 'hi'." --timeout-seconds 60
```

Copy the `Started thread: <ID>` value for the steps below.

### List threads

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-thread list --limit 3 --timeout-seconds 60
```

Verify:

- A table is printed.
- A `NextCursor:` line is printed when there are more results.

### Read thread summary

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-thread read --thread <THREAD_ID> --timeout-seconds 60
```

Verify it prints fields like `Name`, `CreatedAt`, `Cwd`, and `Model` when present.

### Set thread name

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-thread set-name --thread <THREAD_ID> --name "manual-test-thread" --timeout-seconds 60
```

### Fork / archive / unarchive

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-thread fork --thread <THREAD_ID> --timeout-seconds 60
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-thread archive --thread <THREAD_ID> --timeout-seconds 60
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-thread unarchive --thread <THREAD_ID> --timeout-seconds 60
```

### Compact / rollback

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-thread compact --thread <THREAD_ID> --timeout-seconds 60
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-thread rollback --thread <THREAD_ID> --num-turns 1 --timeout-seconds 60
```

### Clean background terminals (experimental capability)

Without experimental API, this should fail with a clear exception:

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-thread clean-bg-terminals --thread <THREAD_ID> --timeout-seconds 60
```

With experimental API enabled, it should succeed:

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-thread clean-bg-terminals --thread <THREAD_ID> --experimental-api --timeout-seconds 60
```

## 5) Skills + apps

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-skills-apps --limit 10 --timeout-seconds 60
```

Verify:

- Skills list prints names and (optionally) `enabled=...`.
- Apps list prints (may be empty depending on environment).

## 6) Config read + requirements + remote skills

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-config --timeout-seconds 60
```

Verify:

- It prints `config/read` summary (top-level keys, layers, MCP servers).
- It prints `configRequirements/read`.
- Remote skills:
  - If you are authenticated for hazelnut scopes, it should list remote skills.
  - If not, you may see an error like `chatgpt authentication required...` (this is expected; run `codex login` to fully cover this feature).

## 7) Skills config write (safe mode via temp CODEX_HOME)

This demo refuses to write unless both `--apply` and `--codex-home` are provided.

```powershell
$tmp = Join-Path $env:TEMP ("codexsdk-configwrite-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp | Out-Null
$skills = Join-Path $tmp "skills"
New-Item -ItemType Directory -Path $skills | Out-Null

dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-config-write --timeout-seconds 60 --codex-home $tmp --apply --skills-enabled true --skills-path $skills
```

Verify it prints `effectiveEnabled=True`.

## 8) MCP server management (via app-server)

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-mcp --timeout-seconds 60
```

Verify it reloads and lists MCP server status (may be 0 servers depending on your config).

Optional OAuth flow (requires a configured MCP server that supports OAuth):

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-mcp --oauth-name "<server-name>" --timeout-seconds 120
```

## 9) Fuzzy file search (experimental)

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-fuzzy --experimental-api --max-updates 1 --timeout-seconds 60
```

Verify you see at least one `[update #...]` line.

## 10) Review/start (app-server review)

Use a custom target to keep it fast:

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-review --target custom --instructions "Say 'ok' and nothing else." --timeout-seconds 60
```

Verify:

- It prints a thread id and review turn id.
- It completes with `Done: completed`.

## 11) Resilient app-server wrapper (auto restart)

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-resilient-stream --restart-between-turns --prompt "Say 'hi'." --prompt2 "Say 'bye'." --timeout-seconds 120
```

Verify:

- It emits a restart marker: `[resiliency] restarted #...`
- Both turns complete.

## 12) Override hooks (response/notification transformers + mappers)

This validates:

- `CodexAppServerClientOptions.ResponseTransformers`
- `CodexAppServerClientOptions.NotificationTransformers`
- `CodexAppServerClientOptions.NotificationMappers`

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-overrides --timeout-seconds 120
```

Verify:

- It prints marker lines:
  - `[response-transformer] ...`
  - `[notification-transformer] ...`
  - `[notification-mapper] ...`
- It prints `ok`.

## 13) Opt-out notification methods (initialize capabilities)

This validates `InitializeCapabilities.OptOutNotificationMethods` reduces notification volume.

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-optout-notifications --timeout-seconds 120
```

Verify:

- It prints `baseline=<N>` where `<N> > 0`.
- It prints `optOut=0`.
- It prints `ok`.

## 14) Output schema (turn/start.outputSchema)

This validates `TurnStartOptions.OutputSchema` produces parseable JSON output.

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-output-schema --timeout-seconds 120
```

Verify:

- It prints JSON (no backticks / no extra prose).
- It prints `Parsed answer: ...`.
- It prints `ok`.

## 15) Sandbox policy (turn/start.sandboxPolicy)

This validates `TurnStartOptions.SandboxPolicy` can override sandbox policy per turn (e.g. `readOnly` vs `workspaceWrite`).

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-sandbox-policy --timeout-seconds 180
```

Verify:

- Phase 1 (`sandboxPolicy=readOnly`) reports `allowed.txt exists (phase1): False`
- Phase 2 (`sandboxPolicy=workspaceWrite`) reports `allowed.txt exists (phase2): True`
- It prints `ok`.

## 16) Collaboration mode (experimental)

This validates:

- the SDK guardrails for `turn/start.collaborationMode` (stable-only client throws `CodexExperimentalApiRequiredException`)
- the experimental endpoint `collaborationMode/list`
- starting a turn with `TurnStartOptions.CollaborationMode` when experimental API is enabled

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-collaboration-mode --timeout-seconds 180
```

Verify:

- Phase 1 prints an expected exception with `Descriptor='turn/start.collaborationMode'`.
- Phase 2 prints a preset summary and completes a turn.
- It prints `ok`.
