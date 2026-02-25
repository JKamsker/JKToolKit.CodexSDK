# Upstream Sync (Schema + DTO Generation)

This repo keeps Codex app-server wire DTOs in sync with upstream by generating **internal** C# types from the upstream JSON Schema bundle.

## Version Pin

`UPSTREAM_CODEX_VERSION.txt` pins the target upstream Codex version (NPM `@openai/codex` version).

When bumping the upstream version, also update the `external/codex` submodule to the matching tag:

- `rust-v<version>` (example: `rust-v0.104.0`)

## Generator

The generator lives in `src/JKToolKit.CodexSDK.UpstreamGen/`.

Common workflows:

```bash
# (Optional) inspect schema metadata
dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen -- schema-info

# Regenerate internal DTOs into src/JKToolKit.CodexSDK/Generated/Upstream/
dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen -- generate

# Verify generated output is up-to-date (used by CI)
dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen -- check
```

## Manual Bump Checklist

1. Update `UPSTREAM_CODEX_VERSION.txt`
2. Update `external/codex` to the matching `rust-v<version>` tag
3. Run `UpstreamGen generate`
4. Run `dotnet test -c Release`
5. Commit the changes
