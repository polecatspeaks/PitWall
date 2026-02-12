using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using PitWall.UI.ViewModels;
using PitWall.UI.Models;
using PitWall.UI.Services;

namespace PitWall.UI.Tests;

/// <summary>
/// Tests for DashboardViewModel telemetry updates and data display.
/// </summary>
public class DashboardViewModelTests
{
	[Fact]
	public void UpdateTelemetry_UpdatesFuelDisplay()
	{
		// Arrange
		var viewModel = new DashboardViewModel();
		var telemetry = new TelemetrySampleDto
		{
			FuelLiters = 45.2,
			SpeedKph = 250.5,
			TyreTempsC = new[] { 92.0, 94.0, 90.0, 91.0 }
		};

		// Act
		viewModel.UpdateTelemetry(telemetry);

		// Assert
		Assert.Equal("45.2 L", viewModel.FuelLiters);
		Assert.Equal("250.5 KPH", viewModel.SpeedDisplay);
	}

	[Fact]
	public void UpdateTelemetry_UpdatesTireTemps()
	{
		// Arrange
		var viewModel = new DashboardViewModel();
		var telemetry = new TelemetrySampleDto
		{
			FuelLiters = 40.0,
			TyreTempsC = new[] { 85.5, 87.2, 83.1, 86.9 }
		};

		// Act
		viewModel.UpdateTelemetry(telemetry);

		// Assert
		Assert.Equal(85.5, viewModel.TireFLTemp);
		Assert.Equal(87.2, viewModel.TireFRTemp);
		Assert.Equal(83.1, viewModel.TireRLTemp);
		Assert.Equal(86.9, viewModel.TireRRTemp);
	}

	[Fact]
	public void UpdateRecommendation_UpdatesStrategy()
	{
		// Arrange
		var viewModel = new DashboardViewModel();
		var recommendation = new RecommendationDto
		{
			Recommendation = "Pit on lap 18",
			Confidence = 0.85
		};

		// Act
		viewModel.UpdateRecommendation(recommendation);

		// Assert
		Assert.Equal("Pit on lap 18", viewModel.StrategyMessage);
		Assert.Equal(0.85, viewModel.StrategyConfidence);
	}

	[Fact]
	public void Alerts_InitiallyEmpty()
	{
		// Arrange & Act
		var viewModel = new DashboardViewModel();

		// Assert
		Assert.Empty(viewModel.Alerts);
	}
}

/// <summary>
/// Tests for TelemetryAnalysisViewModel lap selection and data management.
/// </summary>
public class TelemetryAnalysisViewModelSmokeTests
{
	[Fact]
	public void SelectReferenceLap_UpdatesSelectedLap()
	{
		// Arrange
		var buffer = new TelemetryBuffer(1000);
		var viewModel = new TelemetryAnalysisViewModel(buffer);

		// Act
		viewModel.SelectReferenceLapCommand.Execute(5);

		// Assert
		Assert.Equal(5, viewModel.SelectedReferenceLap);
	}

	[Fact]
	public void DataCollections_InitiallyEmpty()
	{
		// Arrange & Act
		var buffer = new TelemetryBuffer(1000);
		var viewModel = new TelemetryAnalysisViewModel(buffer);

		// Assert
		Assert.Empty(viewModel.SpeedData);
		Assert.Empty(viewModel.ThrottleData);
		Assert.Empty(viewModel.BrakeData);
		Assert.Empty(viewModel.SteeringData);
		Assert.Empty(viewModel.TireTempData);
	}

	[Fact]
	public void AvailableLaps_InitiallyEmpty()
	{
		// Arrange & Act
		var buffer = new TelemetryBuffer(1000);
		var viewModel = new TelemetryAnalysisViewModel(buffer);

		// Assert
		Assert.Empty(viewModel.AvailableLaps);
	}

	[Fact]
	public void LoadCurrentLapData_PopulatesDataCollections()
	{
		// Arrange
		var buffer = new TelemetryBuffer(1000);
		for (int i = 0; i < 5; i++)
		{
			buffer.Add(new TelemetrySampleDto
			{
				LapNumber = 1,
				SpeedKph = 200.0 + i,
				ThrottlePosition = 0.75,
				BrakePosition = 0.0,
				SteeringAngle = 5.0,
				TyreTempsC = new[] { 90.0, 92.0, 88.0, 91.0 }
			});
		}
		var viewModel = new TelemetryAnalysisViewModel(buffer);

		// Act
		viewModel.LoadCurrentLapData(1);

		// Assert
		Assert.Equal(5, viewModel.SpeedData.Count);
		Assert.Equal(5, viewModel.ThrottleData.Count);
		Assert.Equal(5, viewModel.BrakeData.Count);
		Assert.Equal(5, viewModel.SteeringData.Count);
		Assert.Equal(5, viewModel.TireTempData.Count);
		Assert.Equal(200.0, viewModel.SpeedData[0].Value);
		Assert.Equal(75.0, viewModel.ThrottleData[0].Value); // Converted to percentage
	}

	[Fact]
	public void LoadCurrentLapData_HandlesEmptyLapGracefully()
	{
		// Arrange
		var buffer = new TelemetryBuffer(1000);
		var viewModel = new TelemetryAnalysisViewModel(buffer);

		// Act
		viewModel.LoadCurrentLapData(99); // Non-existent lap

		// Assert
		Assert.Empty(viewModel.SpeedData);
		Assert.Contains("No data", viewModel.StatusMessage);
	}

	[Fact]
	public void LoadReferenceLapData_PopulatesReferenceCollections()
	{
		// Arrange
		var buffer = new TelemetryBuffer(1000);
		for (int i = 0; i < 3; i++)
		{
			buffer.Add(new TelemetrySampleDto
			{
				LapNumber = 2,
				SpeedKph = 210.0,
				ThrottlePosition = 0.85,
				BrakePosition = 0.1,
				SteeringAngle = 3.0,
				TyreTempsC = new[] { 95.0, 96.0, 94.0, 95.5 }
			});
		}
		var viewModel = new TelemetryAnalysisViewModel(buffer);

		// Act
		viewModel.LoadReferenceLapData(2);

		// Assert
		Assert.Equal(3, viewModel.ReferenceSpeedData.Count);
		Assert.Equal(3, viewModel.ReferenceThrottleData.Count);
		Assert.Equal(210.0, viewModel.ReferenceSpeedData[0].Value);
	}

	[Fact]
	public void RefreshAvailableLaps_UpdatesLapList()
	{
		// Arrange
		var buffer = new TelemetryBuffer(1000);
		buffer.Add(new TelemetrySampleDto { LapNumber = 1 });
		buffer.Add(new TelemetrySampleDto { LapNumber = 2 });
		buffer.Add(new TelemetrySampleDto { LapNumber = 3 });
		var viewModel = new TelemetryAnalysisViewModel(buffer);

		// Act
		viewModel.RefreshAvailableLaps();

		// Assert
		Assert.Equal(3, viewModel.AvailableLaps.Count);
		Assert.Contains(1, viewModel.AvailableLaps);
		Assert.Contains(2, viewModel.AvailableLaps);
		Assert.Contains(3, viewModel.AvailableLaps);
	}

	[Fact]
	public void UpdateCursorData_PopulatesCursorDataRows()
	{
		// Arrange
		var buffer = new TelemetryBuffer(1000);
		buffer.Add(new TelemetrySampleDto
		{
			LapNumber = 1,
			SpeedKph = 250.0,
			ThrottlePosition = 0.80,
			BrakePosition = 0.0,
			SteeringAngle = 10.0,
			TyreTempsC = new[] { 92.0, 94.0, 90.0, 91.0 },
			FuelLiters = 45.5
		});
		var viewModel = new TelemetryAnalysisViewModel(buffer);
		viewModel.LoadCurrentLapData(1);

		// Act
		viewModel.UpdateCursorData(0.0); // First data point

		// Assert
		Assert.Equal(9, viewModel.CursorData.Count); // 9 parameters tracked
		var speedRow = viewModel.CursorData.FirstOrDefault(r => r.Parameter == "vSpeed");
		Assert.NotNull(speedRow);
		Assert.Equal("250.0", speedRow.CurrentValue);
		Assert.Equal("km/h", speedRow.Unit);
	}

	[Fact]
	public void UpdateCursorData_CalculatesDelta()
	{
		// Arrange
		var buffer = new TelemetryBuffer(1000);
		buffer.Add(new TelemetrySampleDto
		{
			LapNumber = 1,
			SpeedKph = 250.0,
			ThrottlePosition = 0.80
		});
		buffer.Add(new TelemetrySampleDto
		{
			LapNumber = 2,
			SpeedKph = 255.0, // 5 km/h faster
			ThrottlePosition = 0.85
		});
		var viewModel = new TelemetryAnalysisViewModel(buffer);
		viewModel.LoadCurrentLapData(1);
		viewModel.LoadReferenceLapData(2);

		// Act
		viewModel.UpdateCursorData(0.0);

		// Assert
		var speedRow = viewModel.CursorData.FirstOrDefault(r => r.Parameter == "vSpeed");
		Assert.NotNull(speedRow);
		Assert.Equal("250.0", speedRow.CurrentValue);
		Assert.Equal("255.0", speedRow.ReferenceValue);
		Assert.Contains("5.0", speedRow.Delta); // Delta contains arrow indicators like "▼ 5.0"
	}

	[Fact]
	public async Task ExportToCsvAsync_CreatesFile()
	{
		// Arrange
		var buffer = new TelemetryBuffer(1000);
		buffer.Add(new TelemetrySampleDto
		{
			LapNumber = 1,
			SpeedKph = 250.0,
			ThrottlePosition = 0.80
		});
		var viewModel = new TelemetryAnalysisViewModel(buffer);
		viewModel.LoadCurrentLapData(1);
		var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_telemetry_{Guid.NewGuid()}.csv");

		try
		{
			// Act
			await viewModel.ExportToCsvAsync(tempPath);

			// Assert
			Assert.True(System.IO.File.Exists(tempPath));
			var content = await System.IO.File.ReadAllTextAsync(tempPath);
			Assert.Contains("Time,Speed(km/h),Throttle(%),Brake(%),Steering", content); // CSV headers with units
			Assert.Contains("250.0", content); // Data values
		}
		finally
		{
			// Cleanup
			if (System.IO.File.Exists(tempPath))
				System.IO.File.Delete(tempPath);
		}
	}

	[Fact]
	public void CursorDataRows_AreObservable()
	{
		// Arrange
		var buffer = new TelemetryBuffer(1000);
		var viewModel = new TelemetryAnalysisViewModel(buffer);

		// Act
		buffer.Add(new TelemetrySampleDto { LapNumber = 1, SpeedKph = 200.0 });
		viewModel.LoadCurrentLapData(1);
		viewModel.UpdateCursorData(0.0);

		// Assert (CursorData collection should have items)
		Assert.NotEmpty(viewModel.CursorData);
	}
}

/// <summary>
/// Tests for StrategyViewModel pit planning and alternative strategies.
/// </summary>
public class StrategyViewModelTests : IDisposable
{
	private readonly string? _previousSampleDataFlag;

	public StrategyViewModelTests()
	{
		_previousSampleDataFlag = Environment.GetEnvironmentVariable("PITWALL_SAMPLE_STRATEGY_DATA");
		Environment.SetEnvironmentVariable("PITWALL_SAMPLE_STRATEGY_DATA", "1");
	}

	public void Dispose()
	{
		Environment.SetEnvironmentVariable("PITWALL_SAMPLE_STRATEGY_DATA", _previousSampleDataFlag);
	}

	[Fact]
	public void Constructor_InitializesAlternativeStrategies()
	{
		// Arrange & Act
		var viewModel = new StrategyViewModel();

		// Assert
		Assert.Equal(2, viewModel.AlternativeStrategies.Count);
		Assert.Contains(viewModel.AlternativeStrategies, s => s.Name == "2-Stop Aggressive");
		Assert.Contains(viewModel.AlternativeStrategies, s => s.Name == "1-Stop Conservative");
	}

	[Fact]
	public void ToggleFuelSaveMode_TogglesState()
	{
		// Arrange
		var viewModel = new StrategyViewModel();
		var initialState = viewModel.FuelSaveModeActive;

		// Act
		viewModel.ToggleFuelSaveModeCommand.Execute(null);

		// Assert
		Assert.NotEqual(initialState, viewModel.FuelSaveModeActive);
	}

	[Fact]
	public void UpdateFromRecommendation_UpdatesDisplayProperties()
	{
		// Arrange
		var viewModel = new StrategyViewModel();
		var recommendation = new RecommendationDto
		{
			Recommendation = "Continue current stint",
			Confidence = 0.92
		};

		// Act
		viewModel.UpdateFromRecommendation(recommendation);

		// Assert
		Assert.Equal("Continue current stint", viewModel.RecommendedAction);
		Assert.Equal(0.92, viewModel.StrategyConfidence);
	}

	[Fact]
	public void UpdateStintStatus_UpdatesAllStintProperties()
	{
		// Arrange
		var viewModel = new StrategyViewModel();

		// Act
		viewModel.UpdateStintStatus(fuelPct: 65.5, tireWear: 22.3, lap: 15, stintLaps: 8);

		// Assert
		Assert.Equal(65.5, viewModel.CurrentStintFuelPercentage);
		Assert.Equal(22.3, viewModel.CurrentStintTireWear);
		Assert.Equal(15, viewModel.CurrentLap);
		Assert.Equal(8, viewModel.StintLap);
		Assert.Equal(8, viewModel.StintDuration);
	}

	[Fact]
	public void UpdateStintStatus_WithZeroValues_UpdatesCorrectly()
	{
		// Arrange
		var viewModel = new StrategyViewModel();

		// Act
		viewModel.UpdateStintStatus(fuelPct: 0.0, tireWear: 0.0, lap: 1, stintLaps: 1);

		// Assert
		Assert.Equal(0.0, viewModel.CurrentStintFuelPercentage);
		Assert.Equal(0.0, viewModel.CurrentStintTireWear);
		Assert.Equal(1, viewModel.CurrentLap);
		Assert.Equal(1, viewModel.StintLap);
	}

	[Fact]
	public void UpdateStintStatus_WithMaxValues_UpdatesCorrectly()
	{
		// Arrange
		var viewModel = new StrategyViewModel();

		// Act
		viewModel.UpdateStintStatus(fuelPct: 100.0, tireWear: 100.0, lap: 50, stintLaps: 25);

		// Assert
		Assert.Equal(100.0, viewModel.CurrentStintFuelPercentage);
		Assert.Equal(100.0, viewModel.CurrentStintTireWear);
		Assert.Equal(50, viewModel.CurrentLap);
		Assert.Equal(25, viewModel.StintDuration);
	}

	[Fact]
	public void CalculatePitWindow_WithNormalConsumptionRates_CalculatesCorrectWindow()
	{
		// Arrange
		var viewModel = new StrategyViewModel();
		viewModel.UpdateStintStatus(fuelPct: 80.0, tireWear: 20.0, lap: 10, stintLaps: 5);
		
		// Act - Fuel limited scenario: 80% of 60L tank = 48L, at 3L/lap = 16 laps remaining
		//       Tire limited scenario: 80% wear remaining, at 5% wear/lap = 16 laps remaining
		viewModel.CalculatePitWindow(fuelPerLap: 3.0, tireWearPerLap: 5.0, tankCapacity: 60.0);

		// Assert - Should pit in 14-16 laps (current lap 10 + 16 - 2 buffer to lap 10 + 16)
		Assert.Equal(24, viewModel.OptimalPitLapStart); // Lap 10 + 16 - 2 = 24
		Assert.Equal(26, viewModel.OptimalPitLapEnd);   // Lap 10 + 16 = 26
		Assert.Equal(25, viewModel.NextPitLap);          // Lap 10 + 16 - 1 = 25 (middle of window)
	}

	[Fact]
	public void CalculatePitWindow_WhenFuelLimited_UsesMinimumLaps()
	{
		// Arrange
		var viewModel = new StrategyViewModel();
		viewModel.UpdateStintStatus(fuelPct: 30.0, tireWear: 10.0, lap: 5, stintLaps: 3);
		
		// Act - Fuel: 30% of 60L = 18L at 3L/lap = 6 laps remaining
		//       Tires: 90% remaining at 2% wear/lap = 45 laps remaining
		//       Min = 6 laps (fuel limited)
		viewModel.CalculatePitWindow(fuelPerLap: 3.0, tireWearPerLap: 2.0, tankCapacity: 60.0);

		// Assert - Should use fuel-limited calculation
		Assert.Equal(9, viewModel.OptimalPitLapStart);  // Lap 5 + 6 - 2 = 9
		Assert.Equal(11, viewModel.OptimalPitLapEnd);   // Lap 5 + 6 = 11
		Assert.Equal(10, viewModel.NextPitLap);          // Lap 5 + 6 - 1 = 10
	}

	[Fact]
	public void CalculatePitWindow_WhenTireLimited_UsesMinimumLaps()
	{
		// Arrange
		var viewModel = new StrategyViewModel();
		viewModel.UpdateStintStatus(fuelPct: 90.0, tireWear: 85.0, lap: 12, stintLaps: 10);
		
		// Act - Fuel: 90% of 60L = 54L at 2L/lap = 27 laps remaining
		//       Tires: 15% remaining at 5% wear/lap = 3 laps remaining
		//       Min = 3 laps (tire limited)
		viewModel.CalculatePitWindow(fuelPerLap: 2.0, tireWearPerLap: 5.0, tankCapacity: 60.0);

		// Assert - Should use tire-limited calculation
		Assert.Equal(13, viewModel.OptimalPitLapStart); // Lap 12 + 3 - 2 = 13
		Assert.Equal(15, viewModel.OptimalPitLapEnd);   // Lap 12 + 3 = 15
		Assert.Equal(14, viewModel.NextPitLap);          // Lap 12 + 3 - 1 = 14
	}

	[Fact]
	public void CalculatePitWindow_WithLowFuel_CreatesNarrowWindow()
	{
		// Arrange
		var viewModel = new StrategyViewModel();
		viewModel.UpdateStintStatus(fuelPct: 10.0, tireWear: 50.0, lap: 20, stintLaps: 15);
		
		// Act - Very low fuel: 10% of 60L = 6L at 3L/lap = 2 laps remaining
		viewModel.CalculatePitWindow(fuelPerLap: 3.0, tireWearPerLap: 3.0, tankCapacity: 60.0);

		// Assert - Urgent pit window
		Assert.Equal(20, viewModel.OptimalPitLapStart); // Lap 20 + 2 - 2 = 20 (must pit now!)
		Assert.Equal(22, viewModel.OptimalPitLapEnd);   // Lap 20 + 2 = 22
		Assert.Equal(21, viewModel.NextPitLap);
	}

	[Fact]
	public void CalculatePitWindow_WithHighTireWear_CreatesUrgentWindow()
	{
		// Arrange
		var viewModel = new StrategyViewModel();
		viewModel.UpdateStintStatus(fuelPct: 75.0, tireWear: 95.0, lap: 8, stintLaps: 6);
		
		// Act - Critical tire wear: 5% remaining at 5% wear/lap = 1 lap remaining
		viewModel.CalculatePitWindow(fuelPerLap: 2.5, tireWearPerLap: 5.0, tankCapacity: 60.0);

		// Assert - Must pit immediately
		Assert.Equal(7, viewModel.OptimalPitLapStart);  // Lap 8 + 1 - 2 = 7 (already past!)
		Assert.Equal(9, viewModel.OptimalPitLapEnd);    // Lap 8 + 1 = 9
		Assert.Equal(8, viewModel.NextPitLap);           // Lap 8 + 1 - 1 = 8 (this lap!)
	}

	[Fact]
	public void Constructor_InitializesPitStopMarkers()
	{
		// Arrange & Act
		var viewModel = new StrategyViewModel();

		// Assert
		Assert.NotEmpty(viewModel.PitStopMarkers);
		Assert.Contains(viewModel.PitStopMarkers, p => p.Lap == 18);
	}

	[Fact]
	public void Constructor_InitializesCompetitorStrategies()
	{
		// Arrange & Act
		var viewModel = new StrategyViewModel();

		// Assert
		Assert.NotEmpty(viewModel.CompetitorStrategies);
		Assert.Contains(viewModel.CompetitorStrategies, c => c.Driver == "Competitor A");
	}
}

/// <summary>
/// Tests for AiAssistantViewModel message handling and queries.
/// </summary>
public class AtlasAiAssistantViewModelTests
{
	[Fact]
	public async Task SendQuery_WithEmptyInput_DoesNothing()
	{
		// Arrange
		var viewModel = new AiAssistantViewModel(new NullAgentQueryClient());
		viewModel.InputText = "";

		// Act
		await viewModel.SendQueryCommand.ExecuteAsync(null);

		// Assert
		Assert.Empty(viewModel.Messages);
	}

	[Fact]
	public async Task SendQuery_AddsUserMessage()
	{
		// Arrange
		var viewModel = new AiAssistantViewModel(new NullAgentQueryClient());
		viewModel.InputText = "How much fuel remaining?";

		// Act
		await viewModel.SendQueryCommand.ExecuteAsync(null);

		// Assert
		Assert.NotEmpty(viewModel.Messages);
		Assert.Equal("User", viewModel.Messages[0].Role);
		Assert.Equal("How much fuel remaining?", viewModel.Messages[0].Text);
	}

	[Fact]
	public async Task SendQuery_UpdatesStatusMessage()
	{
		// Arrange
		var viewModel = new AiAssistantViewModel(new NullAgentQueryClient());
		viewModel.InputText = "Fuel status";

		// Act
		await viewModel.SendQueryCommand.ExecuteAsync(null);

		// Assert
		Assert.Contains("Response received", viewModel.StatusMessage);
	}

	[Fact]
	public async Task SendQuery_ClearsInputAfterSending()
	{
		// Arrange
		var viewModel = new AiAssistantViewModel(new NullAgentQueryClient());
		viewModel.InputText = "Test query";

		// Act
		await viewModel.SendQueryCommand.ExecuteAsync(null);

		// Assert
		Assert.Empty(viewModel.InputText);
	}

	[Fact]
	public void ToggleContextDisplay_TogglesVisibility()
	{
		// Arrange
		var viewModel = new AiAssistantViewModel();
		var initialState = viewModel.ShowContext;

		// Act
		viewModel.ToggleContextDisplayCommand.Execute(null);

		// Assert
		Assert.NotEqual(initialState, viewModel.ShowContext);
	}

	[Fact]
	public void ClearHistory_RemovesAllMessages()
	{
		// Arrange
		var viewModel = new AiAssistantViewModel();
		viewModel.Messages.Add(new AiMessageViewModel { Role = "User", Text = "Test" });
		viewModel.Messages.Add(new AiMessageViewModel { Role = "Assistant", Text = "Response" });

		// Act
		viewModel.ClearHistoryCommand.Execute(null);

		// Assert
		Assert.Empty(viewModel.Messages);
	}
}

/// <summary>
/// Tests for SettingsViewModel configuration management.
/// </summary>
public class AtlasSettingsViewModelTests
{
	[Fact]
	public void Constructor_InitializesDefaults()
	{
		// Arrange & Act
		var viewModel = new SettingsViewModel(new NullAgentConfigClient());

		// Assert
		Assert.Equal("Ollama", viewModel.LlmProvider);
		Assert.Equal(5000, viewModel.LlmTimeoutMs);
		Assert.Equal(2000, viewModel.LlmDiscoveryTimeoutMs);
		Assert.Equal(11434, viewModel.LlmDiscoveryPort);
		Assert.Equal(50, viewModel.LlmDiscoveryMaxConcurrency);
	}

	[Fact]
	public void LlmProviders_ContainsAllOptions()
	{
		// Arrange & Act
		var viewModel = new SettingsViewModel();

		// Assert
		Assert.Equal(3, viewModel.LlmProviders.Count);
		Assert.Contains("Ollama", viewModel.LlmProviders);
		Assert.Contains("OpenAI", viewModel.LlmProviders);
		Assert.Contains("Anthropic", viewModel.LlmProviders);
	}

	[Fact]
	public async Task LoadSettings_UpdatesProperties()
	{
		// Arrange
		var client = new TestAgentConfigClient();
		var viewModel = new SettingsViewModel(client);

		// Act
		await viewModel.LoadSettingsCommand.ExecuteAsync(null);

		// Assert
		Assert.NotNull(viewModel.StatusMessage);
		Assert.Contains("loaded", viewModel.StatusMessage.ToLower());
	}

	private class TestAgentConfigClient : IAgentConfigClient
	{
		public Task<AgentConfigDto> GetConfigAsync(CancellationToken cancellationToken)
		{
			return Task.FromResult(new AgentConfigDto
			{
				EnableLLM = true,
				LLMProvider = "Ollama",
				LLMEndpoint = "http://localhost:11434",
				LLMModel = "llama3.2"
			});
		}

		public Task<AgentConfigDto> UpdateConfigAsync(AgentConfigUpdateDto update, CancellationToken cancellationToken)
		{
			return Task.FromResult(new AgentConfigDto());
		}

		public Task<IReadOnlyList<string>> DiscoverEndpointsAsync(CancellationToken cancellationToken)
		{
			return Task.FromResult<IReadOnlyList<string>>(new[] { "http://localhost:11434", "http://192.168.1.100:11434" });
		}

		public Task<AgentHealthDto> CheckHealthAsync(CancellationToken cancellationToken)
		{
			return Task.FromResult(new AgentHealthDto { LlmEnabled = true, LlmAvailable = true });
		}

		public Task<LlmTestDto> TestLlmAsync(CancellationToken cancellationToken)
		{
			return Task.FromResult(new LlmTestDto { LlmEnabled = true, Available = true });
		}
	}
}

/// <summary>
/// Tests for TelemetryBuffer circular buffer functionality.
/// </summary>
public class TelemetryBufferTests
{
	[Fact]
	public void Add_StoresSample()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		var sample = new TelemetrySampleDto
		{
			FuelLiters = 50.0,
			SpeedKph = 200.0,
			LapNumber = 1
		};

		// Act
		buffer.Add(sample);

		// Assert
		Assert.Equal(1, buffer.Count);
	}

	[Fact]
	public void GetLatest_ReturnsLastAddedSample()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		var sample1 = new TelemetrySampleDto { FuelLiters = 50.0, LapNumber = 1 };
		var sample2 = new TelemetrySampleDto { FuelLiters = 49.5, LapNumber = 1 };
		buffer.Add(sample1);
		buffer.Add(sample2);

		// Act
		var latest = buffer.GetLatest();

		// Assert
		Assert.NotNull(latest);
		Assert.Equal(49.5, latest.FuelLiters);
	}

	[Fact]
	public void GetLapData_ReturnsOnlySpecifiedLap()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		buffer.Add(new TelemetrySampleDto { LapNumber = 1, FuelLiters = 50.0 });
		buffer.Add(new TelemetrySampleDto { LapNumber = 1, FuelLiters = 49.5 });
		buffer.Add(new TelemetrySampleDto { LapNumber = 2, FuelLiters = 49.0 });
		buffer.Add(new TelemetrySampleDto { LapNumber = 2, FuelLiters = 48.5 });

		// Act
		var lap1Data = buffer.GetLapData(1);
		var lap2Data = buffer.GetLapData(2);

		// Assert
		Assert.Equal(2, lap1Data.Length);
		Assert.Equal(2, lap2Data.Length);
		Assert.All(lap1Data, s => Assert.Equal(1, s.LapNumber));
		Assert.All(lap2Data, s => Assert.Equal(2, s.LapNumber));
	}

	[Fact]
	public void GetAvailableLaps_ReturnsDistinctLapNumbers()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		buffer.Add(new TelemetrySampleDto { LapNumber = 1 });
		buffer.Add(new TelemetrySampleDto { LapNumber = 1 });
		buffer.Add(new TelemetrySampleDto { LapNumber = 2 });
		buffer.Add(new TelemetrySampleDto { LapNumber = 3 });
		buffer.Add(new TelemetrySampleDto { LapNumber = 3 });

		// Act
		var laps = buffer.GetAvailableLaps();

		// Assert
		Assert.Equal(3, laps.Length);
		Assert.Contains(1, laps);
		Assert.Contains(2, laps);
		Assert.Contains(3, laps);
	}

	[Fact]
	public void CircularBuffer_WrapsAround()
	{
		// Arrange
		var buffer = new TelemetryBuffer(3); // Small buffer for testing

		// Act
		buffer.Add(new TelemetrySampleDto { FuelLiters = 50.0 });
		buffer.Add(new TelemetrySampleDto { FuelLiters = 49.0 });
		buffer.Add(new TelemetrySampleDto { FuelLiters = 48.0 });
		buffer.Add(new TelemetrySampleDto { FuelLiters = 47.0 }); // Should overwrite first

		// Assert
		Assert.Equal(3, buffer.Count); // Still only 3 items
		var latest = buffer.GetLatest();
		Assert.Equal(47.0, latest!.FuelLiters);
	}

	[Fact]
	public void Clear_RemovesAllSamples()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		buffer.Add(new TelemetrySampleDto { FuelLiters = 50.0 });
		buffer.Add(new TelemetrySampleDto { FuelLiters = 49.0 });

		// Act
		buffer.Clear();

		// Assert
		Assert.Equal(0, buffer.Count);
		Assert.Null(buffer.GetLatest());
	}

	#region GetLapFraction Tests

	[Fact]
	public void GetLapFraction_ReturnsNull_WhenLapNumberNegative()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		var sample = CreateSampleWithLap(0, 100, -1);
		buffer.Add(sample);

		// Act
		var fraction = buffer.GetLapFraction(-1, sample);

		// Assert
		Assert.Null(fraction);
	}

	[Fact]
	public void GetLapFraction_ReturnsNull_WhenBufferHasLessThanTwoSamples()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		var sample = CreateSampleWithLap(0, 100, 1);
		buffer.Add(sample);

		// Act
		var fraction = buffer.GetLapFraction(1, sample);

		// Assert
		Assert.Null(fraction);
	}

	[Fact]
	public void GetLapFraction_CalculatesProgressBasedOnPreviousLapLength()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);

		// Complete lap 1 with 4 samples
		for (int i = 0; i < 4; i++)
		{
			buffer.Add(CreateSampleWithLap(i, 100, 1));
		}

		// Lap 2 - test progress
		buffer.Add(CreateSampleWithLap(4, 100, 2));  // Start of lap 2
		buffer.Add(CreateSampleWithLap(5, 100, 2));  // 1/3 through
		buffer.Add(CreateSampleWithLap(6, 100, 2));  // 2/3 through

		// Act
		var sample = buffer.GetAll()[5];  // Second sample of lap 2
		var fraction = buffer.GetLapFraction(2, sample);

		// Assert
		Assert.NotNull(fraction);
		Assert.True(fraction.Value >= 0.0 && fraction.Value <= 1.0);
		Assert.True(fraction.Value > 0.0);  // Should be > 0 as it's not at start
		Assert.True(fraction.Value < 1.0);  // Should be < 1 as it's not at end
	}

	[Fact]
	public void GetLapFraction_UsesTimestampsWhenPreviousLapTooShort()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		var baseTime = DateTime.UtcNow;

		// Previous lap (lap 1) with only 1 sample - too short
		buffer.Add(CreateSampleWithTimestamp(0, 100, 1, baseTime));

		// Current lap (lap 2) 
		buffer.Add(CreateSampleWithTimestamp(1, 100, 2, baseTime.AddSeconds(0)));
		buffer.Add(CreateSampleWithTimestamp(2, 100, 2, baseTime.AddSeconds(30)));
		buffer.Add(CreateSampleWithTimestamp(3, 100, 2, baseTime.AddSeconds(60)));

		// Act
		var sample = buffer.GetAll()[2]; // 30 seconds into lap 2
		var fraction = buffer.GetLapFraction(2, sample);

		// Assert
		Assert.NotNull(fraction);
		// Default lap duration is 90 seconds, so 30/90 = 0.333...
		Assert.True(fraction.Value >= 0.3 && fraction.Value <= 0.4);
	}

	[Fact]
	public void GetLapFraction_ClampsBetweenZeroAndOne()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);

		// Lap 1 with samples
		for (int i = 0; i < 3; i++)
		{
			buffer.Add(CreateSampleWithLap(i, 100, 1));
		}

		// Lap 2 at start
		buffer.Add(CreateSampleWithLap(3, 100, 2));

		// Act
		var firstSampleLap2 = buffer.GetAll()[3];
		var fraction = buffer.GetLapFraction(2, firstSampleLap2);

		// Assert
		Assert.NotNull(fraction);
		Assert.InRange(fraction.Value, 0.0, 1.0);
	}

	[Fact]
	public void GetLapFraction_HandlesTimestampBasedCalculation()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		var baseTime = DateTime.UtcNow;

		// Complete lap 1 with 3 samples over 90 seconds
		buffer.Add(CreateSampleWithTimestamp(0, 100, 1, baseTime));
		buffer.Add(CreateSampleWithTimestamp(1, 100, 1, baseTime.AddSeconds(45)));
		buffer.Add(CreateSampleWithTimestamp(2, 100, 1, baseTime.AddSeconds(90)));

		// Lap 2 - 45 seconds in (should be ~0.5)
		buffer.Add(CreateSampleWithTimestamp(3, 100, 2, baseTime.AddSeconds(90)));
		buffer.Add(CreateSampleWithTimestamp(4, 100, 2, baseTime.AddSeconds(135)));

		// Act
		var sample = buffer.GetAll()[4]; // 45 seconds into lap 2
		var fraction = buffer.GetLapFraction(2, sample);

		// Assert
		Assert.NotNull(fraction);
		Assert.True(fraction.Value >= 0.45 && fraction.Value <= 0.55);
	}

	[Fact]
	public void GetLapFraction_HandlesSampleNotFoundInBuffer()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);

		for (int i = 0; i < 5; i++)
		{
			buffer.Add(CreateSampleWithLap(i, 100, 1));
		}

		// Act - Sample not in buffer
		var externalSample = CreateSampleWithLap(99, 100, 1);
		var fraction = buffer.GetLapFraction(1, externalSample);

		// Assert - Should still return a value based on last sample
		Assert.NotNull(fraction);
	}

	[Fact]
	public void GetLapFraction_HandlesEmptyBuffer()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		var sample = CreateSampleWithLap(0, 100, 1);

		// Act
		var fraction = buffer.GetLapFraction(1, sample);

		// Assert
		Assert.Null(fraction);
	}

	[Fact]
	public void GetLapFraction_HandlesSampleMatchByTimestamp()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		var baseTime = DateTime.UtcNow;

		// Previous lap
		buffer.Add(CreateSampleWithTimestamp(0, 100, 1, baseTime));
		buffer.Add(CreateSampleWithTimestamp(1, 100, 1, baseTime.AddSeconds(90)));

		// Current lap
		buffer.Add(CreateSampleWithTimestamp(2, 100, 2, baseTime.AddSeconds(90)));
		buffer.Add(CreateSampleWithTimestamp(3, 100, 2, baseTime.AddSeconds(135)));

		// Act - Create a new sample object with same timestamp as one in buffer
		var sampleWithMatchingTimestamp = CreateSampleWithTimestamp(99, 100, 2, baseTime.AddSeconds(135));
		var fraction = buffer.GetLapFraction(2, sampleWithMatchingTimestamp);

		// Assert
		Assert.NotNull(fraction);
		Assert.InRange(fraction.Value, 0.0, 1.0);
	}

	#endregion

	#region FindPreviousLapDurationSeconds Tests (via GetLapFraction)

	[Fact]
	public void FindPreviousLapDurationSeconds_CalculatesCorrectDuration()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		var baseTime = DateTime.UtcNow;

		// Lap 1: 0 to 80 seconds (80 second lap)
		buffer.Add(CreateSampleWithTimestamp(0, 100, 1, baseTime));
		buffer.Add(CreateSampleWithTimestamp(1, 100, 1, baseTime.AddSeconds(40)));
		buffer.Add(CreateSampleWithTimestamp(2, 100, 1, baseTime.AddSeconds(80)));

		// Lap 2: start at 80 seconds
		buffer.Add(CreateSampleWithTimestamp(3, 100, 2, baseTime.AddSeconds(80)));
		buffer.Add(CreateSampleWithTimestamp(4, 100, 2, baseTime.AddSeconds(120)));

		// Act
		var sample = buffer.GetAll()[4];
		var fraction = buffer.GetLapFraction(2, sample);

		// Assert
		// Previous lap was 80 seconds, we're 40 seconds into lap 2
		// So fraction should be 40/80 = 0.5
		Assert.NotNull(fraction);
		Assert.True(fraction.Value >= 0.45 && fraction.Value <= 0.55);
	}

	[Fact]
	public void FindPreviousLapDurationSeconds_ReturnsZero_WhenAtFirstLap()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		var baseTime = DateTime.UtcNow;

		// Only lap 1 samples
		buffer.Add(CreateSampleWithTimestamp(0, 100, 1, baseTime));
		buffer.Add(CreateSampleWithTimestamp(1, 100, 1, baseTime.AddSeconds(30)));

		// Act
		var sample = buffer.GetAll()[1];
		var fraction = buffer.GetLapFraction(1, sample);

		// Assert - When on first lap, there's no previous lap to calculate from
		// Should still return a fraction using fallback methods
		Assert.NotNull(fraction);
	}

	[Fact]
	public void FindPreviousLapDurationSeconds_HandlesNoTimestamps()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);

		// Lap 1 without timestamps
		buffer.Add(CreateSampleWithLap(0, 100, 1));

		// Lap 2 without timestamps
		buffer.Add(CreateSampleWithLap(1, 100, 2));
		buffer.Add(CreateSampleWithLap(2, 100, 2));

		// Act
		var sample = buffer.GetAll()[2];
		var fraction = buffer.GetLapFraction(2, sample);

		// Assert - Should fall back to sample-based calculation
		Assert.NotNull(fraction);
	}

	#endregion

	#region FindNextLapDurationSeconds Tests

	[Fact]
	public void FindNextLapDurationSeconds_WorksForMultipleLapTransitions()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);
		var baseTime = DateTime.UtcNow;

		// Lap 1: 100 seconds
		buffer.Add(CreateSampleWithTimestamp(0, 100, 1, baseTime));
		buffer.Add(CreateSampleWithTimestamp(1, 100, 1, baseTime.AddSeconds(50)));
		buffer.Add(CreateSampleWithTimestamp(2, 100, 1, baseTime.AddSeconds(100)));

		// Lap 2: 80 seconds - uses lap 1's duration for estimation
		buffer.Add(CreateSampleWithTimestamp(3, 100, 2, baseTime.AddSeconds(100)));
		buffer.Add(CreateSampleWithTimestamp(4, 100, 2, baseTime.AddSeconds(140)));
		buffer.Add(CreateSampleWithTimestamp(5, 100, 2, baseTime.AddSeconds(180)));

		// Lap 3: should use lap 2's duration (80 seconds)
		buffer.Add(CreateSampleWithTimestamp(6, 100, 3, baseTime.AddSeconds(180)));
		buffer.Add(CreateSampleWithTimestamp(7, 100, 3, baseTime.AddSeconds(220)));

		// Act
		var sample = buffer.GetAll()[7]; // 40 seconds into lap 3
		var fraction = buffer.GetLapFraction(3, sample);

		// Assert
		Assert.NotNull(fraction);
		// 40 / 80 = 0.5
		Assert.True(fraction.Value >= 0.45 && fraction.Value <= 0.55, $"Expected fraction between 0.45 and 0.55, got {fraction.Value}");
	}

	#endregion

	#region Additional Coverage Tests

	[Fact]
	public void ReplaceAll_ThrowsArgumentNullException_WhenSamplesNull()
	{
		// Arrange
		var buffer = new TelemetryBuffer();

		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => buffer.ReplaceAll(null!));
	}

	[Fact]
	public void ReplaceAll_ReplacesExistingSamples()
	{
		// Arrange
		var buffer = new TelemetryBuffer(10);
		buffer.Add(CreateSampleWithLap(0, 100, 1));

		var newSamples = new[] { 
			CreateSampleWithLap(1, 200, 2), 
			CreateSampleWithLap(2, 300, 2) 
		};

		// Act
		buffer.ReplaceAll(newSamples);

		// Assert
		Assert.Equal(2, buffer.Count);
		var all = buffer.GetAll();
		Assert.Equal(200, all[0].SpeedKph);
		Assert.Equal(300, all[1].SpeedKph);
	}

	[Fact]
	public void ReplaceAll_ExpandsCapacity_WhenNeeded()
	{
		// Arrange
		var buffer = new TelemetryBuffer(2);
		var samples = Enumerable.Range(0, 10)
			.Select(i => CreateSampleWithLap(i, 100 + i, 1))
			.ToArray();

		// Act
		buffer.ReplaceAll(samples);

		// Assert
		Assert.Equal(10, buffer.Count);
	}

	[Fact]
	public void GetRange_ReturnsCorrectSubset()
	{
		// Arrange
		var buffer = new TelemetryBuffer(10);
		buffer.Add(CreateSampleWithLap(0, 100, 1));
		buffer.Add(CreateSampleWithLap(1, 200, 1));
		buffer.Add(CreateSampleWithLap(2, 300, 1));
		buffer.Add(CreateSampleWithLap(3, 400, 1));

		// Act
		var range = buffer.GetRange(1, 2);

		// Assert
		Assert.Equal(2, range.Length);
		Assert.Equal(200, range[0].SpeedKph);
		Assert.Equal(300, range[1].SpeedKph);
	}

	[Fact]
	public void GetRange_ReturnsEmpty_WhenStartIndexInvalid()
	{
		// Arrange
		var buffer = new TelemetryBuffer(10);
		buffer.Add(CreateSampleWithLap(0, 100, 1));

		// Act & Assert
		var range = buffer.GetRange(-1, 1);
		Assert.Empty(range);

		range = buffer.GetRange(10, 1);
		Assert.Empty(range);
	}

	[Fact]
	public void GetRange_TruncatesCount_WhenExceedingAvailable()
	{
		// Arrange
		var buffer = new TelemetryBuffer(10);
		buffer.Add(CreateSampleWithLap(0, 100, 1));
		buffer.Add(CreateSampleWithLap(1, 200, 1));
		buffer.Add(CreateSampleWithLap(2, 300, 1));

		// Act
		var range = buffer.GetRange(1, 10);

		// Assert
		Assert.Equal(2, range.Length);
	}

	[Fact]
	public void GetValueAtIndex_ReturnsAllMetrics()
	{
		// Arrange
		var buffer = new TelemetryBuffer(10);
		var sample = new TelemetrySampleDto
		{
			SpeedKph = 150,
			FuelLiters = 25.5,
			ThrottlePosition = 0.8,
			BrakePosition = 0.2,
			SteeringAngle = 15.5,
			TyreTempsC = new[] { 90.0, 92.0, 88.0, 91.0 }
		};
		buffer.Add(sample);

		// Act
		var values = buffer.GetValueAtIndex(0);

		// Assert
		Assert.Equal(150, values["Speed"]);
		Assert.Equal(25.5, values["Fuel"]);
		Assert.Equal(80, values["Throttle"]);
		Assert.Equal(20, values["Brake"]);
		Assert.Equal(15.5, values["Steering"]);
		Assert.Equal(90, values["TyreFL"]);
		Assert.Equal(92, values["TyreFR"]);
		Assert.Equal(88, values["TyreRL"]);
		Assert.Equal(91, values["TyreRR"]);
	}

	[Fact]
	public void GetValueAtIndex_ReturnsEmpty_WhenIndexInvalid()
	{
		// Arrange
		var buffer = new TelemetryBuffer(10);
		buffer.Add(CreateSampleWithLap(0, 100, 1));

		// Act & Assert
		var values = buffer.GetValueAtIndex(-1);
		Assert.Empty(values);

		values = buffer.GetValueAtIndex(10);
		Assert.Empty(values);
	}

	[Fact]
	public void GetValueAtIndex_HandlesEmptyTyreArray()
	{
		// Arrange
		var buffer = new TelemetryBuffer(10);
		var sample = new TelemetrySampleDto
		{
			SpeedKph = 150,
			TyreTempsC = Array.Empty<double>()
		};
		buffer.Add(sample);

		// Act
		var values = buffer.GetValueAtIndex(0);

		// Assert
		Assert.Equal(0, values["TyreFL"]);
		Assert.Equal(0, values["TyreFR"]);
		Assert.Equal(0, values["TyreRL"]);
		Assert.Equal(0, values["TyreRR"]);
	}

	[Fact]
	public void GetAll_ReturnsEmpty_WhenBufferEmpty()
	{
		// Arrange
		var buffer = new TelemetryBuffer(10);

		// Act
		var all = buffer.GetAll();

		// Assert
		Assert.Empty(all);
	}

	[Fact]
	public void GetAll_ReturnsCorrectOrder_WhenBufferWrapped()
	{
		// Arrange
		var buffer = new TelemetryBuffer(3);
		buffer.Add(CreateSampleWithLap(0, 100, 1));
		buffer.Add(CreateSampleWithLap(1, 200, 1));
		buffer.Add(CreateSampleWithLap(2, 300, 1));
		buffer.Add(CreateSampleWithLap(3, 400, 1));
		buffer.Add(CreateSampleWithLap(4, 500, 1));

		// Act
		var all = buffer.GetAll();

		// Assert
		Assert.Equal(3, all.Length);
		Assert.Equal(300, all[0].SpeedKph);
		Assert.Equal(400, all[1].SpeedKph);
		Assert.Equal(500, all[2].SpeedKph);
	}

	[Fact]
	public void GetAvailableLaps_ExcludesNegativeLapNumbers()
	{
		// Arrange
		var buffer = new TelemetryBuffer(10);
		buffer.Add(CreateSampleWithLap(0, 100, -1));
		buffer.Add(CreateSampleWithLap(1, 200, 0));
		buffer.Add(CreateSampleWithLap(2, 300, 1));

		// Act
		var laps = buffer.GetAvailableLaps();

		// Assert
		Assert.Equal(2, laps.Length);
		Assert.Equal(new[] { 0, 1 }, laps);
	}

	[Fact]
	public void GetLapData_ReturnsEmpty_WhenLapNotFound()
	{
		// Arrange
		var buffer = new TelemetryBuffer(10);
		buffer.Add(CreateSampleWithLap(0, 100, 1));

		// Act
		var data = buffer.GetLapData(99);

		// Assert
		Assert.Empty(data);
	}

	[Fact]
	public void GetLatest_ReturnsNull_WhenEmpty()
	{
		// Arrange
		var buffer = new TelemetryBuffer();

		// Act & Assert
		Assert.Null(buffer.GetLatest());
	}

	[Fact]
	public void Constructor_InitializesWithDefaultCapacity()
	{
		// Arrange & Act
		var buffer = new TelemetryBuffer();

		// Assert
		Assert.Equal(0, buffer.Count);
	}

	[Fact]
	public void Constructor_InitializesWithCustomCapacity()
	{
		// Arrange & Act
		var buffer = new TelemetryBuffer(100);

		// Assert
		Assert.Equal(0, buffer.Count);
	}

	[Fact]
	public void GetLapFraction_HandlesLapWithOneSample()
	{
		// Arrange
		var buffer = new TelemetryBuffer(100);

		// Previous lap with multiple samples
		for (int i = 0; i < 4; i++)
		{
			buffer.Add(CreateSampleWithLap(i, 100, 1));
		}

		// Current lap with just one sample
		buffer.Add(CreateSampleWithLap(4, 100, 2));

		// Act
		var sample = buffer.GetAll()[4];
		var fraction = buffer.GetLapFraction(2, sample);

		// Assert
		Assert.NotNull(fraction);
		Assert.Equal(0.0, fraction.Value, 3); // At start of lap
	}

	#endregion

	#region Helper Methods

	private static TelemetrySampleDto CreateSampleWithLap(int id, double speed, int lapNumber)
	{
		return new TelemetrySampleDto
		{
			SpeedKph = speed,
			FuelLiters = 50,
			TyreTempsC = new[] { 80.0, 80.0, 80.0, 80.0 },
			ThrottlePosition = 0.5,
			BrakePosition = 0,
			SteeringAngle = 0,
			LapNumber = lapNumber
		};
	}

	private static TelemetrySampleDto CreateSampleWithTimestamp(int id, double speed, int lapNumber, DateTime timestamp)
	{
		return new TelemetrySampleDto
		{
			Timestamp = timestamp,
			SpeedKph = speed,
			FuelLiters = 50,
			TyreTempsC = new[] { 80.0, 80.0, 80.0, 80.0 },
			ThrottlePosition = 0.5,
			BrakePosition = 0,
			SteeringAngle = 0,
			LapNumber = lapNumber
		};
	}

	#endregion
}

/// <summary>
/// Null client for testing without dependencies.
/// </summary>
internal class NullAgentQueryClient : IAgentQueryClient
{
	public Task<AgentResponseDto> SendQueryAsync(string query, CancellationToken cancellationToken)
	{
		return Task.FromResult(new AgentResponseDto
		{
			Answer = "Test response",
			Source = "Test",
			Success = true
		});
	}
}

/// <summary>
/// Null config client for testing without dependencies.
/// </summary>
internal class NullAgentConfigClient : IAgentConfigClient
{
	public Task<AgentConfigDto> GetConfigAsync(CancellationToken cancellationToken)
	{
		return Task.FromResult(new AgentConfigDto
		{
			EnableLLM = false,
			LLMProvider = "Ollama",
			LLMTimeoutMs = 5000,
			RequirePitForLlm = false,
			EnableLLMDiscovery = false,
			LLMDiscoveryTimeoutMs = 2000,
			LLMDiscoveryPort = 11434,
			LLMDiscoveryMaxConcurrency = 50,
			LLMDiscoverySubnetPrefix = "192.168.1"
		});
	}

	public Task<AgentConfigDto> UpdateConfigAsync(AgentConfigUpdateDto update, CancellationToken cancellationToken)
	{
		return Task.FromResult(new AgentConfigDto());
	}

	public Task<IReadOnlyList<string>> DiscoverEndpointsAsync(CancellationToken cancellationToken)
	{
		return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
	}

	public Task<AgentHealthDto> CheckHealthAsync(CancellationToken cancellationToken)
	{
		return Task.FromResult(new AgentHealthDto());
	}

	public Task<LlmTestDto> TestLlmAsync(CancellationToken cancellationToken)
	{
		return Task.FromResult(new LlmTestDto());
	}
}
