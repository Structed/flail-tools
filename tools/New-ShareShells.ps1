#Requires -Version 7.0

<#
.SYNOPSIS
    Writes a per-route HTML shell for every tool, so each one unfurls as itself.

.DESCRIPTION
    Sharing metadata has to be in the served HTML, because a crawler does not run Blazor. The app
    is one page, so every route would otherwise share the landing card.

    The shells are committed beside index.html rather than generated during a deployment, because
    the site is built by more than one host and only one of them runs this repository's workflow.
    A shell that exists in wwwroot is published by whoever runs dotnet publish, wherever that is.

    Copying index.html is safe because the two parts that look host-specific are resolved during
    publish, not written here: the boot script keeps its #[.{fingerprint}] placeholder, which the
    Blazor SDK rewrites in every HTML file it publishes, and <base href> is absolute, so a shell
    one directory down loads exactly the same assets. What is left is the risk of the copy going
    stale, and ShareCardTests fails the build if a shell stops matching index.html.

    Only the head is touched: title, description, and the og: and twitter: pairs. Every tag the
    manifest names must already exist in index.html, so a tag that is renamed or dropped fails here
    rather than leaving a shell quietly serving the wrong card. og:url and canonical stay absent on
    purpose; the shared address carries the seed.

    Run this after editing index.html or the manifest, and commit what it writes.

.PARAMETER Wwwroot
    The app's static root, holding index.html and the card images. Defaults to the one in this
    repository.

.PARAMETER Manifest
    The share-card manifest. Defaults to the one beside this script.

.EXAMPLE
    pwsh ./tools/New-ShareShells.ps1
#>

[CmdletBinding()]
param(
    [string]$Wwwroot,

    [string]$Manifest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-HtmlAttribute {
    param([string]$Value)

    return $Value.
        Replace('&', '&amp;').
        Replace('<', '&lt;').
        Replace('>', '&gt;').
        Replace('"', '&quot;')
}

function Get-MetaContent {
    param([string]$Html, [string]$Attribute, [string]$Name)

    $pattern = "<meta\b(?=[^>]*\b$Attribute=""$([regex]::Escape($Name))"")[^>]*>"
    $tags = [regex]::Matches($Html, $pattern)

    if ($tags.Count -ne 1) {
        throw "index.html has $($tags.Count) <meta $Attribute=""$Name""> tags; expected exactly one."
    }

    $content = [regex]::Match($tags[0].Value, 'content="([^"]*)"')

    if (-not $content.Success) {
        throw "<meta $Attribute=""$Name""> in index.html has no content attribute."
    }

    return $content.Groups[1].Value
}

function Set-MetaContent {
    param([string]$Html, [string]$Attribute, [string]$Name, [string]$Value)

    $pattern = "<meta\b(?=[^>]*\b$Attribute=""$([regex]::Escape($Name))"")[^>]*>"
    $tags = [regex]::Matches($Html, $pattern)

    if ($tags.Count -ne 1) {
        throw "index.html has $($tags.Count) <meta $Attribute=""$Name""> tags; expected exactly one."
    }

    $tag = $tags[0]
    $replaced = [regex]::Replace(
        $tag.Value,
        'content="[^"]*"',
        'content="' + (ConvertTo-HtmlAttribute $Value).Replace('$', '$$') + '"',
        [Text.RegularExpressions.RegexOptions]::None)

    if ($replaced -eq $tag.Value -and $tags[0].Value -notmatch 'content="') {
        throw "<meta $Attribute=""$Name""> in index.html has no content attribute."
    }

    return $Html.Remove($tag.Index, $tag.Length).Insert($tag.Index, $replaced)
}

function Set-DocumentTitle {
    param([string]$Html, [string]$Value)

    $titles = [regex]::Matches($Html, '<title>.*?</title>', 'Singleline')

    if ($titles.Count -ne 1) {
        throw "index.html has $($titles.Count) <title> elements; expected exactly one."
    }

    $escaped = $Value.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;')

    return $Html.Remove($titles[0].Index, $titles[0].Length).
        Insert($titles[0].Index, "<title>$escaped</title>")
}

if (-not $Manifest) {
    $Manifest = Join-Path $PSScriptRoot 'share-cards.json'
}

if (-not $Wwwroot) {
    $Wwwroot = Join-Path $PSScriptRoot '..' 'src' 'FlailTools.Web' 'wwwroot'
}

foreach ($path in $Wwwroot, $Manifest) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "'$path' does not exist."
    }
}

$Wwwroot = (Resolve-Path -LiteralPath $Wwwroot).Path
$cards = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
$indexPath = Join-Path $Wwwroot 'index.html'

if (-not (Test-Path -LiteralPath $indexPath)) {
    throw "No index.html in '$Wwwroot'."
}

$index = Get-Content -LiteralPath $indexPath -Raw
$baseUrl = $cards.baseUrl

$written = foreach ($card in $cards.cards) {
    if (-not (Test-Path -LiteralPath (Join-Path $Wwwroot $card.image))) {
        throw "'$($card.route)' names '$($card.image)', which is not in wwwroot."
    }

    $imageUrl = "$baseUrl$($card.image)"

    if (-not $card.shell) {
        # The landing card is written by hand into index.html. Check the two still say the same
        # thing, so a manifest edit that never reached index.html fails here.
        $mismatches = @(
            @{ Attribute = 'property'; Name = 'og:title';      Expected = $card.title }
            @{ Attribute = 'property'; Name = 'og:description'; Expected = $card.description }
            @{ Attribute = 'name';     Name = 'description';    Expected = $card.description }
            @{ Attribute = 'property'; Name = 'og:image';       Expected = $imageUrl }
            @{ Attribute = 'property'; Name = 'og:image:alt';   Expected = $card.imageAlt }
        ) | Where-Object {
            (Get-MetaContent -Html $index -Attribute $_.Attribute -Name $_.Name) -ne $_.Expected
        }

        if ($mismatches) {
            $names = ($mismatches | ForEach-Object { $_.Name }) -join ', '
            throw "index.html and the '$($card.route)' entry in the manifest disagree on: $names."
        }

        continue
    }

    $shell = $index
    $shell = Set-DocumentTitle -Html $shell -Value $card.documentTitle
    $shell = Set-MetaContent -Html $shell -Attribute 'name' -Name 'description' -Value $card.description
    $shell = Set-MetaContent -Html $shell -Attribute 'property' -Name 'og:title' -Value $card.title
    $shell = Set-MetaContent -Html $shell -Attribute 'property' -Name 'og:description' -Value $card.description
    $shell = Set-MetaContent -Html $shell -Attribute 'property' -Name 'og:image' -Value $imageUrl
    $shell = Set-MetaContent -Html $shell -Attribute 'property' -Name 'og:image:alt' -Value $card.imageAlt
    $shell = Set-MetaContent -Html $shell -Attribute 'name' -Name 'twitter:image' -Value $imageUrl
    $shell = Set-MetaContent -Html $shell -Attribute 'name' -Name 'twitter:image:alt' -Value $card.imageAlt

    $directory = Join-Path $Wwwroot $card.shell
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $destination = Join-Path $directory 'index.html'
    [IO.File]::WriteAllText($destination, $shell, [Text.UTF8Encoding]::new($false))

    [pscustomobject]@{
        Route = $card.route
        Shell = "$($card.shell)/index.html"
        Card  = $card.image
    }
}

if (-not $written) {
    throw "The manifest declares no shells; every route would share the landing card."
}

$written | Format-Table -AutoSize
