# Builds the self-contained win-x64 publish output and wraps it in the Inno Setup installer,
# stamped with the given version. Called by semantic-release's `prepareCmd` (see .releaserc.json)
# during the release job in .github/workflows/release.yml, after the next version is known but
# before @semantic-release/github uploads release assets — so this must finish with
# Output\PulsemapSetup-<Version>.exe (and its .sha256 sidecar) on disk before it returns.
#
# Also runnable locally the same way CI runs it, e.g.:
#   pwsh -File scripts/build-installer.ps1 -Version 1.2.3

param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

# Locked mode, matching how every other restore in this repo's CI runs — fails loudly on
# packages.lock.json drift instead of silently resolving something newer.
dotnet publish src\Pulsemap.App\Pulsemap.App.csproj -c Release -p:PublishProfile=win-x64 -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# CI installs Inno Setup fresh via Chocolatey and adds it to PATH (see release.yml) since
# windows-latest no longer ships it preinstalled. Locally, developers may instead have it from
# a manual install at either of these well-known locations (see Pulsemap.iss's own header
# comment) — check PATH first, then fall back to both.
$isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($null -ne $isccCommand) {
    $isccPath = $isccCommand.Source
}
else {
    $candidatePaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 7\ISCC.exe"
    )
    $isccPath = $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($null -eq $isccPath) {
        throw "ISCC.exe not found on PATH or in the usual Inno Setup install locations."
    }
}

& $isccPath "/DMyAppVersion=$Version" Pulsemap.iss
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE"
}

$installerPath = "Output\PulsemapSetup-$Version.exe"
if (-not (Test-Path $installerPath)) {
    throw "Expected installer not found at $installerPath after a successful ISCC run."
}

# ADR-0002 commits to publishing a checksum alongside the unsigned installer.
$hash = (Get-FileHash -Path $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $installerPath -Leaf)" | Out-File -FilePath "$installerPath.sha256" -Encoding utf8 -NoNewline

Write-Host "Installer built: $installerPath"
