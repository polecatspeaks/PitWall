using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PitWall.UI.Models;

namespace PitWall.UI.ViewModels;

/// <summary>
/// ViewModel for strategy planning, pit window calculation,
/// and alternative strategy comparison.
/// </summary>
public partial class StrategyViewModel : ViewModelBase
{
	[ObservableProperty]
	private double currentStintFuelPercentage;

	[ObservableProperty]
	private double currentStintTireWear;

	[ObservableProperty]
	private int stintLap;

	[ObservableProperty]
	private int stintDuration = 15;

	[ObservableProperty]
	private int optimalPitLapStart;

	[ObservableProperty]
	private int optimalPitLapEnd;

	[ObservableProperty]
	private int nextPitLap;

	[ObservableProperty]
	private double strategyConfidence = 85.0;

	[ObservableProperty]
	private string recommendedAction = "Continue current stint";

	[ObservableProperty]
	private bool hasAlternativeStrategies;

	[ObservableProperty]
	private bool hasNoAlternativeStrategies = true;

	[ObservableProperty]
	private bool hasCompetitorStrategies;

	[ObservableProperty]
	private bool hasNoCompetitorStrategies = true;

	[ObservableProperty]
	private bool fuelSaveModeActive;

	[ObservableProperty]
	private int totalRaceLaps = 30;

	[ObservableProperty]
	private int currentLap = 1;

	[ObservableProperty]
	private bool hasTelemetryData;

	[ObservableProperty]
	private bool hasNoTelemetryData = true;

	public ObservableCollection<StrategyAlternative> AlternativeStrategies { get; } = new();
	public ObservableCollection<PitStopMarker> PitStopMarkers { get; } = new();
	public ObservableCollection<CompetitorStrategy> CompetitorStrategies { get; } = new();

	public StrategyViewModel()
	{
		AlternativeStrategies.CollectionChanged += (_, _) => RefreshStrategyCollections();
		CompetitorStrategies.CollectionChanged += (_, _) => RefreshStrategyCollections();

		var includeSampleData = string.Equals(
			Environment.GetEnvironmentVariable("PITWALL_SAMPLE_STRATEGY_DATA"),
			"1",
			StringComparison.OrdinalIgnoreCase);
		if (!includeSampleData)
		{
			RefreshStrategyCollections();
			return;
		}

		// Initialize with sample alternative strategies
		AlternativeStrategies.Add(new StrategyAlternative
		{
			Name = "2-Stop Aggressive",
			Description = "Two-stop strategy optimized for track position",
			Confidence = 78.5,
			EstimatedFinishPosition = 3
		});
		AlternativeStrategies.Add(new StrategyAlternative
		{
			Name = "1-Stop Conservative",
			Description = "Single-stop strategy with fuel saving",
			Confidence = 82.0,
			EstimatedFinishPosition = 5
		});

		// Initialize with sample pit stop markers
		PitStopMarkers.Add(new PitStopMarker
		{
			Lap = 18,
			FuelToAdd = 45.0,
			ChangeTires = true,
			EstimatedDuration = 25.5
		});

		// Initialize with sample competitor strategies
		CompetitorStrategies.Add(new CompetitorStrategy
		{
			Position = 2,
			CarNumber = "44",
			Driver = "Competitor A",
			LastPitLap = 12,
			EstimatedNextPit = 24,
			StrategyType = "2-Stop"
		});

		RefreshStrategyCollections();
	}

	[RelayCommand]
	private void OverrideStrategy()
	{
		// TODO: Open dialog to manually set pit lap
	}

	[RelayCommand]
	private void ToggleFuelSaveMode()
	{
		FuelSaveModeActive = !FuelSaveModeActive;
		// TODO: Send command to API
	}

	[RelayCommand]
	private void SelectAlternativeStrategy(StrategyAlternative strategy)
	{
		// TODO: Apply selected alternative strategy
	}

	public void UpdateFromRecommendation(RecommendationDto recommendation)
	{
		RecommendedAction = string.IsNullOrWhiteSpace(recommendation.Recommendation)
			? "Awaiting strategy..."
			: recommendation.Recommendation;
		StrategyConfidence = recommendation.Confidence;
	}

	private void RefreshStrategyCollections()
	{
		HasAlternativeStrategies = AlternativeStrategies.Count > 0;
		HasNoAlternativeStrategies = !HasAlternativeStrategies;
		HasCompetitorStrategies = CompetitorStrategies.Count > 0;
		HasNoCompetitorStrategies = !HasCompetitorStrategies;
	}

	/// <summary>
	/// Updates stint status from telemetry
	/// </summary>
	public void UpdateStintStatus(double fuelPct, double tireWear, int lap, int stintLaps)
	{
		HasTelemetryData = true;
		HasNoTelemetryData = false;
		CurrentStintFuelPercentage = fuelPct;
		CurrentStintTireWear = tireWear;
		CurrentLap = lap;
		StintLap = stintLaps;
		StintDuration = stintLaps;
	}

	/// <summary>
	/// Calculates optimal pit window based on fuel consumption and tire wear
	/// </summary>
	public void CalculatePitWindow(double fuelPerLap, double tireWearPerLap, double tankCapacity)
	{
		if (fuelPerLap <= 0 || tireWearPerLap <= 0 || tankCapacity <= 0)
		{
			return;
		}

		var fuelLapsRemaining = (int)((CurrentStintFuelPercentage / 100.0) * tankCapacity / fuelPerLap);
		var tireLapsRemaining = (int)((100.0 - CurrentStintTireWear) / tireWearPerLap);
		
		var maxStintLaps = Math.Min(fuelLapsRemaining, tireLapsRemaining);
		
		OptimalPitLapStart = CurrentLap + maxStintLaps - 2; // 2-lap buffer
		OptimalPitLapEnd = CurrentLap + maxStintLaps;
		NextPitLap = CurrentLap + maxStintLaps - 1; // Middle of window
	}
}

public class StrategyAlternative
{
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public double Confidence { get; set; }
	public int EstimatedFinishPosition { get; set; }
}

public class PitStopMarker
{
	public int Lap { get; set; }
	public double FuelToAdd { get; set; }
	public bool ChangeTires { get; set; }
	public double EstimatedDuration { get; set; }
}

public class CompetitorStrategy
{
	public int Position { get; set; }
	public string CarNumber { get; set; } = string.Empty;
	public string Driver { get; set; } = string.Empty;
	public int? LastPitLap { get; set; }
	public int? EstimatedNextPit { get; set; }
	public string StrategyType { get; set; } = "Unknown";
}
