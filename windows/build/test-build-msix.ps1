<#
.SYNOPSIS
    Checks the parts of build-msix.ps1 that can be verified without Windows.

.DESCRIPTION
    Runs on any platform with PowerShell 7. It parses the build script and
    exercises the manifest rewriter, which is the piece most likely to break
    silently — a bad substitution there produces a package the Store rejects
    only after upload.

    Run:  pwsh -File windows/build/test-build-msix.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$buildScript = Join-Path $repoRoot 'windows/build/build-msix.ps1'
$sourceManifest = Join-Path $repoRoot 'windows/packaging/AppxManifest.xml'

$failures = @()
function Should([bool]$condition, [string]$description) {
    if ($condition) {
        Write-Host "  PASS  $description" -ForegroundColor Green
    }
    else {
        Write-Host "  FAIL  $description" -ForegroundColor Red
        $script:failures += $description
    }
}

Write-Host 'build-msix.ps1'
$parseErrors = $null
$tokens = $null
[System.Management.Automation.Language.Parser]::ParseFile($buildScript, [ref]$tokens, [ref]$parseErrors) | Out-Null
Should (-not $parseErrors) 'the build script parses without errors'

# Load just the manifest rewriter, so this test does not try to publish anything.
$text = Get-Content $buildScript -Raw
$start = $text.IndexOf('function Write-AppxManifest')
$end = $text.IndexOf('function New-PackageLayout')
Should ($start -ge 0 -and $end -gt $start) 'Write-AppxManifest is present'
Invoke-Expression $text.Substring($start, $end - $start)

Write-Host 'AppxManifest rewriting'
$generated = Join-Path ([System.IO.Path]::GetTempPath()) 'AppxManifest.generated.xml'
Write-AppxManifest -SourcePath $sourceManifest -DestinationPath $generated `
    -PackageName '12345Contoso.KeyboardLanguageFix' `
    -PackagePublisher 'CN=ABCD1234-1234-1234-1234-1234567890AB' `
    -PackageVersion '1.2.3.0' `
    -PackageArchitecture 'arm64' `
    -DisplayName 'Contoso Ltd'

[xml]$result = Get-Content $generated -Raw
$ns = New-Object System.Xml.XmlNamespaceManager($result.NameTable)
$ns.AddNamespace('m', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$ns.AddNamespace('rescap', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities')
$ns.AddNamespace('uap', 'http://schemas.microsoft.com/appx/manifest/uap/windows10')
$ns.AddNamespace('desktop', 'http://schemas.microsoft.com/appx/manifest/desktop/windows10')

$identity = $result.SelectSingleNode('/m:Package/m:Identity', $ns)
Should ($identity.Name -eq '12345Contoso.KeyboardLanguageFix') 'Identity/@Name is replaced'
Should ($identity.Publisher -eq 'CN=ABCD1234-1234-1234-1234-1234567890AB') 'Identity/@Publisher is replaced'
Should ($identity.Version -eq '1.2.3.0') 'Identity/@Version is replaced'
Should ($identity.ProcessorArchitecture -eq 'arm64') 'Identity/@ProcessorArchitecture is replaced'

# The regression this test exists for: several elements carry a "Name"
# attribute, and only Identity's may change.
$family = $result.SelectSingleNode('/m:Package/m:Dependencies/m:TargetDeviceFamily', $ns)
Should ($family.Name -eq 'Windows.Desktop') 'TargetDeviceFamily/@Name is left alone'

$capability = $result.SelectSingleNode('/m:Package/m:Capabilities/rescap:Capability', $ns)
Should ($capability -and $capability.Name -eq 'runFullTrust') 'the runFullTrust capability survives'

$publisherDisplay = $result.SelectSingleNode('/m:Package/m:Properties/m:PublisherDisplayName', $ns)
Should ($publisherDisplay.InnerText -eq 'Contoso Ltd') 'PublisherDisplayName is replaced'

Write-Host 'Store requirements'
$application = $result.SelectSingleNode('/m:Package/m:Applications/m:Application', $ns)
Should ($application.EntryPoint -eq 'Windows.FullTrustApplication') 'the app is declared as a full-trust desktop app'
Should ($application.Executable -eq 'KeyboardLanguageFix.exe') 'the executable name matches the build output'

$visual = $application.SelectSingleNode('uap:VisualElements', $ns)
$startup = $application.SelectSingleNode('m:Extensions/desktop:Extension', $ns)
Should ($startup -and $startup.Category -eq 'windows.startupTask') 'the startup task extension is declared'

Write-Host 'Store assets'
$images = Join-Path $repoRoot 'windows/packaging/Images'
foreach ($logo in @($visual.Square150x150Logo, $visual.Square44x44Logo,
                    $result.Package.Properties.Logo)) {
    $file = Join-Path $repoRoot (Join-Path 'windows/packaging' ($logo -replace '\\', '/'))
    Should (Test-Path $file) "$logo exists"
}
Should ((Get-ChildItem $images -Filter '*.png').Count -ge 20) 'the scale and targetsize variants are present'

# The application manifest must not ask for elevation: the Store rejects that.
$appManifest = Get-Content (Join-Path $repoRoot 'windows/src/KeyboardLanguageFix.App/app.manifest') -Raw
Should ($appManifest -match 'level="asInvoker"') 'app.manifest requests asInvoker'
Should ($appManifest -notmatch 'requireAdministrator') 'app.manifest never asks for administrator'

Remove-Item $generated -Force -ErrorAction SilentlyContinue

Write-Host ''
if ($failures.Count -gt 0) {
    Write-Host "$($failures.Count) check(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host 'All packaging checks passed.' -ForegroundColor Green
