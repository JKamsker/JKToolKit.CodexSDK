#!/usr/bin/env bash
set -euo pipefail

if [ ! -s /tmp/gh-aw/codex-openai-relay.pid ]; then
  rm -f /tmp/gh-aw/codex-openai-agent.env
  exit 0
fi

relay_pid="$(cat /tmp/gh-aw/codex-openai-relay.pid)"
sudo kill "$relay_pid" 2>/dev/null || true
rm -f /tmp/gh-aw/codex-openai-relay.pid /tmp/gh-aw/codex-openai-agent.env
