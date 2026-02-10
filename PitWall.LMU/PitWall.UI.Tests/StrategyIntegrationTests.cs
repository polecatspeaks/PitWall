using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using PitWall.UI.ViewModels;
using PitWall.UI.Services;
using PitWall.UI.Models;
using PitWall.Core.Services;

namespace PitWall.UI.Tests;

/// <summary>
/// Integration tests demonstrating Strategy Dashboard with real LMU telemetry data.
/// These tests use the actual lmu_telemetry.db (4.6GB, 764M+ rows, 229 sessions).
/// </summary>
public class StrategyIntegrationTests
{
	private const string DatabasePath = @"c:\Users\ohzee\.claude-worktrees\PitWall\vibrant-meninsky\PitWall.LMU\lmu_telemetry.db";

	[Fact(Skip = "Integration test - requires lmu_telemetry.db")]
	public async Task StrategyDashboard_WithRealTelemetry_CalculatesPitWindow()
	{
		// Skip if database doesn't exist
		if (!File.Exists(DatabasePath))
		{
			return;
		}

		// Arrange - Load real telemetry from LMU database
		var reader = new LmuTelemetryReader(DatabasePath, fallbackSessionCount: 229);
		var buffer = new TelemetryBuffer(10000);
		var viewModel = new StrategyViewModel();

		// Act - Load first 1000 samples from the database
		var samples = new List<PitWall.Core.Models.TelemetrySample>();
		await foreach (var sample in reader.ReadSamplesAsync(
			sessionId: 1,
			startRow: 0,
			endRow: 999,
			CancellationToken.None))
		{
			samples.Add(sample);
		}

		// Convert to TelemetrySampleDto and add to buffer
		int lapNumber = 1;
		foreach (var sample in samples)
		{
			var dto = new TelemetrySampleDto
			{
				LapNumber = lapNumber,
				SpeedKph = sample.SpeedKph,
				ThrottlePosition = sample.Throttle,
				BrakePosition = sample.Brake,
				SteeringAngle = sample.Steering,
				FuelLiters = sample.FuelLiters,
				TyreTempsC = sample.TyreTempsC
			};
			buffer.Add(dto);

			// Simulate lap progression (100 samples per lap @ 100Hz = ~1 second per lap for testing)
			if (buffer.Count % 100 == 0)
			{
				lapNumber++;
			}
		}

		// Calculate fuel consumption rate from real data
		var allSamples = buffer.GetAll();
		var firstFuel = allSamples.First().FuelLiters;
		var lastFuel = allSamples.Last().FuelLiters;
		var fuelConsumed = firstFuel - lastFuel;
		var lapsCompleted = lapNumber - 1;
		var fuelPerLap = lapsCompleted > 0 ? fuelConsumed / lapsCompleted : 3.0;

		// Simulate current stint status
		var currentLap = 5;
		var fuelPct = (lastFuel / firstFuel) * 100.0; // Calculate fuel percentage
		var tireWear = 25.0; // Simulated tire wear for demo
		var stintLaps = 5;

		// Update ViewModel with real data
		viewModel.UpdateStintStatus(fuelPct, tireWear, currentLap, stintLaps);

		// Calculate pit window using real fuel consumption rate
		var tankCapacity = firstFuel > 0 ? firstFuel : 60.0; // Use actual starting fuel as tank capacity
		var tireWearPerLap = 5.0; // Estimated tire wear rate
		viewModel.CalculatePitWindow(fuelPerLap, tireWearPerLap, tankCapacity);

		// Assert - Verify calculations are reasonable
		Assert.True(samples.Count > 0, "Should load telemetry samples from database");
		Assert.True(buffer.Count > 0, "TelemetryBuffer should contain samples");
		Assert.True(fuelPerLap >= 0, "Fuel consumption rate should be non-negative");
		Assert.True(viewModel.OptimalPitLapStart > currentLap, "Optimal pit lap should be in the future");
		Assert.True(viewModel.OptimalPitLapEnd >= viewModel.OptimalPitLapStart, "Pit window end should be after or equal to start");
		Assert.True(viewModel.NextPitLap >= viewModel.OptimalPitLapStart && viewModel.NextPitLap <= viewModel.OptimalPitLapEnd,
			"Next pit lap should be within the optimal window");

		// Output results for manual verification
		Console.WriteLine($"=== Strategy Dashboard Integration Test Results ===");
		Console.WriteLine($"Database: {DatabasePath}");
		Console.WriteLine($"Samples loaded: {samples.Count}");
		Console.WriteLine($"Buffer size: {buffer.Count}");
		Console.WriteLine($"Laps simulated: {lapsCompleted}");
		Console.WriteLine($"Starting fuel: {firstFuel:F2}L");
		Console.WriteLine($"Current fuel: {lastFuel:F2}L");
		Console.WriteLine($"Fuel consumed: {fuelConsumed:F2}L");
		Console.WriteLine($"Fuel per lap: {fuelPerLap:F2}L/lap");
		Console.WriteLine($"Current lap: {currentLap}");
		Console.WriteLine($"Fuel remaining: {fuelPct:F1}%");
		Console.WriteLine($"Tire wear: {tireWear:F1}%");
		Console.WriteLine($"Optimal pit window: Laps {viewModel.OptimalPitLapStart}-{viewModel.OptimalPitLapEnd}");
		Console.WriteLine($"Recommended pit lap: {viewModel.NextPitLap}");
		Console.WriteLine($"Strategy confidence: {viewModel.StrategyConfidence:F1}%");
		Console.WriteLine($"Recommended action: {viewModel.RecommendedAction}");
	}

	[Fact(Skip = "Integration test - requires lmu_telemetry.db")]
	public async Task StrategyDashboard_WithMultipleLaps_TracksStintProgression()
	{
		// Skip if database doesn't exist
		if (!File.Exists(DatabasePath))
		{
			return;
		}

		// Arrange
		var reader = new LmuTelemetryReader(DatabasePath, fallbackSessionCount: 229);
		var buffer = new TelemetryBuffer(10000);
		var viewModel = new StrategyViewModel();

		// Act - Load first 500 samples and track stint progression
		var samples = new List<PitWall.Core.Models.TelemetrySample>();
		await foreach (var sample in reader.ReadSamplesAsync(
			sessionId: 1,
			startRow: 0,
			endRow: 499,
			CancellationToken.None))
		{
			samples.Add(sample);
		}

		// Simulate 5 laps with 100 samples each
		for (int lap = 1; lap <= 5; lap++)
		{
			var lapStart = (lap - 1) * 100;
			var lapEnd = Math.Min(lap * 100, samples.Count);
			
			for (int i = lapStart; i < lapEnd && i < samples.Count; i++)
			{
				var sample = samples[i];
				var dto = new TelemetrySampleDto
				{
					LapNumber = lap,
					SpeedKph = sample.SpeedKph,
					FuelLiters = sample.FuelLiters,
					TyreTempsC = sample.TyreTempsC
				};
				buffer.Add(dto);
			}

			// Update stint status at end of each lap
			var lapData = buffer.GetLapData(lap);
			if (lapData.Length > 0)
			{
				var lastSample = lapData.Last();
				var firstSample = buffer.GetLapData(1).FirstOrDefault();
				var fuelPct = firstSample != null ? (lastSample.FuelLiters / firstSample.FuelLiters) * 100.0 : 100.0;
				var tireWear = lap * 5.0; // Simulated progressive tire wear
				
				viewModel.UpdateStintStatus(fuelPct, tireWear, lap, lap);

				Console.WriteLine($"Lap {lap}: Fuel {fuelPct:F1}%, Tire Wear {tireWear:F1}%");
			}
		}

		// Assert - Verify stint progression is tracked
		Assert.Equal(5, viewModel.CurrentLap);
		Assert.Equal(5, viewModel.StintLap);
		Assert.True(viewModel.CurrentStintFuelPercentage < 100.0, "Fuel should decrease over stint");
		Assert.True(viewModel.CurrentStintTireWear > 0, "Tire wear should accumulate over stint");

		// Verify available laps in buffer
		var availableLaps = buffer.GetAvailableLaps();
		Assert.Contains(1, availableLaps);
		Assert.Contains(5, availableLaps);

		Console.WriteLine($"=== Stint Progression Test Results ===");
		Console.WriteLine($"Laps completed: {viewModel.CurrentLap}");
		Console.WriteLine($"Stint laps: {viewModel.StintLap}");
		Console.WriteLine($"Final fuel: {viewModel.CurrentStintFuelPercentage:F1}%");
		Console.WriteLine($"Final tire wear: {viewModel.CurrentStintTireWear:F1}%");
		Console.WriteLine($"Available laps in buffer: {string.Join(", ", availableLaps)}");
	}

	[Fact]
	public void StrategyDashboard_WithSimulatedData_DemonstratesWorkflow()
	{
		// This test doesn't require the database - uses simulated data to demonstrate workflow

		// Arrange
		var buffer = new TelemetryBuffer(1000);
		var viewModel = new StrategyViewModel();

		// Simulate a stint: 10 laps with decreasing fuel and increasing tire wear
		for (int lap = 1; lap <= 10; lap++)
		{
			// Generate 50 samples per lap
			for (int i = 0; i < 50; i++)
			{
				var sample = new TelemetrySampleDto
				{
					LapNumber = lap,
					SpeedKph = 200.0 + (lap * 2.0), // Slightly increasing speed
					FuelLiters = 60.0 - (lap * 3.0), // Consuming 3L per lap
					ThrottlePosition = 0.80,
					TyreTempsC = new[] { 85.0 + lap, 87.0 + lap, 83.0 + lap, 86.0 + lap }
				};
				buffer.Add(sample);
			}

			// Update stint status after each lap
			var fuelPct = ((60.0 - (lap * 3.0)) / 60.0) * 100.0;
			var tireWear = lap * 4.0; // 4% wear per lap
			viewModel.UpdateStintStatus(fuelPct, tireWear, lap, lap);
		}

		// Calculate pit window based on consumption rates
		viewModel.CalculatePitWindow(fuelPerLap: 3.0, tireWearPerLap: 4.0, tankCapacity: 60.0);

		// Assert - Verify the workflow produces expected results
		Assert.Equal(10, viewModel.CurrentLap);
		Assert.Equal(50.0, viewModel.CurrentStintFuelPercentage); // 50% fuel remaining
		Assert.Equal(40.0, viewModel.CurrentStintTireWear);       // 40% tire wear
		Assert.Equal(500, buffer.Count); // 10 laps × 50 samples
		Assert.Equal(10, buffer.GetAvailableLaps().Length);

		// Fuel limiting: 50% of 60L = 30L at 3L/lap = 10 laps remaining
		// Tire limiting: 60% remaining at 4% wear/lap = 15 laps remaining
		// Min = 10 laps, so optimal pit window = Lap 10+10-2 to Lap 10+10 = Laps 18-20
		Assert.Equal(18, viewModel.OptimalPitLapStart);
		Assert.Equal(20, viewModel.OptimalPitLapEnd);
		Assert.Equal(19, viewModel.NextPitLap);

		Console.WriteLine($"=== Simulated Workflow Test Results ===");
		Console.WriteLine($"Lap {viewModel.CurrentLap}: Fuel {viewModel.CurrentStintFuelPercentage:F1}%, Tire Wear {viewModel.CurrentStintTireWear:F1}%");
		Console.WriteLine($"Optimal pit window: Laps {viewModel.OptimalPitLapStart}-{viewModel.OptimalPitLapEnd}");
		Console.WriteLine($"Recommended pit lap: {viewModel.NextPitLap}");
		Console.WriteLine($"Buffer contains {buffer.Count} samples across {buffer.GetAvailableLaps().Length} laps");
	}
}
