param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot,
    [string]$ApiBase = "http://localhost:5236",
    [string]$AgentBase = "http://localhost:5139"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path $ProjectRoot
$solutionRoot = $root.Path
$telemetryDb = Join-Path $solutionRoot "lmu_telemetry.db"
$apiProject = Join-Path $solutionRoot "PitWall.Api\PitWall.Api.csproj"
$agentProject = Join-Path $solutionRoot "PitWall.Agent\PitWall.Agent.csproj"
$uiProject = Join-Path $solutionRoot "PitWall.UI\PitWall.UI.csproj"

$names = @("PitWall.Api", "PitWall.Agent", "PitWall.UI")
$procs = Get-Process -ErrorAction SilentlyContinue | Where-Object { $names -contains $_.ProcessName }
if ($procs) {
    $procs | Stop-Process -Force
}

Push-Location $solutionRoot
try {
    dotnet build | Out-Host
}
finally {
    Pop-Location
}

$apiArgs = @(
    "-NoExit",
    "-Command",
    "`$env:LMU_TELEMETRY_DB = `"$telemetryDb`"; `$env:ASPNETCORE_URLS = `"$ApiBase`"; Set-Location `"$solutionRoot`"; dotnet run --project `"$apiProject`""
)
Start-Process powershell -ArgumentList $apiArgs

$agentArgs = @(
    "-NoExit",
    "-Command",
    "`$env:ASPNETCORE_URLS = `"$AgentBase`"; Set-Location `"$solutionRoot`"; dotnet run --project `"$agentProject`""
)
Start-Process powershell -ArgumentList $agentArgs

$uiArgs = @(
    "-NoExit",
    "-Command",
    "`$env:PITWALL_API_BASE = `"$ApiBase`"; `$env:PITWALL_AGENT_BASE = `"$AgentBase`"; Set-Location `"$solutionRoot`"; dotnet run --project `"$uiProject`""
)
Start-Process powershell -ArgumentList $uiArgs
