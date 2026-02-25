# Manual Testing Troubleshooting

## Demo hangs / never returns

1. First try **Ctrl+C**.
2. If it still doesn’t exit, kill the **demo process** only:

```powershell
Get-Process JKToolKit.CodexSDK.Demo -ErrorAction SilentlyContinue | Stop-Process -Force
```

Avoid killing all `codex` processes globally unless you are sure they belong to your test.

## “codex not found” / process launch fails

- Ensure `codex --version` works.
- If Codex is not on PATH, pass `--codex-path <PATH>` to demo commands that support it.

## Exec session log not found

- Ensure the sessions directory exists (commonly `%USERPROFILE%\.codex\sessions` on Windows).
- Use `exec-list` to discover log paths.

## App-server: experimental API required

Some endpoints require the experimental capability. Re-run with `--experimental-api`:

- `appserver-thread clean-bg-terminals --experimental-api`
- `appserver-fuzzy --experimental-api`

## App-server: remote skills require ChatGPT auth

If `appserver-config` reports something like:

`chatgpt authentication required for hazelnut scopes; api key auth is not supported`

Run:

```powershell
codex login
```

Then re-run `appserver-config` / `appserver-config-write` (remote skills parts).

## File locked during build

Stop any running demo processes (see “Demo hangs” above), then rebuild/test.

