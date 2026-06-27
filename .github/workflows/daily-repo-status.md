---
description: |
  This workflow creates daily repo status reports. It gathers recent repository
  activity (issues, PRs, discussions, releases, code changes) and generates
  engaging GitHub issues with productivity insights, community highlights,
  and project recommendations.

on:
  schedule: daily
  workflow_dispatch:

permissions:
  contents: read
  issues: read
  pull-requests: read

strict: false

network: defaults

sandbox:
  agent:
    args:
      - --env-file
      - /tmp/gh-aw/codex-openai-agent.env

tools:
  github:
    # If in a public repo, setting `lockdown: false` allows
    # reading issues, pull requests and comments from 3rd-parties
    # If in a private repo this has no particular effect.
    lockdown: false
    min-integrity: none # This workflow is allowed to examine and comment on any issues

safe-outputs:
  mentions: false
  allowed-github-references: []
  threat-detection: false
  create-issue:
    title-prefix: "[repo-status] "
    labels: [report, daily-status]
    close-older-issues: true
engine:
  id: codex
  args:
    - -c
    - model_provider=repo-openai-proxy
    - -c
    - model_providers.repo-openai-proxy.name=OpenAI
    - -c
    - model_providers.repo-openai-proxy.base_url=http://host.docker.internal
    - -c
    - model_providers.repo-openai-proxy.env_key=CODEX_API_KEY
    - -c
    - model_providers.repo-openai-proxy.wire_api=responses
    - -c
    - model_providers.repo-openai-proxy.supports_websockets=false
  env:
    OPENAI_BASE_URL: "http://host.docker.internal"

pre-agent-steps:
  - name: Start Codex endpoint relay
    id: start-codex-endpoint-relay
    env:
      CODEX_LB_BASE_URL: ${{ secrets.CODEX_LB_BASE_URL }}
      CODEX_API_KEY: ${{ secrets.CODEX_API_KEY || secrets.OPENAI_API_KEY }}
      OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY || secrets.CODEX_API_KEY }}
    run: bash .github/scripts/start_codex_openai_relay.sh

post-steps:
  - name: Stop Codex endpoint relay
    if: always()
    run: bash .github/scripts/stop_codex_openai_relay.sh

source: githubnext/agentics/workflows/repo-status.md@1c6668b751c51af8571f01204ceffb19362e0f66
---

# Repo Status

Create an upbeat daily status report for the repo as a GitHub issue.

## What to include

- Recent repository activity (issues, PRs, discussions, releases, code changes)
- Progress tracking, goal reminders and highlights
- Project status and recommendations
- Actionable next steps for maintainers

## Style

- Be positive, encouraging, and helpful 🌟
- Use emojis moderately for engagement
- Keep it concise - adjust length based on actual activity

## Process

1. Gather recent activity from the repository
2. Study the repository, its issues and its pull requests
3. Create a new GitHub issue with your findings and insights
