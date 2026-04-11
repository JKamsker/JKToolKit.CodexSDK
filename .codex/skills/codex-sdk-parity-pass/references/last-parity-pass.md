# Last Parity Pass

## Baseline

- Prior audited upgrade: `0.106.0 -> 0.118.0`
- Canonical repo note: `docs/codex-0.106-to-0.118-interop.md`
- Supporting session log used during the pass: `C:\Users\Jonas\.claude\projects\C--Users-Jonas-repos-private-JKamsker-JKamsker-CodexSDK\093a530a-9f40-4d15-b9a3-503e7a75747d.jsonl`
- PR opened during that pass: `#27`

## What the pass accomplished

- Bumped `external/codex` to `rust-v0.118.0`
- Updated `UPSTREAM_CODEX_VERSION.txt` to `0.118.0`
- Regenerated upstream DTOs and then fixed schema/codegen fallout
- Repeatedly audited SDK behavior against the vendored upstream until no actionable drift remained
- Fixed Linux CI issues caused by Windows-style path assumptions in tests and fixtures

## High-value parity fixes that landed

- Exec resume fallback now discovers newly created session files when local resume-target lookup misses
- CWD normalization now matches upstream semantics before resume-session filtering
- Session recency ordering now uses upstream-style filename timestamp plus UUID tie-breaks
- MCP tool parsing now requires `inputSchema`
- `turn/start` now preserves the full upstream `Turn` payload instead of flattening it away
- `CodexSessionRunner` was split to respect the local file-size guard

## Review follow-up fixes that also became part of the baseline

- `ResumeSessionAsync(SessionId)` was restored to strict id-only lookup
- TOML parsing stopped leaking non-profile sections into top-level config
- Resume fallback logic was tightened so an id-resolution miss can still fall back to `selectedSession.LogPath` in the edge case discovered during review

## End-state conclusion

- The repo was considered clean against vendored upstream `codex-cli` `0.118.0`
- Tests were green locally
- CI was green
- Review comments were addressed
- No remaining actionable drift was identified at that version

## How to use this baseline in a future pass

- Start by assuming the areas listed above were correct at `0.118.0`
- Re-open those areas only if the new upstream delta touches related files or a regression test shows drift
- If you need detail beyond this summary, read the canonical repo note and then inspect the commits around:
  - `480c45e`
  - `df1437e`
  - `3bb4a50`
  - `577f917`
  - `3802b44`
  - `0e07256`
  - `1fbec84`
  - `b9713f2`
