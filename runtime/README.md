# Optional Codex runtimes

`runtime-lock.json` pins the six official npm platform archives to the API
version in `UPSTREAM_CODEX_VERSION.json` and records their SHA-512 integrity.
`LICENSE-CODEX` and `NOTICE-CODEX` are copied from that upstream revision.

Build a package with:

```sh
python3 scripts/pack-codex-runtime.py --rid linux-x64
```

Omit `--rid` to build all six packages. Use `--update-lock` after an intentional
upstream pin change, inspect the URLs and hashes, and refresh the upstream
license/notice if needed. The script refuses a stale lock and rejects archive
links or traversal paths. Downloaded archives and staging output are ignored
under `artifacts/runtime`; packages go to `nuget-packages`.

Packages contain the complete official vendor layout, including helper tools
and third-party notices. They copy into `codex-runtime/<version>/<rid>` on build
and publish. They do not execute a download at application startup. The SDK
selects only its own version and process architecture; explicit paths override
bundles. See [the SDK guide](../docs/high-level-sdk.md) for consumer usage and
cross-publish executable-permission requirements.
