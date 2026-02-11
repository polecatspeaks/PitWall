param(
    [string]$OutputDir = "$(Join-Path $PSScriptRoot "..\PitWall.UI\Assets\Cars\lmu")",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$indexUrl = "https://lemansultimate.com/cars/"

function Convert-ToPlainText([string]$html) {
    $text = [regex]::Replace($html, "<script[\s\S]*?</script>", " ", "IgnoreCase")
    $text = [regex]::Replace($text, "<style[\s\S]*?</style>", " ", "IgnoreCase")
    $text = $text -replace "<br\s*/?>", "`n"
    $text = $text -replace "</p>", "`n"
    $text = $text -replace "</li>", "`n"
    $text = $text -replace "</h\d>", "`n"
    $text = [regex]::Replace($text, "<[^>]+>", " ")
    $text = [System.Net.WebUtility]::HtmlDecode($text)
    $text = $text -replace "\r", ""
    $text = [regex]::Replace($text, "[\t ]+", " ")
    $text = [regex]::Replace($text, "\n\s+", "`n")
    $text = [regex]::Replace($text, "\n{2,}", "`n")
    $text = $text -replace "\s*:\s*", ": "
    $text = $text.Trim()
    return $text
}

function Remove-Diacritics([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return "" }
    $normalized = $value.Normalize([Text.NormalizationForm]::FormD)
    $builder = New-Object System.Text.StringBuilder
    foreach ($ch in $normalized.ToCharArray()) {
        if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch) -ne [Globalization.UnicodeCategory]::NonSpacingMark) {
            [void]$builder.Append($ch)
        }
    }
    return $builder.ToString().Normalize([Text.NormalizationForm]::FormC)
}

function Get-Slug([string]$value) {
    $clean = Remove-Diacritics $value
    $clean = $clean.ToLowerInvariant()
    $clean = [regex]::Replace($clean, "[^a-z0-9]+", "-")
    return $clean.Trim('-')
}

function Get-FirstTagValue([string]$html, [string]$tagName) {
    $pattern = "<${tagName}[^>]*>(.*?)</${tagName}>"
    $match = [regex]::Match($html, $pattern, "IgnoreCase")
    if ($match.Success) {
        return (Convert-ToPlainText $match.Groups[1].Value)
    }
    return ""
}

function Get-SpecValue([string]$text, [string]$label) {
    $pattern = "(?m)^\s*${label}:\s*([^\n]+)"
    $match = [regex]::Match($text, $pattern)
    if ($match.Success) {
        return $match.Groups[1].Value.Trim()
    }
    return ""
}

function Parse-Number([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $null }
    $digits = [regex]::Match($value, "[0-9,]+(\.[0-9]+)?").Value
    if ([string]::IsNullOrWhiteSpace($digits)) { return $null }
    $digits = $digits -replace ",", ""
    return [int]([double]$digits)
}

$index = Invoke-WebRequest -Uri $indexUrl
$links = [regex]::Matches($index.Content, 'https://lemansultimate.com/cars/[^\s"<>]+/', "IgnoreCase") |
    ForEach-Object { $_.Value } |
    Where-Object { $_ -ne $indexUrl } |
    Sort-Object -Unique

if ($links.Count -eq 0) {
    throw "No car links found on $indexUrl"
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

if (-not $DryRun) {
    Get-ChildItem -Path $OutputDir -Filter "*.json" -File -ErrorAction SilentlyContinue | Remove-Item -Force
}

$manifest = New-Object System.Collections.Generic.List[string]

foreach ($link in $links) {
    Write-Host "Fetching $link"
    $page = Invoke-WebRequest -Uri $link
    $html = $page.Content
    $plain = Convert-ToPlainText $html

    $name = Get-FirstTagValue $html "h1"
    if ([string]::IsNullOrWhiteSpace($name)) {
        $name = Get-FirstTagValue $html "h2"
    }
    $name = Remove-Diacritics $name

    $category = Remove-Diacritics (Get-SpecValue $plain "Category")
    $engine = Remove-Diacritics (Get-SpecValue $plain "Engine")
    $transmission = Remove-Diacritics (Get-SpecValue $plain "Transmission")
    $power = Remove-Diacritics (Get-SpecValue $plain "Power")
    $weight = Remove-Diacritics (Get-SpecValue $plain "Weight")
    $length = Remove-Diacritics (Get-SpecValue $plain "Length")
    $width = Remove-Diacritics (Get-SpecValue $plain "Width")
    $height = Remove-Diacritics (Get-SpecValue $plain "Height")

    if ([string]::IsNullOrWhiteSpace($name)) {
        Write-Warning "Skipping $link (missing name)"
        continue
    }

    $slug = Get-Slug $name
    $fileName = "${slug}.json"

    $data = [ordered]@{
        name = $name
        slug = $slug
        category = $category
        engine = $engine
        transmission = $transmission
        power = $power
        powerBhp = Parse-Number $power
        weightKg = Parse-Number $weight
        lengthMm = Parse-Number $length
        widthMm = Parse-Number $width
        heightMm = Parse-Number $height
        sourceUrl = $link
    }

    if (-not $DryRun) {
        $json = ($data | ConvertTo-Json -Depth 4)
        Set-Content -Path (Join-Path $OutputDir $fileName) -Value $json -Encoding utf8
        $manifest.Add($fileName)
    }
}

if (-not $DryRun) {
    $manifestJson = ($manifest | ConvertTo-Json -Depth 2)
    Set-Content -Path (Join-Path $OutputDir "manifest.json") -Value $manifestJson -Encoding utf8
    Write-Host "Wrote $($manifest.Count) car specs to $OutputDir"
}
