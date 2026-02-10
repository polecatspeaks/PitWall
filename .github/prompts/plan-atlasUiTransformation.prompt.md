# Plan: ATLAS Professional UI Transformation

This plan transforms PitWall.UI from basic MVP to full ATLAS-inspired professional motorsport interface with all required screens (Dashboard, Telemetry Analysis, Strategy, AI Engineer, Settings). We'll enhance the existing foundation rather than rebuild, leveraging the solid service layer and correct color palette while adding ScottPlot for waveform visualization and modularizing the architecture.

**Key Decisions:**
- **ScottPlot 5.x** for telemetry traces (performance + cursor support)
- **Enhance-and-modularize** strategy (not full rewrite)
- **TabControl navigation** for multi-page architecture
- **Extract ViewModels** by domain (5 separate VMs)
- **UserControl components** for reusable panels
- **Track maps via Canvas/SVG** (OSINT for geometry data)

**Steps**

### Phase 1: Architecture Foundation (Days 1-4)

1. **Install dependencies and create structure**
   - Add [PitWall.UI/PitWall.UI.csproj](PitWall.UI/PitWall.UI.csproj): `ScottPlot.Avalonia` v5.1.57, `System.Reactive` v6.0.1
   - Create folders: [Views/](Views/), [Controls/](Controls/), [Converters/](Converters/), [Styles/](Styles/)
   - Add [Styles/AtlasTheme.axaml](Styles/AtlasTheme.axaml): ATLAS typography (Rajdhani, Roboto Mono), status gradients, panel hover states

2. **Implement multi-page navigation in** [Views/MainWindow.axaml](Views/MainWindow.axaml)
   - Replace current Row 0-4 layout with `TabControl` (Dashboard | Telemetry | Strategy | Competitors | AI Engineer | Settings)
   - Keep existing status bar as persistent header (Lap, Position, Fuel, Alerts)
   - Extract each tab content to separate View file

3. **Split ViewModels by domain**
   - Extract from [ViewModels/MainWindowViewModel.cs](ViewModels/MainWindowViewModel.cs) into:
     - `DashboardViewModel` (telemetry display, recommendations)
     - `TelemetryAnalysisViewModel` (historical traces, lap comparison)
     - `StrategyViewModel` (pit planning, predictions)
     - `AiAssistantViewModel` (chat, context display)
     - `SettingsViewModel` (LLM config, discovery)
   - Keep `MainWindowViewModel` as orchestrator (WebSocket management, message bus)
   - Use `CommunityToolkit.Mvvm.Messaging` for cross-VM events (lap completed, pit stop)

4. **Create reusable panel UserControls in** [Controls/](Controls/)
   - [FuelPanel.axaml](Controls/FuelPanel.axaml): Large value, progress bar, consumption rate (▸ 3.2 L/lap), estimated laps
   - [TirePanel.axaml](Controls/TirePanel.axaml): 4-corner grid, wear % with dots (● ◐ ○), temps with gradient colors
   - [TimingPanel.axaml](Controls/TimingPanel.axaml): Last/Best/Delta with trend arrows (▲▼), sector times
   - [StrategyPanel.axaml](Controls/StrategyPanel.axaml): Next pit lap, confidence badge, action button
   - [WeatherPanel.axaml](Controls/WeatherPanel.axaml): Conditions, temps, grip percentage
   - Each panel: 200-250px width, self-contained ViewModel property bindings

### Phase 2: Dashboard View (Days 5-7)

5. **Build** [Views/DashboardView.axaml](Views/DashboardView.axaml) **using new components**
   - Row 0: 3-column grid (Fuel + Tires + Strategy panels)
   - Row 1: 3-column grid (Timing + Weather + Battery panels for Hypercars)
   - Row 2: Track map placeholder (Canvas control, "Coming Soon" for now)
   - Row 3: Competitor timing table (Position, Car, Gap, Last Lap, Pit Status)
   - Use `DashboardViewModel` with real-time telemetry updates from WebSocket

6. **Implement status indicators**
   - Create [Converters/FuelStatusConverter.cs](Converters/FuelStatusConverter.cs): < 2 laps = red, < 4 = amber, else green
   - Create [Converters/TireWearConverter.cs](Converters/TireWearConverter.cs): < 15% = red, < 30% = amber, else green
   - Add animated `ProgressBar` controls with smooth fill transitions (200ms easing)
   - Status dots (Ellipse 12×12px) with ToolTip explanations

7. **Add alert system banner**
   - Create [Controls/AlertBanner.axaml](Controls/AlertBanner.axaml): Full-width, colored border, icon + message + dismiss
   - Priority levels: Critical (red, full-screen overlay), Warning (amber, banner), Info (cyan, notification)
   - Wire to `MainWindowViewModel.AlertsDisplay` with auto-dismiss timers (5s for warnings)

### Phase 3: Telemetry Analysis View (Days 8-12)

8. **Create ATLAS-style waveform display in** [Views/TelemetryAnalysisView.axaml](Views/TelemetryAnalysisView.axaml)
   - Vertical stack of 5 `AvaPlot` controls (ScottPlot) for: Speed, Throttle, Brake, Steering, Tire Temps
   - Each chart: 800px width × 120px height, shared X-axis (time/distance), independent Y-axis
   - Red trace (current lap), blue trace (reference lap - personal best or selected)
   - Synchronized crosshair across all charts using shared `MouseMove` handler

9. **Implement data buffering system**
   - Create [Services/TelemetryBuffer.cs](Services/TelemetryBuffer.cs): `CircularBuffer<TelemetrySample>` (10,000 samples = 100 seconds @ 100Hz)
   - Index by lap number for lap-by-lap access
   - Method: `GetLapData(int lapNumber)` → array of samples for single lap
   - Hook buffer to WebSocket stream in `MainWindowViewModel.UpdateTelemetry()`

10. **Build cursor data table below waveforms**
    - DataGrid showing parameter names, current lap value, reference lap value, delta (with ▲▼), units
    - Update table on crosshair position change (extract values from buffer at cursor X coordinate)
    - Rows: vSpeed, nThrottle, nBrake, rSteer, tTyreCentre (FL/FR/RL/RR), fFuel, gLat, gLong
    - Scientific naming convention (ATLAS-style), right-aligned numeric columns

11. **Add lap selector and playback controls**
    - ComboBox: "Select Reference Lap" (populated from buffer lap index)
    - Buttons: [◀ Prev Sector] [Zoom In] [Zoom Out] [▶ Next Sector] [Export CSV]
    - Implement zoom via ScottPlot `AxisLimits` manipulation
    - Export: Write selected lap data to CSV with headers

### Phase 4: Strategy Dashboard (Days 13-15)

12. **Create strategy planning view in** [Views/StrategyView.axaml](Views/StrategyView.axaml)
    - Section 1: Current stint status (fuel bar, tire bar, stint progress bar with optimal range marker)
    - Section 2: Predicted strategy timeline (horizontal with lap markers, pit stop icons at predicted laps)
    - Section 3: Alternative strategy cards (2-stop aggressive, 1-stop conservative) with confidence scores
    - Section 4: Competitor strategies table (estimated next pit, last pit lap, strategy type)

13. **Implement strategy timeline visualization**
    - Create [Controls/StrategyTimelineControl.axaml](Controls/StrategyTimelineControl.axaml): Custom Canvas-based control
    - Draw horizontal line for race duration (0 to total laps)
    - Draw ■ markers at pit stop laps with tooltips (fuel amount, tire change, stop time)
    - Color stints by tire compound (if available) or wear level
    - Click-and-drag pit markers to manually override strategy

14. **Wire to StrategyEngine predictions**
    - `StrategyViewModel` polls [Services/RecommendationClient.cs](Services/RecommendationClient.cs) every 2 seconds
    - Display: Next pit lap, fuel to add, tire change yes/no, confidence percentage
    - "Override Strategy" button opens dialog to manually set pit lap
    - "Fuel Save Mode" button sends `/agent/config` update (future API endpoint)

### Phase 5: AI Engineer Panel (Days 16-17)

15. **Extract AI assistant to dedicated view** [Views/AiEngineerView.axaml](Views/AiEngineerView.axaml)
    - Top section: Scrolling conversation history (ItemsControl with VirtualizingStackPanel)
    - Message template: User avatar/timestamp, response with source badge (Rules Engine 1ms / LLM 1.2s), context box
    - Bottom section: Input TextBox with watermark, [🎤 Voice] + [Send] buttons, quick query buttons
    - Context visibility panel (collapsible): Shows race state sent to AI in monospace font

16. **Enhance message display**
    - Create [Controls/AiMessageControl.axaml](Controls/AiMessageControl.axaml): UserControl for chat bubbles
    - User messages: Right-aligned, dark panel, white text
    - AI responses: Left-aligned, colored border (green=rules, cyan=LLM), timestamp + source + confidence
    - Add "📊 Context" expander showing telemetry snapshot used for that query
    - Implement auto-scroll to latest message

17. **Add quick query buttons**
    - 4 buttons: [Fuel Status?] [Pit Strategy?] [Tire Temps?] [Pace Delta?]
    - Click sends pre-defined query string to [Services/AgentQueryClient.cs](Services/AgentQueryClient.cs)
    - Future: [🎤 Voice Input] uses speech-to-text (placeholder for now)

### Phase 6: Track Map & Competitors (Days 18-20)

18. **Research and acquire track geometry data**
    - Search OSINT sources: OpenStreetMap, racing game mods, iRacing track CSVs
    - Target format: Array of (X, Y) coordinates for track centerline
    - Start with 3-5 popular tracks (Le Mans, Spa, Monza, Sebring, Daytona)
    - Store as JSON files in [Assets/TrackMaps/](Assets/TrackMaps/) folder

19. **Build track map control** [Controls/TrackMapControl.axaml](Controls/TrackMapControl.axaml)
    - Custom Canvas control rendering track outline as Polyline (white stroke, 2px width)
    - Dynamic position markers: Ellipse per car (12px diameter, class color, car number overlay)
    - Calculate car position from lap progress percentage (interpolate along track coordinates)
    - Zoom/pan gestures (mouse wheel = zoom, drag = pan)

20. **Create competitors view** [Views/CompetitorsView.axaml](Views/CompetitorsView.axaml)
    - DataGrid: Position, Car #, Driver, Class icon, Gap, Last Lap, Best Lap, Pit Status, Laps
    - Sort by position, color-code by class (LMP1=red, LMP2=blue, GTE=green)
    - Click row highlights that car on track map
    - Real-time updates from API (future endpoint: `/api/standings`)

### Phase 7: Settings & Polish (Days 21-25)

21. **Extract settings to dedicated view** [Views/SettingsView.axaml](Views/SettingsView.axaml)
    - Move all LLM config, discovery settings, cloud provider settings from current MainWindow Row 4
    - Use `SettingsViewModel` extracted from `MainWindowViewModel`
    - Organize into collapsible groups: Local LLM, Discovery, OpenAI, Anthropic
    - Keep existing discovery button + results display logic

22. **Implement performance optimizations**
    - Add [Services/TelemetryThrottler.cs](Services/TelemetryThrottler.cs): Rx.NET `Sample(16ms)` operator on WebSocket stream
    - String pooling for frequently updated values: Create `StringCache` for lap time formats
    - Profile with Avalonia DevTools (F12) to identify layout thrashing
    - Ensure 60 FPS during telemetry updates (use `RenderTimer` to measure)

23. **Add ATLAS typography and final styling touches**
    - Update [Styles/AtlasTheme.axaml](Styles/AtlasTheme.axaml) with font imports (Google Fonts API or local)
    - Apply `TextBlock.giant` (48px) to critical alerts
    - Apply `TextBlock.xxxl` (36px) to main data values (fuel liters, lap time)
    - Apply `TextBlock.xl` (20px) to section headers
    - Test all status color gradients (tire temp cold→optimal→hot)

24. **Implement keyboard shortcuts**
    - F1 = Dashboard tab, F2 = Telemetry tab, F3 = Strategy tab, F4 = Competitors tab, F5 = AI Engineer tab
    - F6 = Confirm pit request (if in optimal window), F12 = Emergency mode (critical info only)
    - Space = Pause/Resume telemetry updates, Esc = Dismiss alerts
    - Add [Services/KeyboardShortcutHandler.cs](Services/KeyboardShortcutHandler.cs) with `KeyDown` event subscription

25. **Cross-platform testing and bug fixes**
    - Test on Windows (primary), Linux (if available), macOS (if available)
    - Fix any Avalonia platform-specific rendering issues
    - Test with real LMU telemetry stream (full session playback)
    - Verify WebSocket reconnection logic on connection drop
    - Test all API endpoints (`/agent/config`, `/agent/query`, `/agent/llm/discover`, `/api/recommend`)

**Verification**

- **Build check:** `dotnet build PitWall.LMU/PitWall.UI` succeeds with no errors
- **Test suite:** `dotnet test PitWall.LMU/PitWall.UI.Tests` all pass (update test stubs for new ViewModels)
- **Performance:** Maintain 60 FPS with 5 telemetry traces + 100Hz WebSocket stream (measure with Avalonia DevTools)
- **UI validation:** All 6 tabs render correctly, navigation works, panels display real data
- **Discovery:** LLM discovery detects Ollama, AI queries return responses shown in engineer panel
- **Telemetry:** ATLAS waveforms display with synchronized cursor, lap comparison works
- **Strategy:** Timeline shows predicted pit stops, alternative strategies display with confidence
- **Manual test:** Run full stack (Agent + UI), connect to LMU, complete 5-lap session, verify all features

**Decisions**

- **Chose ScottPlot over LiveChartsCore:** Superior real-time performance (millions of points @ 60 FPS), native cursor support, purpose-built for streaming telemetry. LiveChartsCore still in RC phase.
- **Enhance vs rebuild:** Existing foundation (color palette, services, WebSocket) is solid. Modularize architecture but keep core intact.
- **Track map via OSINT:** No official API, use OpenStreetMap/gaming community resources for track coordinates. Start with 3-5 tracks.
- **Performance first:** 16ms frame budget (60 FPS) is non-negotiable. Use Rx.NET throttling and string pooling to achieve this.

**Timeline Estimate:** 25 working days (~5 weeks at full-time pace) for complete ATLAS transformation with all MVP screens.

## Current State Summary

**Technical Stack:**
- Avalonia 11.3.11 + .NET 9.0
- CommunityToolkit.Mvvm 8.2.1 (well-integrated)
- Native HttpClient + ClientWebSocket
- Color palette 95% ATLAS-compliant ✅
- Service layer architecture clean ✅

**What Works:**
- WebSocket telemetry streaming (100Hz backend → UI)
- AI query/response flow via AgentQueryClient
- Settings persistence (GET/PUT /agent/config)
- Recommendation polling (2-second intervals)
- LLM discovery feature (now working with firewall fix)

**Architecture Gaps:**
- Monolithic single-page design (469-line MainWindowViewModel)
- No multi-page navigation (need TabControl)
- No component modularity (all hardcoded in MainWindow.axaml)
- No separation of concerns (everything in one ViewModel)

**Visualization Gaps (Blockers):**
- ⛔ No charting library (need ScottPlot.Avalonia v5.1.57)
- ⛔ No track map visualization (need custom Canvas)
- ⛔ No data history storage (need CircularBuffer)
- 📊 Basic progress bars (need animation + gradients)

**Recommended Approach:** **ENHANCE + MODULARIZE** (not full rebuild)
- 30% ATLAS alignment currently
- 3-4 weeks for full transformation
- Reuse: color palette, service layer, WebSocket streaming
- Add: ScottPlot, ViewModels split, UserControls, TabControl navigation
