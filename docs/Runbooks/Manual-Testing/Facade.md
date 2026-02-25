# Manual Testing: Facade Routing (`CodexSdk`)

This validates:

- `CodexSdk.ReviewAsync(...)` routing for:
  - exec-mode reviews (`codex review`)
  - app-server reviews (`review/start`)

## 1) Run the facade routing demo

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- sdk-review-route --timeout-seconds 600
```

Verify:

- **Phase 1 (Exec)**:
  - If `git` is available, it prints `[exec] exit=0 ...`.
  - If `git` is not available, it prints a clear "skipped" message (this is acceptable).
  - This phase may take a few minutes depending on the repository size and current Codex behavior.
- **Phase 2 (AppServer)**:
  - It prints `[app-server] thread=...`.
  - It completes the review turn and prints `Done: completed`.
- It prints `ok`.
