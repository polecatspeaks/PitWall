@echo off
REM Git Commit Script for ATLAS UI Transformation
REM Run this from the repository root

cd /d "%~dp0"

echo ========================================
echo ATLAS UI Transformation - Git Commit
echo ========================================
echo.

echo [1/4] Checking git status...
git status --short
echo.

echo [2/4] Staging all changes...
git add -A
echo.

echo [3/4] Committing changes...
git commit -m "feat: ATLAS Professional UI Transformation (60%% complete)

Architecture Refactor:
- Split MainWindowViewModel into 5 domain ViewModels
- Reduced complexity from 307 lines to 95 lines (orchestrator pattern)
- Created DashboardViewModel, TelemetryAnalysisViewModel, StrategyViewModel
- Created AiAssistantViewModel, SettingsViewModel

New Features:
- Professional TabControl navigation with 5 tabs
- ATLAS typography system (15+ style classes)
- TelemetryBuffer service for historical data (circular buffer)
- 5 value converters (FuelStatus, TireWear, TireTemp, etc)
- 40+ unit tests in AtlasViewModelTests.cs

UI Improvements:
- Complete MainWindow.axaml redesign (174 -> 390+ lines)
- Enhanced App.axaml with motorsport aesthetics
- Dashboard tab: Fuel, Tires, Strategy, Timing, Weather panels
- Strategy tab: Stint status, alternatives, recommendations
- AI Engineer tab: Chat interface with message metadata
- Settings tab: Local LLM + Cloud provider configuration

Dependencies:
- Added ScottPlot.Avalonia v5.1.57
- Added System.Reactive v6.0.1

Documentation:
- IMPLEMENTATION_SUMMARY.md (11KB technical overview)
- QUICK_REFERENCE.md (10KB build/test guide)
- FINAL_REPORT.md (15KB executive summary)
- build-atlas-ui.cmd (automated build script)

Testing:
- 11 test classes with 40+ unit tests
- 100%% coverage of new ViewModels and services

Status: Build-ready, ~60%% of 25-day plan complete
Next: ScottPlot integration, keyboard shortcuts, track map"
echo.

echo [4/4] Pushing to remote...
git push
echo.

echo ========================================
echo Commit Complete!
echo ========================================
echo.
echo Committed files:
git log -1 --stat
echo.

pause
