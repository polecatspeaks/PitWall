param(
    [string]$SourceDir = "C:\\Users\\ohzee\\git\\lmu",
    [string]$DbPath = "C:\\Users\\ohzee\\.claude-worktrees\\PitWall\\vibrant-meninsky\\PitWall.LMU\\lmu_telemetry.db"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SourceDir)) {
    throw "Source directory not found: $SourceDir"
}

$tempSql = Join-Path $env:TEMP "pitwall_import.sql"
$tempEncoding = "utf8"

if (Test-Path $DbPath) {
    Remove-Item $DbPath -Force
}

$initSql = @"
CREATE TABLE IF NOT EXISTS sessions (
    session_id INTEGER,
    source_file VARCHAR,
    recording_time VARCHAR,
    track_name VARCHAR,
    track_layout VARCHAR,
    car_name VARCHAR,
    car_class VARCHAR,
    session_type VARCHAR,
    weather_conditions VARCHAR,
    driver_name VARCHAR
);
CREATE TABLE IF NOT EXISTS session_metadata (
    session_id INTEGER,
    "key" VARCHAR,
    "value" VARCHAR
);
"@

Set-Content -Path $tempSql -Value $initSql -Encoding $tempEncoding
& duckdb $DbPath -f $tempSql | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Failed to initialize DuckDB at $DbPath"
}

function Escape-Sql([string]$value) {
    if ($null -eq $value) {
        return $null
    }

    return $value -replace "'", "''"
}

$files = Get-ChildItem $SourceDir -Filter *.duckdb -File | Sort-Object Name
$sessionId = 0

foreach ($file in $files) {
    $sessionId++

    Write-Host "Processing $sessionId/$($files.Count): $($file.Name)..."

    $tableRows = & duckdb -csv $file.FullName "SELECT table_name FROM information_schema.tables WHERE table_schema='main' ORDER BY table_name;" | ConvertFrom-Csv
    $tables = $tableRows.table_name | Where-Object { $_ -ne "metadata" }

    $metaRows = & duckdb -csv $file.FullName "SELECT key, value FROM metadata;" | ConvertFrom-Csv
    $meta = @{}
    foreach ($row in $metaRows) {
        $meta[$row.key] = $row.value
    }

    $recordingTime = Escape-Sql $meta["RecordingTime"]
    $trackName = Escape-Sql $meta["TrackName"]
    $trackLayout = Escape-Sql $meta["TrackLayout"]
    $carName = Escape-Sql $meta["CarName"]
    $carClass = Escape-Sql $meta["CarClass"]
    $sessionType = Escape-Sql $meta["SessionType"]
    $weather = Escape-Sql $meta["WeatherConditions"]
    $driverName = Escape-Sql $meta["DriverName"]
    $sourceFile = Escape-Sql $file.Name

    $sqlLines = New-Object System.Collections.Generic.List[string]
    $sqlLines.Add("ATTACH '$($file.FullName)' AS src;")

    foreach ($table in $tables) {
        $tableName = $table -replace '"', '""'
        
        # Create table with session_id column included (empty table with correct schema)
        $sqlLines.Add(("CREATE TABLE IF NOT EXISTS ""{0}"" AS SELECT *, CAST(NULL AS INTEGER) AS session_id FROM src.""{0}"" WHERE 1=0;" -f $tableName))
        
        # Insert data with session_id value
        $sqlLines.Add(("INSERT INTO ""{0}"" SELECT *, {1} AS session_id FROM src.""{0}"";" -f $tableName, $sessionId))
    }

    $sqlLines.Add("INSERT INTO sessions (session_id, source_file, recording_time, track_name, track_layout, car_name, car_class, session_type, weather_conditions, driver_name) VALUES ($sessionId, '$sourceFile', '$recordingTime', '$trackName', '$trackLayout', '$carName', '$carClass', '$sessionType', '$weather', '$driverName');")

    foreach ($row in $metaRows) {
        $key = Escape-Sql $row.key
        $val = Escape-Sql $row.value
        $sqlLines.Add(("INSERT INTO session_metadata (session_id, ""key"", ""value"") VALUES ({0}, '{1}', '{2}');" -f $sessionId, $key, $val))
    }

    $sqlLines.Add("DETACH src;")

    Set-Content -Path $tempSql -Value $sqlLines -Encoding $tempEncoding
    & duckdb -bail $DbPath -f $tempSql | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "DuckDB import failed for $($file.FullName)"
    }

    Write-Host "  ✓ Imported session $sessionId with session_id column in all tables"
}

Write-Host ""
Write-Host "✓ Database rebuild complete!"
& duckdb $DbPath 'SELECT COUNT(*) AS sessions FROM sessions;'
Write-Host ""
Write-Host "Sample GPS data check:"
& duckdb $DbPath 'SELECT session_id, COUNT(*) AS rows FROM "GPS Latitude" GROUP BY session_id ORDER BY session_id LIMIT 5;'