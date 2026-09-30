# Codex 0.159.1 -> 0.159.2 Interop Research

## Scope

- Verified the `0.159.2` API marker matches the vendored `rust-v0.159.2` commit.
- Regenerated the upstream schema and DTO bundle and confirmed it is unchanged.
- Audited the release notes and local source delta from `rust-v0.159.1` to `rust-v0.159.2`.
- Focused on the Windows background-process, Job Object, shell snapshot, Git, hooks, MCP, and sandbox launch paths touched by the release.

## Confirmed Upstream Changes

- Windows background processes now consistently use `CREATE_NO_WINDOW` to prevent transient console windows.
- Redirected shell-tool and shell-snapshot launches use a shared background-command helper, while interactive launches continue to inherit their console.
- Job Object launches preserve `CREATE_NO_WINDOW` both for suspended contained children and for the uncontained fallback path.
- Git, hooks, MCP, provider-auth, plugin, daemon, and sandbox subprocess call sites adopt the same console-free background policy.

## No Actionable SDK Drift

- The changes are internal to subprocesses launched by Codex and do not alter CLI arguments, exec JSON, app-server protocol, or schemas.
- SDK-owned exec, resume, review, app-server, MCP-server, and generic stdio launches already set `ProcessStartInfo.CreateNoWindow = true` and redirect the streams they consume.
- The SDK does not reimplement Codex's Windows Job Object or nested child-process launch machinery.
- Generated schema and DTO output remains current without changes.

## Validation

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.159.2`
- Local commit and source diff from `rust-v0.159.1` to `rust-v0.159.2`
- Upstream background-command and Windows Job Object implementations and tests
- Upstream shell snapshot, Git, hooks, MCP, daemon, and sandbox process-launch call sites

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.159.1 -> 0.159.2` window.
