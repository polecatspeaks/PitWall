@echo off
REM ATLAS UI Transformation - Build and Test Script
REM Run this script from the PitWall.LMU directory

echo ========================================
echo ATLAS UI Transformation - Build Script
echo ========================================
echo.

cd /d "%~dp0"

echo [1/5] Restoring NuGet packages...
dotnet restore
if errorlevel 1 (
    echo ERROR: Package restore failed!
    pause
    exit /b 1
)
echo.

echo [2/5] Building PitWall.UI project...
dotnet build PitWall.UI\PitWall.UI.csproj --no-restore
if errorlevel 1 (
    echo ERROR: UI build failed!
    pause
    exit /b 1
)
echo.

echo [3/5] Building all projects...
dotnet build --no-restore
if errorlevel 1 (
    echo ERROR: Full build failed!
    pause
    exit /b 1
)
echo.

echo [4/5] Running unit tests...
dotnet test --no-build --verbosity normal
if errorlevel 1 (
    echo WARNING: Some tests failed!
)
echo.

echo [5/5] Build complete!
echo.
echo ========================================
echo Next Steps:
echo ========================================
echo 1. Review IMPLEMENTATION_SUMMARY.md
echo 2. Run application: dotnet run --project PitWall.UI\PitWall.UI.csproj
echo 3. Press F12 in app for Avalonia DevTools
echo 4. Test all 5 tabs (Dashboard, Telemetry, Strategy, AI, Settings)
echo ========================================
echo.

pause
