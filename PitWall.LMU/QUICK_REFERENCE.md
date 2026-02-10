# ATLAS UI Transformation - Quick Reference Guide

## Build & Test

### Option 1: Using Build Script
```cmd
cd C:\Users\ohzee\.claude-worktrees\PitWall\vibrant-meninsky.worktrees\copilot-worktree-2026-02-10T01-41-25\PitWall.LMU
build-atlas-ui.cmd
```

### Option 2: Manual Commands
```cmd
cd C:\Users\ohzee\.claude-worktrees\PitWall\vibrant-meninsky.worktrees\copilot-worktree-2026-02-10T01-41-25\PitWall.LMU

# Restore packages
dotnet restore

# Build
dotnet build

# Test
dotnet test --verbosity normal

# Run
dotnet run --project PitWall.UI\PitWall.UI.csproj
```

## Expected Build Issues & Fixes

### Issue 1: Missing AgentResponseDto.Context Property
**Error**: `'AgentResponseDto' does not contain a definition for 'Context'`

**Fix**: Add to `Models/AgentResponseDto.cs`:
```csharp
public string? Context { get; set; }
```

### Issue 2: Missing RecommendationDto Properties
**Error**: Properties not found on RecommendationDto

**Fix**: Verify `Models/RecommendationDto.cs` has:
```csharp
public string Recommendation { get; set; }
public double Confidence { get; set; }
```

### Issue 3: ScottPlot Compatibility
**Error**: Version conflicts with Avalonia 11.3.11

**Fix**: If ScottPlot.Avalonia v5.1.57 is incompatible, try:
```xml
<PackageReference Include="ScottPlot.Avalonia" Version="5.0.42" />
```

### Issue 4: Missing Converters
**Warning**: Converters not found in App.axaml

**Status**: Converters are temporarily in `Models/ValueConverters.cs`. No action needed unless you prefer to move them to a `Converters` folder.

## Testing the UI

### Tab Navigation Testing
1. Launch app: `dotnet run --project PitWall.UI\PitWall.UI.csproj`
2. Verify all 5 tabs are clickable
3. Check tab content renders without errors

### Dashboard Testing
- Verify fuel, tire, strategy, and timing panels display
- Check status bar shows lap, position, gap data
- Confirm alert banner appears/hides correctly

### Settings Testing
1. Click Settings tab
2. Modify LLM endpoint
3. Click "Save Settings"
4. Click "Reload Settings"
5. Verify changes persisted

### AI Assistant Testing
1. Click AI Engineer tab
2. Type a query: "How much fuel remaining?"
3. Click Send
4. Verify message appears in history

## Architecture Overview

### ViewModel Hierarchy
```
MainWindowViewModel (Orchestrator)
  ├─ Dashboard: DashboardViewModel
  ├─ TelemetryAnalysis: TelemetryAnalysisViewModel
  ├─ Strategy: StrategyViewModel
  ├─ AiAssistant: AiAssistantViewModel
  └─ Settings: SettingsViewModel
```

### Data Flow
```
WebSocket Telemetry
  ↓
MainWindowViewModel.UpdateTelemetry()
  ↓
TelemetryBuffer.Add()
  ↓
Dashboard.UpdateTelemetry()
  ↓
UI Bindings Update
```

### Recommendation Flow
```
PeriodicTimer (2s)
  ↓
RecommendationClient.GetRecommendationAsync()
  ↓
Dashboard.UpdateRecommendation()
Strategy.UpdateFromRecommendation()
  ↓
UI Bindings Update
```

## Key Bindings (Planned)

| Key | Action |
|-----|--------|
| F1 | Dashboard Tab |
| F2 | Telemetry Tab |
| F3 | Strategy Tab |
| F4 | AI Engineer Tab |
| F5 | Settings Tab |
| F6 | Pit Request |
| F12 | Emergency Mode |
| Space | Pause Telemetry |
| Esc | Dismiss Alerts |

## Next Implementation Steps

### Priority 1: ScottPlot Integration (Phase 3)
**File**: `Views/MainWindow.axaml` - Telemetry Tab

Replace placeholder with:
```xml
<Grid RowDefinitions="Auto,*,*,*,*,*,Auto" RowSpacing="4">
    <!-- Add 5 AvaPlot controls here -->
    <AvaPlot Grid.Row="1" Height="120" />
    <AvaPlot Grid.Row="2" Height="120" />
    <AvaPlot Grid.Row="3" Height="120" />
    <AvaPlot Grid.Row="4" Height="120" />
    <AvaPlot Grid.Row="5" Height="120" />
</Grid>
```

**ViewModel**: Populate plots in `TelemetryAnalysisViewModel`:
```csharp
public void PopulatePlots(TelemetrySampleDto[] lapData)
{
    SpeedData.Clear();
    for (int i = 0; i < lapData.Length; i++)
    {
        SpeedData.Add(new TelemetryDataPoint 
        { 
            Time = i * 0.01, // 100Hz = 10ms
            Value = lapData[i].SpeedKph 
        });
    }
}
```

### Priority 2: Quick Query Buttons (Phase 5)
**File**: `Views/MainWindow.axaml` - AI Engineer Tab

Add before input box:
```xml
<UniformGrid Rows="2" Columns="2" Margin="0,0,0,8">
    <Button Classes="quick-query" Content="🔋 Fuel Status?" 
            Command="{Binding AiAssistant.SendQuickQueryCommand}" 
            CommandParameter="How much fuel remaining?" />
    <Button Classes="quick-query" Content="🎯 Pit Strategy?" 
            Command="{Binding AiAssistant.SendQuickQueryCommand}" 
            CommandParameter="When should I pit?" />
    <Button Classes="quick-query" Content="🔥 Tire Temps?" 
            Command="{Binding AiAssistant.SendQuickQueryCommand}" 
            CommandParameter="How are my tire temperatures?" />
    <Button Classes="quick-query" Content="⚡ Pace Delta?" 
            Command="{Binding AiAssistant.SendQuickQueryCommand}" 
            CommandParameter="Am I faster or slower than last lap?" />
</UniformGrid>
```

### Priority 3: Track Map (Phase 6)
**File**: Create `Controls/TrackMapControl.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="PitWall.UI.Controls.TrackMapControl">
    <Canvas x:Name="TrackCanvas" Background="#1A1A1A">
        <!-- Track outline rendered here -->
    </Canvas>
</UserControl>
```

**Code-behind**: Implement track rendering in `TrackMapControl.axaml.cs`

### Priority 4: Keyboard Shortcuts (Phase 7)
**File**: `Views/MainWindow.axaml.cs`

Add to constructor:
```csharp
this.KeyDown += OnKeyDown;

private void OnKeyDown(object? sender, KeyEventArgs e)
{
    if (DataContext is not MainWindowViewModel vm) return;
    
    switch (e.Key)
    {
        case Key.F1: vm.SelectedTabIndex = 0; break;
        case Key.F2: vm.SelectedTabIndex = 1; break;
        case Key.F3: vm.SelectedTabIndex = 2; break;
        case Key.F4: vm.SelectedTabIndex = 3; break;
        case Key.F5: vm.SelectedTabIndex = 4; break;
    }
}
```

## Performance Optimization

### Telemetry Throttling
**File**: Create `Services/TelemetryThrottler.cs`

```csharp
using System.Reactive.Linq;

public class TelemetryThrottler
{
    public IObservable<T> Throttle<T>(IObservable<T> source)
    {
        return source.Sample(TimeSpan.FromMilliseconds(16)); // 60 FPS
    }
}
```

### String Pooling
Cache frequently updated strings to reduce allocations:
```csharp
private static class StringCache
{
    private static readonly Dictionary<string, string> Cache = new();
    
    public static string GetCached(string value)
    {
        if (Cache.TryGetValue(value, out var cached))
            return cached;
        Cache[value] = value;
        return value;
    }
}
```

## Avalonia DevTools

Press **F12** in running application to open DevTools:
- **Visual Tree**: Inspect UI hierarchy
- **Events**: Monitor property changes
- **Performance**: Profile rendering

## Common XAML Patterns

### Data Binding
```xml
<!-- One-Way (VM → UI) -->
<TextBlock Text="{Binding FuelLiters}" />

<!-- Two-Way (VM ↔ UI) -->
<TextBox Text="{Binding LlmEndpoint, Mode=TwoWay}" />

<!-- Command -->
<Button Command="{Binding SaveSettingsCommand}" />

<!-- Converter -->
<Border Background="{Binding FuelLaps, Converter={StaticResource FuelStatusConverter}}" />
```

### Styling
```xml
<!-- Apply style class -->
<TextBlock Classes="section-header" Text="FUEL" />

<!-- Multiple classes -->
<Border Classes="atlas-panel atlas-panel-success" />

<!-- Dynamic resource -->
<TextBlock Foreground="{DynamicResource BrushSuccess}" />
```

## Debugging Tips

### Breakpoints in ViewModels
```csharp
[ObservableProperty]
private string fuelLiters = "-- L";
// Set breakpoint on property getter/setter

partial void OnFuelLitersChanged(string value)
{
    // Set breakpoint here to catch all changes
}
```

### Logging Telemetry Updates
```csharp
public void UpdateTelemetry(TelemetrySampleDto telemetry)
{
    System.Diagnostics.Debug.WriteLine($"Telemetry: Fuel={telemetry.FuelLiters}, Speed={telemetry.SpeedKph}");
    Dashboard.UpdateTelemetry(telemetry);
}
```

### Check Binding Errors
Enable Avalonia trace logging in `Program.cs`:
```csharp
LogEventLevel.Debug // Change from Warning to Debug
```

## File Structure Reference

```
PitWall.UI/
├── ViewModels/
│   ├── MainWindowViewModel.cs ✅ (Refactored)
│   ├── DashboardViewModel.cs ✅ (New)
│   ├── TelemetryAnalysisViewModel.cs ✅ (New)
│   ├── StrategyViewModel.cs ✅ (New)
│   ├── AiAssistantViewModel.cs ✅ (New)
│   ├── SettingsViewModel.cs ✅ (New)
│   └── ViewModelBase.cs (Existing)
├── Views/
│   ├── MainWindow.axaml ✅ (Complete Redesign)
│   └── MainWindow.axaml.cs (Existing)
├── Services/
│   ├── TelemetryBuffer.cs ✅ (New)
│   ├── TelemetryStreamClient.cs (Existing)
│   ├── RecommendationClient.cs (Existing)
│   ├── AgentQueryClient.cs (Existing)
│   └── AgentConfigClient.cs (Existing)
├── Models/
│   ├── TelemetrySampleDto.cs ✅ (Updated)
│   ├── RecommendationDto.cs (Existing)
│   ├── AgentResponseDto.cs (Existing)
│   ├── AgentConfigDto.cs (Existing)
│   ├── AiMessage.cs (Existing)
│   └── ValueConverters.cs ✅ (New)
├── App.axaml ✅ (Enhanced with ATLAS styles)
├── Program.cs (Existing)
└── IMPLEMENTATION_SUMMARY.md ✅ (New)

PitWall.UI.Tests/
└── AtlasViewModelTests.cs ✅ (New - 11 test classes)
```

## Contact & Support

If you encounter issues:
1. Check `IMPLEMENTATION_SUMMARY.md` for detailed documentation
2. Review build errors and apply fixes from this guide
3. Use Avalonia DevTools (F12) for runtime debugging
4. Check existing tests in `PitWall.UI.Tests/` for usage examples

## Success Criteria

✅ Application builds without errors  
✅ All 5 tabs are navigable  
✅ Status bar displays correctly  
✅ Dashboard panels render with data bindings  
✅ Settings can be saved/loaded  
✅ AI Assistant accepts queries  
✅ Tests pass (minimum 80% of AtlasViewModelTests)  

## What's Next?

After successful build:
1. **Test Drive**: Run the app and navigate all tabs
2. **Connect API**: Start PitWall.Agent and verify telemetry flow
3. **ScottPlot**: Integrate waveform displays
4. **Track Map**: Add visual track representation
5. **Polish**: Keyboard shortcuts, animations, performance tuning

---

**Created**: 2026-02-10  
**Version**: 1.0  
**Status**: Ready for Build & Test
