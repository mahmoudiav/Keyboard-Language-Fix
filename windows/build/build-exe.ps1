<#
.SYNOPSIS
    Builds KeyboardLanguageFix.exe.

.DESCRIPTION
    The short path from a downloaded copy of this repository to a program you
    can run. Double-click build-exe.cmd, or run this script directly.

    The result is self-contained: it carries its own copy of .NET, so it runs on
    any Windows 10/11 machine with nothing else installed. That is why it is
    large — about 70 MB as a single file.

    This is the plain-executable path. For a Microsoft Store package, use
    build-msix.ps1 instead.

.PARAMETER Mode
    Single  - one .exe, easy to move around or put on a USB stick (default).
    Folder  - an .exe beside its DLLs. Starts a little faster.

.PARAMETER Architecture
    x64 for any normal PC. arm64 for Windows on ARM (Surface Pro X, Snapdragon).

.PARAMETER Output
    Where to put the result. Defaults to windows\dist\exe.

.EXAMPLE
    .\build-exe.ps1

.EXAMPLE
    .\build-exe.ps1 -Mode Folder -Architecture arm64
#>
[CmdletBinding()]
param(
    [ValidateSet('Single', 'Folder')]
    [string]$Mode = 'Single',

    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64',

    [string]$Output
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repoRoot 'windows\src\KeyboardLanguageFix.App\KeyboardLanguageFix.App.csproj'

if (-not (Test-Path $project)) {
    throw "Could not find the app project at $project. Run this script from inside the repository you downloaded."
}

if (-not $Output) {
    $Output = Join-Path $repoRoot 'windows\dist\exe'
}

Write-Host ''
Write-Host 'Keyboard Language Fix — building an executable' -ForegroundColor Cyan
Write-Host ''

# ---- Is the .NET SDK here? ------------------------------------------------

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host 'The .NET SDK is not installed.' -ForegroundColor Red
    Write-Host ''
    Write-Host 'Install it once, then run this again. Either:'
    Write-Host '  winget install Microsoft.DotNet.SDK.8'
    Write-Host 'or download it from:'
    Write-Host '  https://dotnet.microsoft.com/download/dotnet/8.0'
    Write-Host ''
    Write-Host 'Close and reopen this window after installing, so it picks up the new PATH.'
    exit 1
}

$sdks = & dotnet --list-sdks
$hasSdk8OrNewer = $sdks | Where-Object { $_ -match '^(\d+)\.' -and [int]$Matches[1] -ge 8 }
if (-not $hasSdk8OrNewer) {
    Write-Host 'A .NET SDK is installed, but this app needs version 8.0 or newer.' -ForegroundColor Red
    Write-Host 'Found:'
    $sdks | ForEach-Object { Write-Host "  $_" }
    Write-Host ''
    Write-Host 'Install the current one with:  winget install Microsoft.DotNet.SDK.8'
    exit 1
}

Write-Host "Using .NET SDK $(& dotnet --version)"
Write-Host "Target:      win-$Architecture"
Write-Host "Mode:        $Mode"
Write-Host "Output:      $Output"
Write-Host ''

# ---- Build ----------------------------------------------------------------

if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }

$arguments = @(
    'publish', $project,
    '-c', 'Release',
    '-r', "win-$Architecture",
    '--self-contained', 'true',
    '-p:DebugType=none',
    '-p:PublishDocumentationFiles=false',
    '-p:PublishReferencesDocumentationFiles=false',
    '-o', $Output
)

if ($Mode -eq 'Single') {
    $arguments += @(
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true'
    )
}

Write-Host 'Building. The first run downloads the .NET runtime for the target, so give it a minute...' -ForegroundColor Cyan
Write-Host ''

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host 'The build failed. The output above says why.' -ForegroundColor Red
    exit 1
}

# ---- Report ---------------------------------------------------------------

$exe = Join-Path $Output 'KeyboardLanguageFix.exe'
if (-not (Test-Path $exe)) {
    throw "The build finished but $exe is missing."
}

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)

Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
Write-Host ''
Write-Host "  $exe  ($sizeMb MB)"
Write-Host ''
Write-Host 'To use it:'
Write-Host '  1. Double-click the file. It has no window — look for its icon in the'
Write-Host '     notification area, next to the clock (you may need to click the ^ arrow).'
Write-Host '  2. Select some text anywhere, then press Ctrl+Shift+Space.'
Write-Host '  3. Double-click the tray icon for settings.'
Write-Host ''
Write-Host 'Windows SmartScreen will warn the first time, because the file is not'
Write-Host 'code-signed. Choose "More info" then "Run anyway".'
Write-Host ''

if ($Mode -eq 'Single') {
    Write-Host 'This is a single file: copy it anywhere you like, it needs nothing beside it.'
}
else {
    Write-Host 'Keep the whole folder together — the .exe needs the files next to it.'
}

# Open the folder in Explorer when a person ran this, rather than a script.
if ([Environment]::UserInteractive -and $env:OS -eq 'Windows_NT') {
    Start-Process explorer.exe $Output
}
