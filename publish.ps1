# ============================================================================
# publish.ps1 - Windows PowerShell equivalent of publish.sh
# ============================================================================
#
# Bumps the semantic version, runs the desktop test suite (LGPL + GPL),
# packs all NuGet packages with the new version, pushes them to nuget.org,
# then commits the version bump and creates a git tag.
#
# Usage:
#   ./publish.ps1 [-Bump major|minor|patch]
#
# Parameters:
#   -Bump  Version bump type. Default: patch
#
# Prerequisites:
#   - .NET SDK on PATH (any version >= net8.0; DOTNET_ROLL_FORWARD handles SDK gap)
#   - NUGET_API_KEY environment variable set to a valid NuGet API key
#   - Clean git working tree recommended
#
# Differences from publish.sh:
#   - Skips Linux package-integration and Android on-device tests (Windows host).
#   - Runs the FULL suite on both licence variants. The two stream tests that used to be
#     skipped here held a FileStream open while probing the same path; they are fixed, not
#     filtered.
# ============================================================================

[CmdletBinding()]
param(
    [ValidateSet('major','minor','patch')]
    [string]$Bump = 'patch'
)

$ErrorActionPreference = 'Stop'

$ScriptDir   = $PSScriptRoot
$VersionFile = Join-Path $ScriptDir 'VERSION'
$ReleaseDir  = Join-Path $ScriptDir 'release'
$NugetSource = 'https://api.nuget.org/v3/index.json'

if (-not $env:NUGET_API_KEY) {
    throw 'NUGET_API_KEY environment variable is not set'
}

# Tests target net8.0; we ship with net9/10 SDKs on this machine, so roll forward.
$env:DOTNET_ROLL_FORWARD = 'LatestMajor'

# ── Version management ─────────────────────────────────────────────────────────
# Reads the current version from the VERSION file, bumps it according to the
# requested level, and computes the new version string.

if (-not (Test-Path $VersionFile)) {
    [System.IO.File]::WriteAllText($VersionFile, "1.0.0`n")
}

$current = (Get-Content $VersionFile -Raw).Trim()
Write-Host "Current version: $current"

$parts = $current.Split('.')
$major = [int]$parts[0]; $minor = [int]$parts[1]; $patch = [int]$parts[2]

switch ($Bump) {
    'major' { $major++; $minor = 0; $patch = 0 }
    'minor' { $minor++; $patch = 0 }
    'patch' { $patch++ }
}

$newVersion = "$major.$minor.$patch"
Write-Host "New version: $newVersion"

# ── Run desktop tests (LGPL + GPL) ─────────────────────────────────────────────
# The whole suite runs on both licence variants. Two things here are load-bearing:
#
#   1. bin/obj are DELETED between variants. UseGPL selects the native runtime through a
#      ProjectReference condition, and an incremental build does not re-evaluate it — so running the
#      GPL leg over the LGPL leg's output silently re-tests LGPL. Both legs pass, and libx264 is
#      never loaded once.
#   2. LOXIFI_REQUIRE_GPL turns "libx264 is missing, so skip" into a failure on the GPL leg. Without
#      it a skipped H.264 test is indistinguishable from a passing one, which is what let (1) hide.

Write-Host ''
Write-Host '========================================'
Write-Host '  Running desktop tests (LGPL + GPL)'
Write-Host '========================================'

$testProject = Join-Path $ScriptDir 'tests/Loxifi.FFmpeg.Tests/Loxifi.FFmpeg.Tests.csproj'
$testDir     = Split-Path $testProject -Parent

foreach ($variant in @(@{License='LGPL'; UseGPL='false'}, @{License='GPL'; UseGPL='true'})) {
    Write-Host ''
    Write-Host "=== Desktop Tests ($($variant.License)) ==="

    foreach ($stale in @('bin','obj')) {
        $path = Join-Path $testDir $stale
        if (Test-Path $path) { Remove-Item $path -Recurse -Force }
    }

    $env:LOXIFI_REQUIRE_GPL = if ($variant.UseGPL -eq 'true') { '1' } else { '' }
    try {
        & dotnet test $testProject -c Release -p:UseGPL=$($variant.UseGPL) --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Desktop $($variant.License) tests FAILED"
        }
    }
    finally {
        $env:LOXIFI_REQUIRE_GPL = ''
    }
}

Write-Host ''
Write-Host 'All tests passed.'

# ── Build and pack ─────────────────────────────────────────────────────────────
# Packs all projects (core library + all runtime packages) with the new version.

Write-Host ''
Write-Host '========================================'
Write-Host "  Building and packing v$newVersion"
Write-Host '========================================'

if (Test-Path $ReleaseDir) { Remove-Item -Recurse -Force $ReleaseDir }
New-Item -ItemType Directory -Path $ReleaseDir | Out-Null

$projects = @(
    'src/Loxifi.FFmpeg/Loxifi.FFmpeg.csproj',
    'src/Loxifi.FFmpeg.Runtime.linux-x64/Loxifi.FFmpeg.Runtime.linux-x64.csproj',
    'src/Loxifi.FFmpeg.Runtime.win-x64/Loxifi.FFmpeg.Runtime.win-x64.csproj',
    'src/Loxifi.FFmpeg.Runtime.android-arm64/Loxifi.FFmpeg.Runtime.android-arm64.csproj',
    'src/Loxifi.FFmpeg.Runtime.linux-x64.GPL/Loxifi.FFmpeg.Runtime.linux-x64.GPL.csproj',
    'src/Loxifi.FFmpeg.Runtime.win-x64.GPL/Loxifi.FFmpeg.Runtime.win-x64.GPL.csproj',
    'src/Loxifi.FFmpeg.Runtime.android-arm64.GPL/Loxifi.FFmpeg.Runtime.android-arm64.GPL.csproj'
)

foreach ($proj in $projects) {
    Write-Host "Packing $proj..."
    & dotnet pack (Join-Path $ScriptDir $proj) `
        -c Release `
        -o $ReleaseDir `
        -p:PackageVersion=$newVersion `
        --nologo
    if ($LASTEXITCODE -ne 0) { throw "Pack failed: $proj" }
}

Write-Host ''
Write-Host 'Packages:'
Get-ChildItem -Path $ReleaseDir -Filter '*.nupkg' | ForEach-Object { Write-Host $_.FullName }

# ── Push to NuGet ──────────────────────────────────────────────────────────────
# --skip-duplicate avoids errors if a package version was already published
# (e.g. from a partial previous run).

Write-Host ''
Write-Host '========================================'
Write-Host '  Pushing to NuGet'
Write-Host '========================================'

foreach ($pkg in Get-ChildItem -Path $ReleaseDir -Filter '*.nupkg') {
    Write-Host "Pushing $($pkg.Name)..."
    & dotnet nuget push $pkg.FullName `
        --api-key $env:NUGET_API_KEY `
        --source $NugetSource `
        --skip-duplicate
    if ($LASTEXITCODE -ne 0) { throw "Push failed: $($pkg.Name)" }
}

# ── Update version file, commit, tag, and push ─────────────────────────────────

[System.IO.File]::WriteAllText($VersionFile, "$newVersion`n")

& git add $VersionFile
if ($LASTEXITCODE -ne 0) { throw 'git add failed' }

$commitMessage = @"
Bump version to $newVersion

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
"@

& git commit -m $commitMessage
if ($LASTEXITCODE -ne 0) { throw 'git commit failed' }

& git tag "v$newVersion"
if ($LASTEXITCODE -ne 0) { throw 'git tag failed' }

& git push
if ($LASTEXITCODE -ne 0) { throw 'git push failed' }

& git push --tags
if ($LASTEXITCODE -ne 0) { throw 'git push --tags failed' }

Write-Host ''
Write-Host '========================================'
Write-Host "  Published v$newVersion to NuGet"
Write-Host '========================================'
