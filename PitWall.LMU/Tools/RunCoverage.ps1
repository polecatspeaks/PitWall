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

$runsettings = Join-Path $rootPath "coverage.runsettings"

Write-Host "Building solution..."
Push-Location $rootPath
try {
    & dotnet build --no-restore -v quiet
}
finally {
    Pop-Location
}

Write-Host "Running tests with coverage (per-project to ensure proper instrumentation)..."
$testProjects = @(
    "PitWall.Tests\PitWall.Tests.csproj",
    "PitWall.UI.Tests\PitWall.UI.Tests.csproj",
    "PitWall.Telemetry.Live.Tests\PitWall.Telemetry.Live.Tests.csproj"
)

$anyFailed = $false
foreach ($project in $testProjects) {
    $projectPath = Join-Path $rootPath $project
    if (-not (Test-Path $projectPath)) {
        Write-Warning "Test project not found: $project"
        continue
    }
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
    Write-Host "  Running $projectName..."
    Push-Location $rootPath
    try {
        & dotnet test $project --no-build --collect:"XPlat Code Coverage" --results-directory "$resultsPath" --settings "$runsettings" --blame-hang-timeout 60s
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "  $projectName reported failures."
            $anyFailed = $true
        }
    }
    finally {
        Pop-Location
    }
}

if ($anyFailed) {
    Write-Warning "Some test projects reported failures. Coverage will still be generated from available results."
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
