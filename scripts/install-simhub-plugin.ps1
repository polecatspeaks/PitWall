param(
    [string]$SimHubPath = "C:\\Program Files (x86)\\SimHub",
    [ValidateSet("Debug", "Release")][string]$Configuration = "Debug",
    [switch]$NoBuild,
    [switch]$IncludePdb
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot "PitWall.sln"
$buildOutput = Join-Path $repoRoot "bin\$Configuration\net48"

Write-Host "SimHub plugin install" -ForegroundColor Cyan
Write-Host "Target folder: $SimHubPath"
Write-Host "Configuration: $Configuration"

$simHubRunning = Get-Process -Name "SimHub" -ErrorAction SilentlyContinue
if ($simHubRunning) {
    Write-Warning "SimHub is currently running. Close SimHub before installing to ensure DLLs reload." 
}

if (-not $NoBuild) {
    Write-Host "Building PitWall..."
    $build = Start-Process dotnet -ArgumentList @("build", "`"$solutionPath`"", "-c", $Configuration, "-v", "minimal") -NoNewWindow -PassThru -Wait
    if ($build.ExitCode -ne 0) {
        Write-Error "Build failed (exit $($build.ExitCode)). Aborting copy." 
        exit $build.ExitCode
    }
}

if (-not (Test-Path $buildOutput)) {
    Write-Error "Build output not found at $buildOutput"
    exit 1
}

$files = @("SimHub.Plugins.PitWall.dll", "System.Data.SQLite.dll")
if ($IncludePdb) { $files += "PitWall.pdb" }

Write-Host "Copying plugin files to $SimHubPath..."
foreach ($file in $files) {
    $src = Join-Path $buildOutput $file
    if (Test-Path $src) {
        Copy-Item $src -Destination $SimHubPath -Force
        Write-Host "  + $file"
    }
    else {
        Write-Warning "  - Missing $file in build output; skipping"
    }
}

# Copy native SQLite interop folders (x86/x64) if present
foreach ($arch in @("x86", "x64")) {
    $srcDir = Join-Path $buildOutput $arch
    if (Test-Path $srcDir) {
        $destDir = Join-Path $SimHubPath $arch
        Copy-Item $srcDir -Destination $destDir -Recurse -Force
        Write-Host "  + $arch/ native interop"
    }
}

Write-Host ""
Write-Host "Installation complete!" -ForegroundColor Green
Write-Host "Restart SimHub and check 'Additional plugins' for 'Pit Wall Race Engineer'." -ForegroundColor Green
