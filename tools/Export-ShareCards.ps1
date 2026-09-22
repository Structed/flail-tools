#Requires -Version 7.0

<#
.SYNOPSIS
    Rasterises the sharing-card SVGs in wwwroot to PNGs of the same name.

.DESCRIPTION
    Each card is an SVG at its final pixel size, so the export is a screenshot of that SVG at
    exactly its declared width and height. Two details are not obvious:

    A card may pull in another SVG with <image href="...">, and headless Chromium refuses that
    subresource over file://. The referenced file is therefore inlined as a data URI into a
    temporary copy rather than the browser being handed --allow-file-access-from-files.

    A standalone SVG document is laid out by the browser rather than by the card, so it is wrapped
    in a zero-margin HTML page instead. Without that, the screenshot picks up the document margin
    and the PNG is a few pixels off in both directions.

    The written PNG's IHDR is read back and compared against the SVG, so an off-size card fails
    here rather than in somebody's feed.

.PARAMETER WebRoot
    The wwwroot holding the SVGs. Defaults to the one in this repository.

.PARAMETER Browser
    A Chromium-based browser to render with. Found automatically when not given.

.PARAMETER Filter
    Which SVGs to export. Defaults to the sharing cards.

.EXAMPLE
    pwsh ./tools/Export-ShareCards.ps1
#>

[CmdletBinding()]
param(
    [string]$WebRoot,
    [string]$Browser,
    [string]$Filter = 'open-graph*.svg'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-Browser {
    param([string]$Explicit)

    if ($Explicit) {
        if (-not (Test-Path -LiteralPath $Explicit)) {
            throw "No browser at '$Explicit'."
        }

        return (Resolve-Path -LiteralPath $Explicit).Path
    }

    foreach ($name in 'msedge', 'chrome', 'chromium', 'google-chrome', 'chromium-browser') {
        $command = Get-Command $name -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1

        if ($command) {
            return $command.Source
        }
    }

    $candidates = @(
        "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
        '/usr/bin/microsoft-edge'
        '/usr/bin/google-chrome'
        '/usr/bin/chromium'
        '/usr/bin/chromium-browser'
        '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
        '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge'
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw 'No Chromium-based browser found. Install Edge or Chrome, or pass -Browser.'
}

function ConvertTo-SelfContainedSvg {
    param([string]$Markup, [string]$Directory)

    return [regex]::Replace($Markup, 'href="([^"]+\.svg)"', {
        param($match)

        $referenced = Join-Path $Directory $match.Groups[1].Value

        if (-not (Test-Path -LiteralPath $referenced)) {
            throw "A card references '$($match.Groups[1].Value)', which is not beside it."
        }

        $encoded = [Convert]::ToBase64String([IO.File]::ReadAllBytes($referenced))
        return "href=`"data:image/svg+xml;base64,$encoded`""
    })
}

function Get-SvgSize {
    param([string]$Markup, [string]$Name)

    $width = [regex]::Match($Markup, '<svg\b[^>]*?\bwidth="(\d+)"')
    $height = [regex]::Match($Markup, '<svg\b[^>]*?\bheight="(\d+)"')

    if (-not $width.Success -or -not $height.Success) {
        throw "'$Name' does not declare a pixel width and height on its root element."
    }

    return [pscustomobject]@{
        Width  = [int]$width.Groups[1].Value
        Height = [int]$height.Groups[1].Value
    }
}

function Get-PngSize {
    param([string]$Path)

    $bytes = [IO.File]::ReadAllBytes($Path)

    if ($bytes.Length -lt 24) {
        throw "'$Path' is too short to be a PNG."
    }

    $signature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)

    for ($index = 0; $index -lt $signature.Length; $index++) {
        if ($bytes[$index] -ne $signature[$index]) {
            throw "'$Path' is not a PNG."
        }
    }

    # Cast before shifting: -shl keeps the left operand's type, so a byte shifted eight places is 0.
    function Read-BigEndian {
        param([byte[]]$Buffer, [int]$Offset)

        return ([int]$Buffer[$Offset] -shl 24) -bor
               ([int]$Buffer[$Offset + 1] -shl 16) -bor
               ([int]$Buffer[$Offset + 2] -shl 8) -bor
               [int]$Buffer[$Offset + 3]
    }

    return [pscustomobject]@{
        Width  = Read-BigEndian -Buffer $bytes -Offset 16
        Height = Read-BigEndian -Buffer $bytes -Offset 20
    }
}

function Invoke-Screenshot {
    param(
        [string]$Executable,
        [string]$PageUri,
        [string]$Destination,
        [int]$Width,
        [int]$Height,
        [string]$ProfileDirectory
    )

    $noise = Join-Path $ProfileDirectory 'browser.log'

    # Chromium removed old headless, but shipped builds still differ on which spelling they take.
    foreach ($mode in '--headless=new', '--headless') {
        if (Test-Path -LiteralPath $Destination) {
            Remove-Item -LiteralPath $Destination -Force
        }

        $arguments = @(
            $mode
            '--disable-gpu'
            '--hide-scrollbars'
            '--force-device-scale-factor=1'
            '--no-first-run'
            '--no-default-browser-check'
            '--disable-extensions'
            '--virtual-time-budget=5000'
            "--user-data-dir=$ProfileDirectory"
            "--window-size=$Width,$Height"
            "--screenshot=$Destination"
            $PageUri
        )

        # Start-Process -Wait, because the browser writes the file after its launcher has returned.
        Start-Process -FilePath $Executable -ArgumentList $arguments -Wait -NoNewWindow `
            -RedirectStandardOutput $noise -RedirectStandardError "$noise.err"

        if (Test-Path -LiteralPath $Destination) {
            return
        }
    }

    throw "'$Executable' produced no screenshot for $PageUri."
}

if (-not $WebRoot) {
    $WebRoot = Join-Path $PSScriptRoot '..' 'src' 'FlailTools.Web' 'wwwroot'
}

if (-not (Test-Path -LiteralPath $WebRoot)) {
    throw "No wwwroot at '$WebRoot'."
}

$WebRoot = (Resolve-Path -LiteralPath $WebRoot).Path
$executable = Find-Browser -Explicit $Browser
Write-Host "Rendering with $executable"

$cards = @(Get-ChildItem -LiteralPath $WebRoot -Filter $Filter -File | Sort-Object Name)

if ($cards.Count -eq 0) {
    throw "Nothing matching '$Filter' in '$WebRoot'."
}

$workspace = Join-Path ([IO.Path]::GetTempPath()) "flail-cards-$([Guid]::NewGuid().ToString('n'))"
$profileDirectory = Join-Path $workspace 'profile'
New-Item -ItemType Directory -Path $profileDirectory -Force | Out-Null

try {
    $exported = foreach ($card in $cards) {
        $markup = Get-Content -LiteralPath $card.FullName -Raw
        $size = Get-SvgSize -Markup $markup -Name $card.Name
        $page = Join-Path $workspace ([IO.Path]::ChangeExtension($card.Name, '.html'))
        $destination = [IO.Path]::ChangeExtension($card.FullName, '.png')
        $inlined = ConvertTo-SelfContainedSvg -Markup $markup -Directory $WebRoot

        $html = @"
<!DOCTYPE html>
<html><head><meta charset="utf-8"><style>
html, body { margin: 0; padding: 0; background: #fff; }
svg { display: block; }
</style></head><body>
$inlined
</body></html>
"@

        Set-Content -LiteralPath $page -Value $html -Encoding utf8NoBOM

        Invoke-Screenshot `
            -Executable $executable `
            -PageUri ([Uri]$page).AbsoluteUri `
            -Destination $destination `
            -Width $size.Width `
            -Height $size.Height `
            -ProfileDirectory $profileDirectory

        $written = Get-PngSize -Path $destination

        if ($written.Width -ne $size.Width -or $written.Height -ne $size.Height) {
            throw ("'$($card.Name)' declares $($size.Width)x$($size.Height) but its export is " +
                "$($written.Width)x$($written.Height).")
        }

        [pscustomobject]@{
            Card  = $card.Name
            Png   = [IO.Path]::GetFileName($destination)
            Size  = "$($written.Width)x$($written.Height)"
            Bytes = (Get-Item -LiteralPath $destination).Length
        }
    }

    $exported | Format-Table -AutoSize
}
finally {
    Remove-Item -LiteralPath $workspace -Recurse -Force -ErrorAction SilentlyContinue
}
