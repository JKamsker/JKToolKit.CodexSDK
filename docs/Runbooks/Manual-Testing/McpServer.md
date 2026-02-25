# Manual Testing: MCP Server Mode (`codex mcp-server`)

This validates:

- MCP JSON-RPC handshake (`initialize`, `notifications/initialized`)
- `tools/list` parsing
- `tools/call` invocation
- Codex tool wrappers (`codex`, `codex-reply`)
- low-level escape hatches (`CallAsync`, `CallToolAsync`)

## 1) Tools/list + start session + reply (high-level wrappers)

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- mcpserver --prompt "Say hi." --followup "Say bye."
```

Verify:

- It prints a tools list containing at least `codex` and `codex-reply`.
- It prints `Hi.` and then `Bye.` (or equivalent responses).

## 2) Low-level API surface (escape hatches)

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- mcpserver --low-level --prompt "Say hi." --followup "Say bye."
```

Verify:

- It prints: `[low-level] CallAsync(tools/list): tools=...`
- It still prints the follow-up reply text (exercising `CallToolAsync` directly).

