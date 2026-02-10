using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using PitWall.UI.Models;

namespace PitWall.UI.ViewModels;

/// <summary>
/// ViewModel for the main dashboard view showing real-time telemetry,
/// fuel, tires, strategy recommendations, and timing information.
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
	[ObservableProperty]
	private string fuelLiters = "-- L";

	[ObservableProperty]
	private string fuelLaps = "-- LAPS";

	[ObservableProperty]
	private double fuelPercentage;

	[ObservableProperty]
	private string fuelConsumptionRate = "-- L/lap";

	[ObservableProperty]
	private string speedDisplay = "-- KPH";

	[ObservableProperty]
	private double tireFLWear;

	[ObservableProperty]
	private double tireFRWear;

	[ObservableProperty]
	private double tireRLWear;

	[ObservableProperty]
	private double tireRRWear;

	[ObservableProperty]
	private double tireFLTemp;

	[ObservableProperty]
	private double tireFRTemp;

	[ObservableProperty]
	private double tireRLTemp;

	[ObservableProperty]
	private double tireRRTemp;

	[ObservableProperty]
	private string strategyMessage = "Awaiting strategy...";

	[ObservableProperty]
	private double strategyConfidence;

	[ObservableProperty]
	private int nextPitLap;

	[ObservableProperty]
	private string lastLapTime = "--:--.---";

	[ObservableProperty]
	private string bestLapTime = "--:--.---";

	[ObservableProperty]
	private string lapTimeDelta = "+-.---";

	[ObservableProperty]
	private string weatherConditions = "CLEAR";

	[ObservableProperty]
	private int trackTempC;

	[ObservableProperty]
	private double gripPercentage = 100.0;

	[ObservableProperty]
	private int currentLap;

	[ObservableProperty]
	private int totalLaps = 30;

	[ObservableProperty]
	private int position = 1;

	[ObservableProperty]
	private string gapToLeader = "--";

	public ObservableCollection<string> Alerts { get; } = new();

	public void UpdateTelemetry(TelemetrySampleDto telemetry)
	{
		FuelLiters = $"{telemetry.FuelLiters:0.0} L";
		SpeedDisplay = $"{telemetry.SpeedKph:0.0} KPH";

		if (telemetry.TyreTempsC.Length >= 4)
		{
			TireFLTemp = telemetry.TyreTempsC[0];
			TireFRTemp = telemetry.TyreTempsC[1];
			TireRLTemp = telemetry.TyreTempsC[2];
			TireRRTemp = telemetry.TyreTempsC[3];
		}

		// TODO: Calculate fuel percentage when we have max capacity
		// FuelPercentage = telemetry.FuelLiters / maxCapacity * 100;
	}

	public void UpdateRecommendation(RecommendationDto recommendation)
	{
		StrategyMessage = string.IsNullOrWhiteSpace(recommendation.Recommendation)
			? "Awaiting strategy..."
			: recommendation.Recommendation;

		StrategyConfidence = recommendation.Confidence;

		// Parse next pit lap from recommendation message (simplified)
		// TODO: Get this from a structured response
	}
}
