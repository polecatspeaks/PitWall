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
	private int optimalPitLapStart = 16;

	[ObservableProperty]
	private int optimalPitLapEnd = 20;

	[ObservableProperty]
	private int nextPitLap = 18;

	[ObservableProperty]
	private double strategyConfidence = 85.0;

	[ObservableProperty]
	private string recommendedAction = "Continue current stint";

	[ObservableProperty]
	private bool fuelSaveModeActive;

	[ObservableProperty]
	private int totalRaceLaps = 30;

	[ObservableProperty]
	private int currentLap = 1;

	public ObservableCollection<StrategyAlternative> AlternativeStrategies { get; } = new();
	public ObservableCollection<PitStopMarker> PitStopMarkers { get; } = new();
	public ObservableCollection<CompetitorStrategy> CompetitorStrategies { get; } = new();

	public StrategyViewModel()
	{
		// Add sample alternative strategies
		AlternativeStrategies.Add(new StrategyAlternative
		{
			Name = "2-Stop Aggressive",
			Description = "Early pit on L12, final on L22",
			Confidence = 78.5,
			EstimatedFinishPosition = 3
		});

		AlternativeStrategies.Add(new StrategyAlternative
		{
			Name = "1-Stop Conservative",
			Description = "Single pit on L18, fuel-save mode",
			Confidence = 82.0,
			EstimatedFinishPosition = 4
		});
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
		RecommendedAction = recommendation.Recommendation ?? string.Empty;
		StrategyConfidence = recommendation.Confidence;
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
