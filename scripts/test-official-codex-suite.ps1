<#
    Runs the full JKToolKit.CodexSDK test suite against an official Codex CLI release
    downloaded via GitHub CLI. By default, the Codex version is read from
    UPSTREAM_CODEX_VERSION.txt and the gated E2E/smoke tests are enabled.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$Solution = 'JKToolKit.CodexSDK.sln',
    [string]$Configuration = 'Release',
    [string]$CodexVersion = '',
    [string]$ResultsDir = 'artifacts\test-results',
    [string]$ToolsDir = '.tmp\tools\codex',
    [switch]$ForceDownload,
    [switch]$SkipE2E,
    [switch]$SkipSessionJsonlSmoke,
    [string[]]$AdditionalDotnetTestArguments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Require-Command {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "Required command not found on PATH: $Name"
    }
}

function Get-UpstreamCodexVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRootPath
    )

    $versionFile = Join-Path $RepoRootPath 'UPSTREAM_CODEX_VERSION.txt'
    if (-not (Test-Path -LiteralPath $versionFile)) {
        throw "UPSTREAM_CODEX_VERSION.txt not found: $versionFile"
    }

    $version = (Get-Content -LiteralPath $versionFile | Select-Object -First 1).Trim()
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "UPSTREAM_CODEX_VERSION.txt does not contain a version."
    }

    return $version
}

function Get-WindowsAssetName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    $assetArch = switch ($arch) {
        'X64' { 'x64' }
        'Arm64' { 'arm64' }
        default { throw "Unsupported Windows architecture for official Codex npm asset: $arch" }
    }

    return "codex-npm-win32-$assetArch-$Version.tgz"
}

function Get-ReleaseAsset {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseTag,
        [Parameter(Mandatory = $true)]
        [string]$AssetName
    )

    $json = & gh release view $ReleaseTag -R openai/codex --json assets
    if ($LASTEXITCODE -ne 0) {
        throw "gh release view failed for $ReleaseTag"
    }

    $release = ($json -join "`n") | ConvertFrom-Json
    $asset = $release.assets | Where-Object { $_.name -eq $AssetName } | Select-Object -First 1
    if ($null -eq $asset) {
        throw "Release asset not found on ${ReleaseTag}: $AssetName"
    }

    return $asset
}

function Remove-DirectoryIfPresent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$AllowedRoot
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    $resolvedRoot = (Resolve-Path -LiteralPath $AllowedRoot).Path
    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove path outside allowed root. Path: $resolvedPath Root: $resolvedRoot"
    }

    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
}

function Get-CodexExecutable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PrefixDir
    )

    if (-not (Test-Path -LiteralPath $PrefixDir)) {
        return $null
    }

    return Get-ChildItem -Path $PrefixDir -Filter 'codex.exe' -Recurse -File |
        Select-Object -First 1
}

function Install-OfficialCodex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRootPath,
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$ToolsRoot,
        [switch]$Force
    )

    $releaseTag = "rust-v$Version"
    $assetName = Get-WindowsAssetName -Version $Version
    $asset = Get-ReleaseAsset -ReleaseTag $releaseTag -AssetName $assetName

    $versionDir = Join-Path $ToolsRoot $Version
    $prefixDir = Join-Path $versionDir 'prefix'
    $assetPath = Join-Path $versionDir $asset.name

    $null = New-Item -ItemType Directory -Force -Path $versionDir

    if ($Force -or -not (Test-Path -LiteralPath $assetPath)) {
        Write-Host "Downloading $assetName from $releaseTag via gh..."
        $null = & gh release download $releaseTag -R openai/codex -p $asset.name -D $versionDir
        if ($LASTEXITCODE -ne 0) {
            throw "gh release download failed for $assetName"
        }
    }

    $codexExe = Get-CodexExecutable -PrefixDir $prefixDir
    if ($Force -or $null -eq $codexExe) {
        Write-Host "Installing $assetName into $prefixDir via npm..."
        Remove-DirectoryIfPresent -Path $prefixDir -AllowedRoot $versionDir
        $null = New-Item -ItemType Directory -Force -Path $prefixDir

        $null = & npm install --global --prefix $prefixDir $assetPath
        if ($LASTEXITCODE -ne 0) {
            throw "npm install failed for $assetPath"
        }

        $codexExe = Get-CodexExecutable -PrefixDir $prefixDir
    }

    if ($null -eq $codexExe) {
        throw "Unable to locate codex.exe under $prefixDir after installation."
    }

    return $codexExe
}

if (-not $IsWindows) {
    throw 'This helper currently supports Windows only.'
}

if (-not (Test-Path -LiteralPath $RepoRoot)) {
    throw "RepoRoot not found: $RepoRoot"
}

Require-Command -Name 'gh'
Require-Command -Name 'npm'
Require-Command -Name 'dotnet'

if ([string]::IsNullOrWhiteSpace($CodexVersion)) {
    $CodexVersion = Get-UpstreamCodexVersion -RepoRootPath $RepoRoot
}

$resolvedToolsRoot = Join-Path $RepoRoot $ToolsDir
$null = New-Item -ItemType Directory -Force -Path $resolvedToolsRoot

$codexExe = Install-OfficialCodex -RepoRootPath $RepoRoot -Version $CodexVersion -ToolsRoot $resolvedToolsRoot -Force:$ForceDownload
$codexDir = $codexExe.Directory.FullName

$resultsPath = Join-Path $RepoRoot $ResultsDir
$null = New-Item -ItemType Directory -Force -Path $resultsPath

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$trxName = "codexsdk-$CodexVersion-$timestamp.trx"
$solutionPath = Join-Path $RepoRoot $Solution

$originalPath = $env:PATH
$originalCodexE2E = $env:CODEX_E2E
$originalSessionSmoke = $env:CODEX_SESSION_JSONL_SMOKE

try {
    $env:PATH = "$codexDir;$originalPath"
    if (-not $SkipE2E) {
        $env:CODEX_E2E = '1'
    }

    if (-not $SkipSessionJsonlSmoke) {
        $env:CODEX_SESSION_JSONL_SMOKE = '1'
    }

    $codexVersionOutput = (& codex --version) -join "`n"
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to execute codex --version after PATH override.'
    }

    Write-Host "RepoRoot: $RepoRoot"
    Write-Host "Codex CLI: $codexVersionOutput"
    Write-Host "codex.exe: $($codexExe.FullName)"
    Write-Host "ResultsDir: $resultsPath"
    Write-Host "CODEX_E2E: $($env:CODEX_E2E)"
    Write-Host "CODEX_SESSION_JSONL_SMOKE: $($env:CODEX_SESSION_JSONL_SMOKE)"

    $dotnetArgs = @(
        'test',
        $solutionPath,
        '-c', $Configuration,
        '--results-directory', $resultsPath,
        '--logger', "trx;LogFileName=$trxName"
    )

    if ($AdditionalDotnetTestArguments.Count -gt 0) {
        $dotnetArgs += $AdditionalDotnetTestArguments
    }

    & dotnet @dotnetArgs
    $exitCode = $LASTEXITCODE
}
finally {
    $env:PATH = $originalPath
    $env:CODEX_E2E = $originalCodexE2E
    $env:CODEX_SESSION_JSONL_SMOKE = $originalSessionSmoke
}

if ($exitCode -ne 0) {
    throw "dotnet test failed with exit code $exitCode. See $resultsPath\\$trxName"
}

Write-Host "dotnet test succeeded. TRX: $(Join-Path $resultsPath $trxName)"
