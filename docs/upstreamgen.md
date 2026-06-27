# Upstream Sync (Schema + DTO Generation)

This repo keeps Codex app-server wire DTOs in sync with upstream by generating **internal** C# types from the upstream JSON Schema bundle.

## Version Pin

`UPSTREAM_CODEX_VERSION.json` tracks upstream Codex versions:

- `api`: the version used for generated upstream schema/DTO artifacts. The upstream sync workflow updates this field.
- `integration`: the version whose deeper handwritten SDK parity pass is complete. The gh-aw parity workflow updates this field after validation.

When bumping the API version, also update the `external/codex` submodule to the matching tag:

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

1. Update `UPSTREAM_CODEX_VERSION.json` `api`
2. Update `external/codex` to the matching `rust-v<version>` tag
3. Run `UpstreamGen generate`
4. Run the deeper parity pass
5. Update `UPSTREAM_CODEX_VERSION.json` `integration` after parity is complete
6. Run `dotnet test -c Release`
7. Commit the changes
