param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "ReToolbox\ReToolbox.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\win-x64"
$installerScript = Join-Path $repoRoot "installer\ReToolbox.iss"

function Find-MSBuild {
    $onPath = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($found) { return $found }
    }

    throw "MSBuild not found. Install Visual Studio 2022 or Build Tools with .NET desktop development."
}

$msbuild = Find-MSBuild
$isccCandidates = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
) | Where-Object { $_ -and (Test-Path $_) }
$iscc = $isccCandidates | Select-Object -First 1

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Host "Publishing ReToolbox (x64)..."
& $msbuild $projectPath `
    -t:Restore,Publish `
    -p:Configuration=$Configuration `
    -p:Platform=x64 `
    -p:RuntimeIdentifier=win-x64 `
    -p:PublishDir=$publishDir `
    -v:minimal

if ($LASTEXITCODE -ne 0) {
    throw "MSBuild Publish failed with exit code: $LASTEXITCODE"
}

if (-not $iscc) {
    Write-Warning "Inno Setup 6 was not found. Publish output is ready at $publishDir."
    exit 0
}

Write-Host "Building installer..."
Write-Warning "Installer signing is not configured by this repository; generated output will be unsigned."
& $iscc "/DMyPublishDir=$publishDir" $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compile failed with exit code: $LASTEXITCODE"
}

Write-Host "Done."
