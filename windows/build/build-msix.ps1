<#
.SYNOPSIS
    Publishes the app and packs it into an MSIX for the Microsoft Store.

.DESCRIPTION
    Run this on Windows with the .NET 8 SDK and the Windows SDK installed
    (makeappx.exe and signtool.exe come from the Windows SDK).

    For a Store submission, do NOT sign the package: Partner Center signs it
    with your publisher certificate on upload. Signing is only for sideloading
    a test build on your own machine.

.PARAMETER Identity
    The Identity/Name reserved in Partner Center, e.g. 12345Contoso.KeyboardLanguageFix

.PARAMETER Publisher
    The Publisher string from Partner Center, e.g. CN=ABCD1234-...-1234567890AB

.PARAMETER PublisherDisplayName
    Your publisher display name, exactly as it appears in Partner Center.

.PARAMETER Version
    Four-part package version. The Store requires the last part to be 0.

.PARAMETER Architectures
    Which architectures to build. Both produces a .msixbundle.

.PARAMETER SelfContained
    Bundle the .NET runtime. Leave this on for Store builds: the Store cannot
    install the .NET Desktop Runtime as a dependency for a Win32 package.

.PARAMETER SignWithSelfSigned
    Create (or reuse) a self-signed certificate and sign the package so it can
    be sideloaded for testing. Never use this for a Store submission.

.EXAMPLE
    .\build-msix.ps1 -Identity 12345Contoso.KeyboardLanguageFix `
                     -Publisher "CN=ABCD1234-1234-1234-1234-1234567890AB" `
                     -PublisherDisplayName "Contoso"

.EXAMPLE
    .\build-msix.ps1 -SignWithSelfSigned    # a sideloadable test build
#>
[CmdletBinding()]
param(
    [string]$Identity = 'PUBLISHER.KeyboardLanguageFix',
    [string]$Publisher = 'CN=REPLACE-WITH-YOUR-PARTNER-CENTER-PUBLISHER-ID',
    [string]$PublisherDisplayName = 'REPLACE-WITH-YOUR-PUBLISHER-DISPLAY-NAME',
    [ValidatePattern('^\d+\.\d+\.\d+\.0$')]
    [string]$Version = '1.0.0.0',
    [ValidateSet('x64', 'arm64', 'both')]
    [string]$Architectures = 'x64',
    [bool]$SelfContained = $true,
    [switch]$SignWithSelfSigned
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$appProject = Join-Path $repoRoot 'windows\src\KeyboardLanguageFix.App\KeyboardLanguageFix.App.csproj'
$packagingDir = Join-Path $repoRoot 'windows\packaging'
$outputDir = Join-Path $repoRoot 'windows\dist'

function Find-WindowsSdkTool([string]$name) {
    $tool = Get-Command $name -ErrorAction SilentlyContinue
    if ($tool) { return $tool.Source }

    $roots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "$env:ProgramFiles\Windows Kits\10\bin"
    ) | Where-Object { $_ -and (Test-Path $_) }

    $found = $roots |
        ForEach-Object { Get-ChildItem $_ -Recurse -Filter $name -ErrorAction SilentlyContinue } |
        Where-Object { $_.FullName -match '\\(x64|x86)\\' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $found) {
        throw "$name was not found. Install the Windows SDK (or Visual Studio with the " +
              "'Windows 10/11 SDK' component) and try again."
    }
    return $found.FullName
}

function Write-AppxManifest {
    <#
        Edits the manifest through the XML DOM rather than by string replacement.
        Attribute names repeat across this document — Identity, TargetDeviceFamily
        and Capability all carry a "Name" — so a text substitution would happily
        corrupt the wrong element.

        Every value is an explicit parameter: PowerShell variable names are
        case-insensitive, so a local named $identity would otherwise shadow the
        script's $Identity parameter.
    #>
    param(
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$DestinationPath,
        [Parameter(Mandatory)][string]$PackageName,
        [Parameter(Mandatory)][string]$PackagePublisher,
        [Parameter(Mandatory)][string]$PackageVersion,
        [Parameter(Mandatory)][string]$PackageArchitecture,
        [Parameter(Mandatory)][string]$DisplayName
    )

    $document = New-Object System.Xml.XmlDocument
    $document.PreserveWhitespace = $true
    $document.Load($SourcePath)

    $namespaces = New-Object System.Xml.XmlNamespaceManager($document.NameTable)
    $namespaces.AddNamespace('m', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')

    $identityNode = $document.SelectSingleNode('/m:Package/m:Identity', $namespaces)
    if (-not $identityNode) { throw 'AppxManifest.xml has no Identity element.' }
    $identityNode.SetAttribute('Name', $PackageName)
    $identityNode.SetAttribute('Publisher', $PackagePublisher)
    $identityNode.SetAttribute('Version', $PackageVersion)
    $identityNode.SetAttribute('ProcessorArchitecture', $PackageArchitecture)

    $displayNameNode = $document.SelectSingleNode(
        '/m:Package/m:Properties/m:PublisherDisplayName', $namespaces)
    if (-not $displayNameNode) { throw 'AppxManifest.xml has no PublisherDisplayName element.' }
    $displayNameNode.InnerText = $DisplayName

    $document.Save($DestinationPath)
}

function New-PackageLayout([string]$runtime, [string]$architecture) {
    $layout = Join-Path $outputDir "layout-$architecture"
    if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
    New-Item $layout -ItemType Directory -Force | Out-Null

    Write-Host "Publishing $runtime..." -ForegroundColor Cyan
    $publishArgs = @(
        'publish', $appProject,
        '-c', 'Release',
        '-r', $runtime,
        '--self-contained', $SelfContained.ToString().ToLowerInvariant(),
        '-p:DebugType=none',
        '-p:PublishSingleFile=false',
        '-o', $layout
    )
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $runtime." }

    # Package assets and manifest sit beside the binaries.
    Copy-Item (Join-Path $packagingDir 'Images') $layout -Recurse -Force

    Write-AppxManifest `
        -SourcePath (Join-Path $packagingDir 'AppxManifest.xml') `
        -DestinationPath (Join-Path $layout 'AppxManifest.xml') `
        -PackageName $Identity `
        -PackagePublisher $Publisher `
        -PackageVersion $Version `
        -PackageArchitecture $architecture `
        -DisplayName $PublisherDisplayName

    # .pdb and .xml files are build output, not part of a shipping package.
    Get-ChildItem $layout -Include *.pdb, *.xml -Recurse -File |
        Where-Object { $_.Name -ne 'AppxManifest.xml' } |
        Remove-Item -Force

    return $layout
}

New-Item $outputDir -ItemType Directory -Force | Out-Null
$makeappx = Find-WindowsSdkTool 'makeappx.exe'

$targets = switch ($Architectures) {
    'both'  { @(@{ Runtime = 'win-x64'; Arch = 'x64' }, @{ Runtime = 'win-arm64'; Arch = 'arm64' }) }
    'arm64' { @(@{ Runtime = 'win-arm64'; Arch = 'arm64' }) }
    default { @(@{ Runtime = 'win-x64'; Arch = 'x64' }) }
}

$packages = @()
foreach ($target in $targets) {
    $layout = New-PackageLayout $target.Runtime $target.Arch
    $msix = Join-Path $outputDir "KeyboardLanguageFix-$Version-$($target.Arch).msix"

    Write-Host "Packing $msix..." -ForegroundColor Cyan
    & $makeappx pack /o /d $layout /p $msix
    if ($LASTEXITCODE -ne 0) { throw 'makeappx pack failed.' }

    Remove-Item $layout -Recurse -Force
    $packages += $msix
}

$artifact = $packages[0]

if ($packages.Count -gt 1) {
    $bundleDir = Join-Path $outputDir 'bundle'
    New-Item $bundleDir -ItemType Directory -Force | Out-Null
    $packages | ForEach-Object { Move-Item $_ $bundleDir -Force }

    $artifact = Join-Path $outputDir "KeyboardLanguageFix-$Version.msixbundle"
    Write-Host "Bundling $artifact..." -ForegroundColor Cyan
    & $makeappx bundle /o /d $bundleDir /p $artifact
    if ($LASTEXITCODE -ne 0) { throw 'makeappx bundle failed.' }
    Remove-Item $bundleDir -Recurse -Force
}

if ($SignWithSelfSigned) {
    Write-Host 'Signing with a self-signed certificate (sideload testing only)...' -ForegroundColor Yellow

    # The certificate subject must match the package Publisher exactly.
    $certificate = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $Publisher } |
        Select-Object -First 1

    if (-not $certificate) {
        $certificate = New-SelfSignedCertificate `
            -Type Custom -Subject $Publisher `
            -KeyUsage DigitalSignature -FriendlyName 'Keyboard Language Fix (test)' `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    }

    $signtool = Find-WindowsSdkTool 'signtool.exe'
    & $signtool sign /fd SHA256 /sha1 $certificate.Thumbprint $artifact
    if ($LASTEXITCODE -ne 0) { throw 'signtool failed.' }

    $cerPath = Join-Path $outputDir 'KeyboardLanguageFix-test.cer'
    Export-Certificate -Cert $certificate -FilePath $cerPath | Out-Null

    Write-Host ''
    Write-Host "To trust this test build, run once as administrator:" -ForegroundColor Yellow
    Write-Host "  Import-Certificate -FilePath `"$cerPath`" -CertStoreLocation Cert:\LocalMachine\TrustedPeople"
}

Write-Host ''
Write-Host "Built $artifact" -ForegroundColor Green
if (-not $SignWithSelfSigned) {
    Write-Host 'Unsigned, as a Store submission requires — Partner Center signs it on upload.'
}
