<#
    Creates a new manual testing run checklist file under `.tmp/manual_testing/<run_number>.md`.

    This is a local helper script. It does not run tests; it only scaffolds the per-run task document
    described in `docs/Runbooks/Manual-Testing/README.md`.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$OutDir = '.tmp\manual_testing',
    [int]$PadWidth = 4,

    # If 0, the next run number is inferred from existing run files.
    [int]$RunNumber = 0,

    # If omitted, defaults to $env:USERNAME (Windows) or $env:USER (Linux/macOS).
    [string]$Tester = '',

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-SafeExternalOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileName,
        [string[]]$Arguments = @()
    )

    try {
        $output = & $FileName @Arguments 2>$null
        if ($LASTEXITCODE -ne 0) {
            return $null
        }
        return ($output -join "`n").Trim()
    }
    catch {
        return $null
    }
}

if (-not (Test-Path -LiteralPath $RepoRoot)) {
    throw "RepoRoot not found: $RepoRoot"
}

$resolvedOutDir = Join-Path $RepoRoot $OutDir
$null = New-Item -ItemType Directory -Force -Path $resolvedOutDir

if ($RunNumber -lt 0) {
    throw "RunNumber must be >= 0 (0 = auto)."
}

$nextRunNumber = $RunNumber
if ($nextRunNumber -eq 0) {
    $maxExisting = 0
    $existing = @(Get-ChildItem -LiteralPath $resolvedOutDir -Filter '*.md' -File -ErrorAction SilentlyContinue)
    foreach ($file in $existing) {
        if ($file.BaseName -match '^(?<n>\d+)$') {
            $n = [int]$Matches['n']
            if ($n -gt $maxExisting) {
                $maxExisting = $n
            }
        }
    }

    $nextRunNumber = $maxExisting + 1
}

$runLabel = $nextRunNumber.ToString(('D' + $PadWidth))
$outFile = Join-Path $resolvedOutDir ($runLabel + '.md')

if ((Test-Path -LiteralPath $outFile) -and -not $Force) {
    throw "File already exists: $outFile (use -Force to overwrite)"
}

$dateUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm')
$defaultTester = $env:USERNAME
if ([string]::IsNullOrWhiteSpace($defaultTester)) { $defaultTester = $env:USER }
$testerValue = if ([string]::IsNullOrWhiteSpace($Tester)) { $defaultTester } else { $Tester }
if ([string]::IsNullOrWhiteSpace($testerValue)) { $testerValue = 'n/a' }

$branch = Get-SafeExternalOutput -FileName 'git' -Arguments @('-C', $RepoRoot, 'rev-parse', '--abbrev-ref', 'HEAD')
if ([string]::IsNullOrWhiteSpace($branch)) { $branch = 'n/a' }

$head = Get-SafeExternalOutput -FileName 'git' -Arguments @('-C', $RepoRoot, 'rev-parse', 'HEAD')
if ([string]::IsNullOrWhiteSpace($head)) { $head = 'n/a' }

$codexVersion = Get-SafeExternalOutput -FileName 'codex' -Arguments @('--version')
if ([string]::IsNullOrWhiteSpace($codexVersion)) { $codexVersion = 'n/a' }

$dotnetVersion = Get-SafeExternalOutput -FileName 'dotnet' -Arguments @('--version')
if ([string]::IsNullOrWhiteSpace($dotnetVersion)) { $dotnetVersion = 'n/a' }

$os = $null
try {
    $os = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
}
catch {
    $os = [System.Environment]::OSVersion.VersionString
}
if ([string]::IsNullOrWhiteSpace($os)) { $os = 'n/a' }

$template = @'
# Manual Testing Run __RUN_LABEL__

## Meta
- Date (UTC): __DATE_UTC__
- Tester: __TESTER__
- Branch: __BRANCH__
- HEAD: __HEAD__
- Codex CLI: __CODEX_CLI__
- .NET SDK: __DOTNET_SDK__
- OS: __OS__

## Status rules (strict)
- `[ ] [PENDING] ...` = not executed yet
- `[x] [PASS] ...`    = executed and passed
- `[x] [FAIL] ...`    = executed and failed (must include a Failure block)

## Test cases
- [ ] [PENDING] TC01 - Build + unit tests (`dotnet test`)
- [ ] [PENDING] TC02 - Exec: start/stream/resume (`demo exec`)
- [ ] [PENDING] TC03 - Exec: list sessions (`demo exec-list`)
- [ ] [PENDING] TC04 - Exec: attach to JSONL (`demo exec-attach`)
- [ ] [PENDING] TC05 - Structured output pipeline (`demo structured-review`)
- [ ] [PENDING] TC06 - Non-interactive review (commit scope) (`demo review --commit <sha>`)
- [ ] [PENDING] TC07 - App-server: stream deltas (`demo appserver-stream`)
- [ ] [PENDING] TC08 - App-server: typed + raw notifications (`demo appserver-notifications`)
- [ ] [PENDING] TC09 - App-server: steer + interrupt (`demo appserver-turn-control`)
- [ ] [PENDING] TC10 - App-server: approval handler (`demo appserver-approval`)
- [ ] [PENDING] TC11 - App-server: thread lifecycle commands (`demo appserver-thread ...`)
- [ ] [PENDING] TC12 - App-server: skills + apps (`demo appserver-skills-apps`)
- [ ] [PENDING] TC13 - App-server: config read (`demo appserver-config`)
- [ ] [PENDING] TC14 - App-server: config write (temp CODEX_HOME) (`demo appserver-config-write`)
- [ ] [PENDING] TC15 - App-server: MCP management (`demo appserver-mcp`)
- [ ] [PENDING] TC16 - App-server: fuzzy search (experimental) (`demo appserver-fuzzy --experimental-api`)
- [ ] [PENDING] TC17 - App-server: review/start (`demo appserver-review`)
- [ ] [PENDING] TC18 - App-server: resilient wrapper (`demo appserver-resilient-stream --restart-between-turns`)
- [ ] [PENDING] TC19 - MCP-server: tools + session + reply (`demo mcpserver`)
- [ ] [PENDING] TC20 - MCP-server: low-level escape hatches (`demo mcpserver --low-level`)
- [ ] [PENDING] TC21 - DI + override hooks (`demo di-overrides`) (see: `docs/Runbooks/Manual-Testing/DI-and-Overrides.md`)
- [ ] [PENDING] TC22 - Exec: override hooks (`demo exec-overrides`)
- [ ] [PENDING] TC23 - App-server: override hooks (`demo appserver-overrides`)
- [ ] [PENDING] TC24 - App-server: opt-out notification methods (`demo appserver-optout-notifications`)
- [ ] [PENDING] TC25 - App-server: output schema (`demo appserver-output-schema`)
- [ ] [PENDING] TC26 - App-server: sandbox policy (`demo appserver-sandbox-policy`)
- [ ] [PENDING] TC27 - App-server: collaboration mode (experimental) (`demo appserver-collaboration-mode`)
- [ ] [PENDING] TC28 - MCP-server: codex tool overrides + elicitation (`demo mcp-overrides`)
- [ ] [PENDING] TC29 - Facade: review routing (`demo sdk-review-route`)

## Failures
<!--
For each failed test case, append a block like:

### TCxx - <short title>
- Command:
- Expected:
- Actual:
- Error/output:
- Notes:
-->

## Summary
- Overall: [PENDING]
- Notes:

'@

$content = $template
$content = $content.Replace('__RUN_LABEL__', $runLabel)
$content = $content.Replace('__DATE_UTC__', $dateUtc)
$content = $content.Replace('__TESTER__', $testerValue)
$content = $content.Replace('__BRANCH__', $branch)
$content = $content.Replace('__HEAD__', $head)
$content = $content.Replace('__CODEX_CLI__', $codexVersion)
$content = $content.Replace('__DOTNET_SDK__', $dotnetVersion)
$content = $content.Replace('__OS__', $os)

Set-Content -LiteralPath $outFile -Value $content -Encoding utf8
Write-Information "Created manual testing run file: $outFile"
