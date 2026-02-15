using System;
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
	private string throttleDisplay = "--%";

	[ObservableProperty]
	private string brakeDisplay = "--%";

	[ObservableProperty]
	private string steeringDisplay = "--";

	[ObservableProperty]
	private double throttlePercent;

	[ObservableProperty]
	private double brakePercent;

	[ObservableProperty]
	private double steeringPercent;

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

	[ObservableProperty]
	private string trackName = "TRACK";

	[ObservableProperty]
	private string currentSector = "--";

	[ObservableProperty]
	private string currentCorner = "--";

	[ObservableProperty]
	private string currentSegment = "--";

	[ObservableProperty]
	private string carName = "CAR";

	[ObservableProperty]
	private string carClass = "--";

	[ObservableProperty]
	private string carPower = "--";

	[ObservableProperty]
	private string carWeight = "--";

	[ObservableProperty]
	private string carDimensions = "--";

	public ObservableCollection<string> Alerts { get; } = new();
	public ObservableCollection<RelativePositionEntry> RelativePositions { get; } = new();
	public ObservableCollection<StandingsEntry> Standings { get; } = new();

	public void UpdateTelemetry(TelemetrySampleDto telemetry)
	{
		FuelLiters = $"{telemetry.FuelLiters:0.0} L";
		SpeedDisplay = $"{telemetry.SpeedKph:0.0} KPH";

		var throttle = Math.Clamp(telemetry.ThrottlePosition, 0, 1);
		var brake = Math.Clamp(telemetry.BrakePosition, 0, 1);
		var steering = Math.Clamp(telemetry.SteeringAngle, -1, 1);

		ThrottlePercent = throttle * 100;
		BrakePercent = brake * 100;
		SteeringPercent = (steering + 1) * 50;

		ThrottleDisplay = $"{ThrottlePercent:0}%";
		BrakeDisplay = $"{BrakePercent:0}%";
		SteeringDisplay = $"{steering:0.00}";

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

	public void UpdateTrackContext(TrackSegmentStatus status)
	{
		TrackName = status.TrackName;
		CurrentSector = status.SectorName;
		CurrentCorner = status.CornerLabel;
		CurrentSegment = status.SegmentType;
	}

	public void UpdateCarSpec(CarSpec? spec, string? fallbackName)
	{
		CarName = !string.IsNullOrWhiteSpace(spec?.Name)
			? spec!.Name
			: (string.IsNullOrWhiteSpace(fallbackName) ? "CAR" : fallbackName!);
		CarClass = !string.IsNullOrWhiteSpace(spec?.Category) ? spec!.Category : "--";
		CarPower = !string.IsNullOrWhiteSpace(spec?.Power) ? spec!.Power : "--";
		CarWeight = spec?.WeightKg.HasValue == true ? $"{spec.WeightKg}kg" : "--";

		if (spec?.LengthMm.HasValue == true && spec?.WidthMm.HasValue == true && spec?.HeightMm.HasValue == true)
		{
			CarDimensions = $"L {spec.LengthMm}mm W {spec.WidthMm}mm H {spec.HeightMm}mm";
		}
		else
		{
			CarDimensions = "--";
		}
	}
}

public class RelativePositionEntry
{
	public string Relation { get; set; } = string.Empty;
	public int Position { get; set; }
	public string CarNumber { get; set; } = string.Empty;
	public string Driver { get; set; } = string.Empty;
	public string Gap { get; set; } = string.Empty;
	public string Class { get; set; } = string.Empty;
}

public class StandingsEntry
{
	public int Position { get; set; }
	public string Class { get; set; } = string.Empty;
	public string CarNumber { get; set; } = string.Empty;
	public string Driver { get; set; } = string.Empty;
	public string Gap { get; set; } = string.Empty;
	public int Laps { get; set; }
}
