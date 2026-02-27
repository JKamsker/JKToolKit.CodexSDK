# Manual Testing: Exec Mode (`codex exec`)

This validates:

- `CodexClient.StartSessionAsync` / `ResumeSessionAsync`
- JSONL tailing + typed event parsing (`IAsyncEnumerable<CodexEvent>`)
- `AttachToLogAsync` + `ListSessionsAsync`
- structured outputs helpers
- non-interactive reviews (`codex review`)

## 1) Exec streaming (start + follow + graceful stop)

Run a short prompt so the session exits quickly:

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- exec --prompt "Say 'ok' and nothing else." --reasoning low
```

Verify:

- The command prints **Session ID** and **Log file**.
- Event streaming shows `user_message` and the assistant response (`ok`).
- The process returns to the shell **without hanging** (this exercises follow-mode shutdown).
- The demo prints a second phase where it **resumes** the session with the follow-up prompt `"how are you"`.

## 2) List sessions

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- exec-list --limit 5
```

Verify you see timestamps, session ids, and log paths.

## 3) Attach to an existing JSONL log

1. Copy the `Log file` path printed by the `exec` demo.
2. Attach:

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- exec-attach --log "<PASTE LOG PATH HERE>"
```

Verify:

- It prints the session id and creation timestamp.
- It prints a sequence of event type names.
- It prints agent text when present (e.g., `ok`).

## 4) Structured outputs (typed JSON)

The demo includes a `structured-review` command that uses the structured-output pipeline.

Run a minimal prompt (fast/cheap) to verify the JSON -> DTO path works:

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- structured-review --max-attempts 1 --reasoning low --prompt "Return JSON only matching the schema with: summary='ok', severity='clean', issues=[], fixTasks=[]."
```

Verify:

- It completes in a single attempt.
- It renders a summary panel with `ok`, and shows "No issues found." / "No fix tasks needed."

## 5) Non-interactive code review (`codex review`)

### Current Codex CLI prompt/scope behavior

Some Codex CLI versions (observed in v0.104.0) reject supplying both a scope flag (`--commit`, `--base`, `--uncommitted`) **and**
a prompt argument (even though the help text shows `[PROMPT]`).

This runbook splits the tests into:

- **scope selection** (no prompt)
- **custom instructions** (prompt, no scope flags)

### Review a commit (scope selection)

```powershell
git log -1 --oneline
dotnet run --project src/JKToolKit.CodexSDK.Demo -- review --commit <SHA_FROM_GIT_LOG>
```

Verify:

- Codex prints a review with the expected header and summary.
- Exit code is 0.

### Review uncommitted changes (scope selection, optional)

1. Make a trivial local change (e.g., edit a comment).
2. Run:

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- review --uncommitted
```

Verify it produces review output for your local diff.

### Custom instructions (no scope flags)

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- review "Focus on correctness and security. If everything is fine, reply 'clean'."
```

Verify it runs and prints a review response based on your instructions.

## 6) Exec override hooks (EventTransformers + EventMappers)

This validates:

- `CodexClientOptions.EventTransformers`
- `CodexClientOptions.EventMappers`

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- exec-overrides --timeout-seconds 120
```

Verify:

- It prints `[transformer] ...` and `[mapper] ...` marker lines.
- It prints a `[custom-event] ...` line (custom type mapped by the demo mapper).
- It prints `ok`.
