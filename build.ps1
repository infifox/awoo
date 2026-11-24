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

.PARAMETER Package
    Build what a release ships instead: a self-contained portable folder,
    that folder zipped, and the Inno Setup installer over it, all under
    artifacts/. Implies Release. Fetches Inno Setup's command-line compiler
    into .tools/ if the machine has none -- it needs no install and no
    administrator rights.

.EXAMPLE
    .\build.ps1 -Run

.EXAMPLE
    .\build.ps1 -Package
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [switch] $Run,

    [switch] $Package
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$project = Join-Path $root 'src\Awoo\Awoo.csproj'
$sdkChannel = '10.0'
$innoVersion = '6.7.3'

if ($Package) {
    # There is no such thing as a Debug release artifact.
    $Configuration = 'Release'
}

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

function Resolve-Iscc {
    # Inno Setup proper, where somebody has installed it.
    foreach ($dir in @("${env:ProgramFiles(x86)}\Inno Setup 6", "$env:ProgramFiles\Inno Setup 6")) {
        $candidate = Join-Path $dir 'ISCC.exe'
        if (Test-Path $candidate) { return $candidate }
    }

    # Then the copy this script fetched earlier.
    $toolsDir = Join-Path $root ".tools\innosetup-$innoVersion"
    $fetched = Join-Path $toolsDir 'tools\ISCC.exe'
    if (Test-Path $fetched) { return $fetched }

    # Otherwise fetch one. Inno Setup publishes its compiler as a NuGet
    # package, which is a zip: no installer and no administrator rights.
    Write-Host "No Inno Setup found; fetching $innoVersion into $toolsDir ..." -ForegroundColor Yellow

    # Saved as .zip rather than .nupkg, which is the same thing but is the
    # only extension Windows PowerShell's Expand-Archive will open.
    $zip = Join-Path $env:TEMP "tools.innosetup.$innoVersion.zip"
    $url = "https://api.nuget.org/v3-flatcontainer/tools.innosetup/$innoVersion/tools.innosetup.$innoVersion.nupkg"
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    Expand-Archive -Path $zip -DestinationPath $toolsDir -Force

    if (-not (Test-Path $fetched)) {
        throw "Tools.InnoSetup $innoVersion did not contain $fetched."
    }

    return $fetched
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

if ($Package) {
    $version = (& $dotnet msbuild $project -getProperty:Version -p:Configuration=$Configuration | Select-Object -Last 1).Trim()
    if (-not $version) {
        throw 'Could not read the project version.'
    }

    $artifacts = Join-Path $root 'artifacts'
    $portable = Join-Path $artifacts 'portable'
    $dist = Join-Path $artifacts 'dist'
    Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $dist | Out-Null

    # Self-contained, so the result runs on a machine with neither the .NET
    # runtime nor the Windows App Runtime installed.
    Write-Host ''
    Write-Host "Publishing awoo $version, self-contained ..." -ForegroundColor Cyan
    & $dotnet publish $project -c $Configuration -p:SelfContained=true -p:PublishDir=$portable
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed with exit code $LASTEXITCODE."
    }

    $zip = Join-Path $dist "awoo-$version-portable-win-x64.zip"
    Write-Host "Zipping the portable build ..." -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip -CompressionLevel Optimal

    $iscc = Resolve-Iscc
    Write-Host "Compiling the installer with $iscc ..." -ForegroundColor Cyan
    & $iscc (Join-Path $root 'packaging\awoo.iss') `
        "/DAppVersion=$version" "/DPayloadDir=$portable" "/DOutputDir=$dist"
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }

    Write-Host ''
    foreach ($artifact in Get-ChildItem $dist) {
        Write-Host ("{0,8:N1} MB  {1}" -f ($artifact.Length / 1MB), $artifact.FullName) -ForegroundColor Green
    }
}

if ($Run) {
    if (-not (Test-Path $exe)) {
        throw "Expected the build to produce $exe."
    }

    # awoo is tray-only, so there is nothing to wait for on the console.
    Write-Host 'Starting awoo. Look for the speaker icon in the notification area; right-click it and choose Exit to quit.' -ForegroundColor Cyan
    Start-Process -FilePath $exe
}
