#!/usr/bin/env bash
set -euo pipefail

if [ -z "${CODEX_LB_BASE_URL:-}" ]; then
  echo "::error::CODEX_LB_BASE_URL secret is required for the Codex custom endpoint."
  exit 1
fi

codex_key="${CODEX_API_KEY:-${OPENAI_API_KEY:-}}"
openai_key="${OPENAI_API_KEY:-$codex_key}"
if [ -z "$codex_key" ]; then
  echo "::error::CODEX_API_KEY or OPENAI_API_KEY secret is required for Codex."
  exit 1
fi

case "$codex_key$openai_key" in
  *$'\n'*|*$'\r'*)
    echo "::error::Codex API key secrets must not contain newlines."
    exit 1
    ;;
esac

echo "::add-mask::${CODEX_LB_BASE_URL}"
echo "::add-mask::${codex_key}"
echo "::add-mask::${openai_key}"
mkdir -p /tmp/gh-aw
umask 077
{
  printf 'CODEX_API_KEY=%s\n' "$codex_key"
  printf 'OPENAI_API_KEY=%s\n' "$openai_key"
} > /tmp/gh-aw/codex-openai-agent.env

export CODEX_OPENAI_RELAY_HOST="${CODEX_OPENAI_RELAY_HOST:-0.0.0.0}"
export CODEX_OPENAI_RELAY_PORT="${CODEX_OPENAI_RELAY_PORT:-80}"

python_path="$(command -v python3)"
sudo --preserve-env=CODEX_LB_BASE_URL,CODEX_OPENAI_RELAY_HOST,CODEX_OPENAI_RELAY_PORT \
  bash -c 'nohup "$1" .github/scripts/codex_openai_relay.py > /tmp/gh-aw/codex-openai-relay.log 2>&1 & echo "$!" > /tmp/gh-aw/codex-openai-relay.pid' \
  bash "$python_path"

for _ in $(seq 1 20); do
  if curl -fsS --max-time 2 "http://127.0.0.1:${CODEX_OPENAI_RELAY_PORT}/__codex_relay_health" >/dev/null; then
    exit 0
  fi
  if ! sudo kill -0 "$(cat /tmp/gh-aw/codex-openai-relay.pid)" 2>/dev/null; then
    echo "::error::Codex endpoint relay exited before it became healthy."
    tail -50 /tmp/gh-aw/codex-openai-relay.log || true
    exit 1
  fi
  sleep 1
done

echo "::error::Codex endpoint relay did not become healthy."
tail -50 /tmp/gh-aw/codex-openai-relay.log || true
exit 1
