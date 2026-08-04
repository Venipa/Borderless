#Requires -Version 5.1
<#
.SYNOPSIS
  Packs a published Borderless folder into an MSIX (self-contained recommended).

.EXAMPLE
  .\installer\msix\Build-Msix.ps1 -SourceDir .\publish-bundled -Version 1.0.0.0 -OutFile .\Borderless-1.0.0.0-win-x64.msix
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $SourceDir,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $OutFile,

    [string] $ManifestTemplate = (Join-Path $PSScriptRoot "Package.appxmanifest"),

    [string] $IconSource = (Join-Path $PSScriptRoot "..\..\Borderless.App\Resources\Iconx512.png"),

    # Prefer explicit args; else env (CI secrets). Local unsigned defaults are inert placeholders.
    [string] $PackageIdentityName = "",

    [string] $Publisher = "",

    [string] $PublisherDisplayName = "",

    [string] $DisplayName = "",

    [string] $PfxPath = "",

    [string] $PfxPassword = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PackageIdentityName)) {
    $PackageIdentityName = $env:MSIX_PACKAGE_IDENTITY_NAME
}
if ([string]::IsNullOrWhiteSpace($Publisher)) {
    $Publisher = $env:MSIX_PUBLISHER
}
if ([string]::IsNullOrWhiteSpace($PublisherDisplayName)) {
    $PublisherDisplayName = $env:MSIX_PUBLISHER_DISPLAY_NAME
}
if ([string]::IsNullOrWhiteSpace($DisplayName)) {
    $DisplayName = $env:MSIX_DISPLAY_NAME
}

if ([string]::IsNullOrWhiteSpace($PackageIdentityName)) {
    $PackageIdentityName = "Borderless.Dev"
}
if ([string]::IsNullOrWhiteSpace($Publisher)) {
    $Publisher = "CN=BorderlessDev"
}
if ([string]::IsNullOrWhiteSpace($PublisherDisplayName)) {
    $PublisherDisplayName = "Borderless"
}
if ([string]::IsNullOrWhiteSpace($DisplayName)) {
    $DisplayName = "Borderless"
}

function Find-SdkTool([string] $Name) {
    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (-not (Test-Path $kitsRoot)) {
        throw "Windows SDK bin folder not found: $kitsRoot"
    }

    $match = Get-ChildItem -Path $kitsRoot -Recurse -Filter $Name -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -match '\\x64$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $match) {
        throw "Could not find $Name under $kitsRoot"
    }

    return $match.FullName
}

function Write-PngAsset {
    param(
        [string] $SourcePng,
        [string] $DestPng,
        [int] $Size
    )

    Add-Type -AssemblyName System.Drawing
    $src = [System.Drawing.Image]::FromFile((Resolve-Path $SourcePng).Path)
    try {
        $bmp = New-Object System.Drawing.Bitmap $Size, $Size
        try {
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            try {
                $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $g.Clear([System.Drawing.Color]::Transparent)
                $g.DrawImage($src, 0, 0, $Size, $Size)
            }
            finally {
                $g.Dispose()
            }

            $dir = Split-Path $DestPng -Parent
            if (-not (Test-Path $dir)) {
                New-Item -ItemType Directory -Path $dir | Out-Null
            }

            $bmp.Save($DestPng, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $bmp.Dispose()
        }
    }
    finally {
        $src.Dispose()
    }
}

$SourceDir = (Resolve-Path $SourceDir).Path
$ManifestTemplate = (Resolve-Path $ManifestTemplate).Path
$IconSource = (Resolve-Path $IconSource).Path
$OutFile = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutFile)

if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "Version must be MAJOR.MINOR.PATCH.BUILD. Got: $Version"
}

if (-not (Test-Path (Join-Path $SourceDir "Borderless.exe"))) {
    throw "Borderless.exe not found in SourceDir: $SourceDir"
}

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("borderless-msix-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $stage | Out-Null

try {
    Write-Host "Staging payload from $SourceDir"
    Copy-Item -Path (Join-Path $SourceDir "*") -Destination $stage -Recurse -Force

    $assets = Join-Path $stage "Assets"
    New-Item -ItemType Directory -Path $assets -Force | Out-Null
    Write-PngAsset -SourcePng $IconSource -DestPng (Join-Path $assets "StoreLogo.png") -Size 50
    Write-PngAsset -SourcePng $IconSource -DestPng (Join-Path $assets "Square44x44Logo.png") -Size 44
    Write-PngAsset -SourcePng $IconSource -DestPng (Join-Path $assets "Square150x150Logo.png") -Size 150

    [xml] $manifest = Get-Content -Path $ManifestTemplate -Raw
    $manifest.Package.Identity.Name = $PackageIdentityName
    $manifest.Package.Identity.Version = $Version
    $manifest.Package.Identity.Publisher = $Publisher
    $manifest.Package.Properties.DisplayName = $DisplayName
    $manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName

    $ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $ns.AddNamespace("default", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
    $ns.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10")
    $visual = $manifest.SelectSingleNode("//uap:VisualElements", $ns)
    if ($visual) {
        $visual.SetAttribute("DisplayName", $DisplayName)
    }

    $manifestPath = Join-Path $stage "AppxManifest.xml"
    $manifest.Save($manifestPath)
    Write-Host "AppxManifest Identity.Name=$PackageIdentityName Version=$Version DisplayName=$DisplayName PublisherDisplayName=$PublisherDisplayName"
    Write-Host "AppxManifest Identity.Publisher set (value redacted)."

    if (Test-Path $OutFile) {
        Remove-Item $OutFile -Force
    }

    $makeAppx = Find-SdkTool "makeappx.exe"
    Write-Host "makeappx: $makeAppx"
    & $makeAppx pack /o /d $stage /p $OutFile
    if ($LASTEXITCODE -ne 0) {
        throw "makeappx failed with exit code $LASTEXITCODE"
    }

    if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
        if (-not (Test-Path $PfxPath)) {
            throw "PFX not found: $PfxPath"
        }

        $signTool = Find-SdkTool "signtool.exe"
        Write-Host "signtool: $signTool"
        $signArgs = @(
            "sign", "/fd", "SHA256", "/a",
            "/f", (Resolve-Path $PfxPath).Path,
            $OutFile
        )
        if (-not [string]::IsNullOrWhiteSpace($PfxPassword)) {
            $signArgs = @("sign", "/fd", "SHA256", "/f", (Resolve-Path $PfxPath).Path, "/p", $PfxPassword, $OutFile)
        }

        & $signTool @signArgs
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed with exit code $LASTEXITCODE"
        }
    }
    else {
        Write-Host "No PFX provided — MSIX left unsigned (sideload / trust cert yourself)."
    }

    Get-Item $OutFile | Format-List FullName, Length
}
finally {
    if (Test-Path $stage) {
        Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
}
