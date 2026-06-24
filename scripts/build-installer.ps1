param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "ReToolbox\ReToolbox.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\win-x64-new"
$installerScript = Join-Path $repoRoot "installer\ReToolbox.iss"

$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not (Test-Path $msbuild)) {
    throw "MSBuild not found: $msbuild"
}

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Host "Publishing ReToolbox..."
& $msbuild $projectPath `
    /t:Restore,Publish `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform `
    /p:RuntimeIdentifier=win-x64 `
    /p:PublishDir=$publishDir `
    /v:minimal

if ($LASTEXITCODE -ne 0) {
    throw "MSBuild Publish failed with exit code: $LASTEXITCODE"
}

if (-not $iscc) {
    Write-Warning "Inno Setup 6 was not found. Publish output is ready."
    Write-Warning "Install Inno Setup 6 and rerun this script, or compile manually: $installerScript"
    exit 0
}

Write-Host "Building installer..."
& $iscc $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compile failed with exit code: $LASTEXITCODE"
}

Write-Host "Done."
