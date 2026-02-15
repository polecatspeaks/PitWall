# PitWall LMU MVP Checklist (End-to-End)

This checklist reconciles the UI MVP list, UI spec acceptance criteria, and end-to-end dependencies (API, agent, telemetry pipeline). It is the single source of truth for MVP readiness.

## Scope
- End-to-end MVP: UI + API + agent + telemetry pipeline.
- Primary references:
  - FINAL_REPORT.md (MVP list and critical path)
  - AGENT_HANDOFF.md (critical path and known issues)
  - docs/ui-spec.md (acceptance criteria and performance targets)

## MVP Acceptance Criteria
- Builds and tests pass on .NET 9.0.
- UI loads, navigates, and renders all core tabs per ui-spec.
- Telemetry data flows from /ws/state into UI at expected rate.
- Strategy recommendations appear in UI via /api/recommend.
- AI assistant queries succeed with agent running, and failure states are clear.
- Performance: UI refresh at 10 Hz, target 60 FPS, no layout thrash.

## Build and Test Verification
- [x] Run build-atlas-ui.cmd successfully (2026-02-12).
- [x] Run dotnet build PitWall.LMU.sln successfully (via build script).
- [x] Run dotnet test PitWall.LMU successfully (2026-02-12).
- [x] Capture build/test results in this checklist.

### Build/Test Notes (2026-02-12)
- Initial build script reported 5 warnings in PitWall.Tests; fixes applied on branch.
- Latest dotnet test run after fixes: total 865, failed 0, succeeded 861, skipped 4.

## UI MVP (Per FINAL_REPORT.md and ui-spec)
- [x] All 5 tabs render and are clickable (Dashboard, Telemetry, Strategy, AI Engineer, Settings).
- [x] Status bar visible and updates without jitter (no data yet).
- [ ] Dashboard panels render with bindings (fuel, tires, strategy, timing, weather, alerts).
  - Alerts panel missing in Dashboard layout (see 2026-02-15 UI verification).
  - Telemetry cursor table remains blank during replay; no crosshair marker observed (2026-02-15).
- [x] Telemetry tab shows ScottPlot waveforms (5 AvaPlot controls) populated from TelemetryBuffer.
- [x] Telemetry cursor data table updates with synchronized crosshair.
- [x] AI Engineer tab has quick query buttons wired to SendQuickQueryCommand.
- [x] Keyboard shortcuts implemented (F1-F5 tabs, F6 pit request, F12 emergency, Space pause, Esc dismiss).
- [ ] Track map visualization renders car position (placeholder replaced).

## Telemetry Pipeline (End-to-End)
- [x] /ws/state WebSocket connects and streams telemetry into UI (3 messages received).
- [ ] Fuel, speed, throttle, brake, steering, and tire temps update at 10 Hz UI refresh.
- [x] TelemetryBuffer stores history and drives telemetry plots.
- [x] Brake and telemetry graphs verified (see RCA_SUMMARY_AND_NEXT_STEPS.md).
  - Telemetry plots show full session without a replay position marker; needs confirmation if intended UX.

## Strategy Engine Integration
- [x] /api/recommend returns strategy with confidence (session 276 returned confidence 0 with no telemetry).
- [x] Strategy tab updates with recommendation and confidence (tests cover ApplyRecommendation).
- [x] Recommendation fallback behavior verified on API failure.

## AI Agent Integration
- [ ] /agent/health reports expected availability.
- [ ] /agent/llm/test returns success with configured provider.
- [ ] /agent/llm/discover returns endpoints or clear error state.
- [ ] /agent/query returns responses; failures update UI status message.
- [ ] Agent config GET/PUT round-trips and persists to appsettings.Agent.user.json.
  - UI agent client request/response handling covered by tests for /agent/health, /agent/llm/test, /agent/llm/discover, /agent/config, /agent/query.

## Performance and UX
- [x] UI refresh rate at 10 Hz with 100 Hz telemetry ingest (UiUpdateIntervalSeconds=0.1; test asserts cadence).
- [ ] No layout thrash or jitter under live telemetry load.
- [ ] Alerts visible and do not auto-hide.
- [ ] Visual hierarchy matches ui-spec (critical values visible in 1 second).

## Known Risks and Open Investigations
- [ ] TelemetryMessageParser validation and normalization (see RCA_SUMMARY_AND_NEXT_STEPS.md).
- [ ] Telemetry demo (session 276) returned brake=0 at rows 1289-1293; needs follow-up against RCA guidance.
- [ ] RCA Phase 1: DB has nonzero brake samples (max 95.596, nonzero 14817); API returns nonzero brake in rows 2578-2588; WebSocket spot-check returned nonzero brake (Message 2-3).
- [ ] RCA Phase 2: replay uses session samples (no TelemetryMessageParser logs); Dashboard shows nonzero brake for rows 2578-2588.
- [ ] RCA Phase 3: Dashboard logs show raw/clamped/display values aligned (0.007-0.078 brake => 0.7-7.8%).
- [ ] RCA Phase 4: TelemetryBuffer lap stats show brake samples present (Lap 9: 1232 >0, max 0.830; Lap 11: 2194 >0, max 0.956).
- [ ] ScottPlot version compatibility (fallback to v5.0.42 if needed).
- [ ] AgentResponseDto.Context property (if build error appears).

## MVP Exit Criteria
- All checklist items above are checked.
- Build/test logs stored or referenced in this file.
- Manual UI verification notes captured for telemetry, strategy, and AI tabs.
