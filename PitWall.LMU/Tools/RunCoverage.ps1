param(
    [string]$ResultsDir = "",
    [string]$ReportDir = ""
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot ".."))
$rootPath = $root.Path
$resultsPath = if ([string]::IsNullOrWhiteSpace($ResultsDir)) {
    Join-Path $rootPath "TestResults\Coverage"
} else {
    $ResultsDir
}

$reportPath = if ([string]::IsNullOrWhiteSpace($ReportDir)) {
    Join-Path $rootPath "TestResults\CoverageReport"
} else {
    $ReportDir
}

if (Test-Path $resultsPath) {
    Remove-Item -Path $resultsPath -Recurse -Force
}

if (Test-Path $reportPath) {
    Remove-Item -Path $reportPath -Recurse -Force
}

New-Item -ItemType Directory -Path $resultsPath | Out-Null

Write-Host "Running tests with coverage..."
Push-Location $rootPath
try {
    & dotnet test --collect:"XPlat Code Coverage" --results-directory "$resultsPath"
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "dotnet test reported failures. Coverage will still be generated from available results."
    }
}
finally {
    Pop-Location
}

$coverageFiles = Get-ChildItem -Path $resultsPath -Recurse -Filter "coverage.cobertura.xml" -File
if (-not $coverageFiles -or $coverageFiles.Count -eq 0) {
    Write-Warning "No coverage.cobertura.xml files found under $resultsPath"
    exit 1
}

Write-Host "Restoring local dotnet tools..."
& dotnet tool restore --tool-manifest (Join-Path $rootPath ".config\dotnet-tools.json")

$reportPaths = ($coverageFiles | ForEach-Object { $_.FullName }) -join ";"
$reportArgs = @(
    "-reports:$reportPaths",
    "-targetdir:$reportPath",
    "-reporttypes:HtmlInline;Cobertura"
)

Write-Host "Generating coverage report..."
Push-Location $rootPath
try {
    & dotnet tool run reportgenerator -- @reportArgs
    if ($LASTEXITCODE -ne 0) {
        throw "ReportGenerator failed."
    }
}
finally {
    Pop-Location
}

Write-Host "Coverage report written to: $reportPath\index.html"
