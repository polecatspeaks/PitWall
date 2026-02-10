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
public class TelemetryAnalysisViewModelTests
{
	[Fact]
	public void SelectReferenceLap_UpdatesSelectedLap()
	{
		// Arrange
		var viewModel = new TelemetryAnalysisViewModel();

		// Act
		viewModel.SelectReferenceLapCommand.Execute(5);

		// Assert
		Assert.Equal(5, viewModel.SelectedReferenceLap);
	}

	[Fact]
	public void DataCollections_InitiallyEmpty()
	{
		// Arrange & Act
		var viewModel = new TelemetryAnalysisViewModel();

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
		var viewModel = new TelemetryAnalysisViewModel();

		// Assert
		Assert.Empty(viewModel.AvailableLaps);
	}
}

/// <summary>
/// Tests for StrategyViewModel pit planning and alternative strategies.
/// </summary>
public class StrategyViewModelTests
{
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
}
