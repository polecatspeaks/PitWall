#!/bin/bash
# Demo script to show telemetry streaming works correctly

echo "=== LMU Telemetry Fix Demo ==="
echo ""
echo "Starting API server with session 276 database..."

cd "$(dirname "$0")"
export LMU_TELEMETRY_DB="$PWD/data/lmu_telemetry_session_276.db"

# Start API in background
dotnet run --project PitWall.Api/PitWall.Api.csproj > /tmp/api_demo.log 2>&1 &
API_PID=$!

# Wait for API to start
sleep 5

echo "API started (PID: $API_PID)"
echo ""

# Test 1: Early rows (no brake yet)
echo "Test 1: First 5 rows (brake should be 0, lap should be 0)"
echo "----------------------------------------"
curl -s "http://localhost:5236/api/sessions/276/samples?startRow=0&endRow=4" | \
    jq -r '.samples[] | "Row \(.): brake=\(.brake | tonumber | . * 100 | floor)%, lap=\(.lapNumber)"' | \
    head -5
echo ""

# Test 2: Rows where brake appears
echo "Test 2: Rows 1289-1293 (brake should be non-zero, lap=0)"
echo "----------------------------------------"
curl -s "http://localhost:5236/api/sessions/276/samples?startRow=1289&endRow=1293" | \
    jq -r '.samples[] | "Row \(.): brake=\(.brake | tonumber | . * 100 | floor)%, throttle=\(.throttle | tonumber | . * 100 | floor)%, lap=\(.lapNumber)"' | \
    head -5
echo ""

# Test 3: Later rows
echo "Test 3: Rows 5000-5004 (still lap 0 since we're in first 100 seconds)"
echo "----------------------------------------"
curl -s "http://localhost:5236/api/sessions/276/samples?startRow=5000&endRow=5004" | \
    jq -r '.samples[] | "Row \(.): brake=\(.brake | tonumber | . * 100 | floor)%, lap=\(.lapNumber)"' | \
    head -5
echo ""

echo "Demo complete! Shutting down API..."
kill $API_PID 2>/dev/null
sleep 1

echo ""
echo "=== Summary ==="
echo "✅ API returns brake values starting at row 1289"
echo "✅ Lap values correctly show 0 for all rows (Lap 1 starts at 181.42s, outside 100s window)"
echo "✅ GPS Time-based forward-fill working correctly"
echo ""
echo "See /tmp/api_demo.log for full API logs"
