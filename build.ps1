<#
.SYNOPSIS
    Builds (and optionally runs) awoo.

.DESCRIPTION
    Finds a .NET 10 SDK, bootstrapping one into .dotnet/ if the machine has
    none, then builds src/Awoo. awoo is an unpackaged WinUI 3 app, so the
    result is a plain exe under src/Awoo/bin.

.PARAMETER Configuration
    Debug (default) or Release.

.PARAMETER Run
    Start the app once it has built.

.EXAMPLE
    .\build.ps1 -Run
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [switch] $Run
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$project = Join-Path $root 'src\Awoo\Awoo.csproj'
$sdkChannel = '10.0'

function Resolve-Dotnet {
    # An SDK already on PATH is the normal case.
    $onPath = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($onPath) {
        $sdks = & $onPath.Source --list-sdks 2>$null
        if ($LASTEXITCODE -eq 0 -and ($sdks | Where-Object { $_ -match '^10\.' })) {
            return $onPath.Source
        }
    }

    # Then one this script installed earlier, for the user or in the repo.
    foreach ($dir in @((Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'), (Join-Path $root '.dotnet'))) {
        $candidate = Join-Path $dir 'dotnet.exe'
        if (Test-Path $candidate) {
            $sdks = & $candidate --list-sdks 2>$null
            if ($LASTEXITCODE -eq 0 -and ($sdks | Where-Object { $_ -match '^10\.' })) {
                return $candidate
            }
        }
    }

    # Otherwise fetch one. Per-user, so no administrator rights are needed.
    $installDir = Join-Path $root '.dotnet'
    Write-Host "No .NET $sdkChannel SDK found; installing one into $installDir ..." -ForegroundColor Yellow

    $script = Join-Path $env:TEMP 'dotnet-install.ps1'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $script -UseBasicParsing
    & $script -Channel $sdkChannel -Quality GA -Architecture x64 -InstallDir $installDir -NoPath

    $installed = Join-Path $installDir 'dotnet.exe'
    if (-not (Test-Path $installed)) {
        throw "The .NET SDK install did not produce $installed."
    }

    return $installed
}

$dotnet = Resolve-Dotnet
Write-Host "Using $dotnet" -ForegroundColor DarkGray

# Ask MSBuild where the output goes rather than guessing at the path.
# -getProperty with no target evaluates the project without building it.
$targetDir = (& $dotnet msbuild $project -getProperty:TargetDir -p:Configuration=$Configuration | Select-Object -Last 1).Trim()
if (-not $targetDir) {
    throw 'Could not work out the build output directory.'
}

& $dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

$exe = Join-Path $targetDir 'awoo.exe'
Write-Host ""
Write-Host "Built $exe" -ForegroundColor Green

if ($Run) {
    if (-not (Test-Path $exe)) {
        throw "Expected the build to produce $exe."
    }

    # awoo is tray-only, so there is nothing to wait for on the console.
    Write-Host 'Starting awoo. Look for the speaker icon in the notification area; right-click it and choose Exit to quit.' -ForegroundColor Cyan
    Start-Process -FilePath $exe
}
