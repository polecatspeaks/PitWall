using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PitWall.UI.Models;
using PitWall.UI.Services;

namespace PitWall.UI.ViewModels;

/// <summary>
/// ViewModel for telemetry analysis with waveform displays, lap comparison,
/// and historical data visualization using ScottPlot.
/// </summary>
public partial class TelemetryAnalysisViewModel : ViewModelBase
{
	private readonly TelemetryBuffer _telemetryBuffer;

	public event EventHandler? DataSeriesUpdated;

	[ObservableProperty]
	private int? selectedReferenceLap;

	[ObservableProperty]
	private int currentLap = 1;

	[ObservableProperty]
	private bool isPlayingBack;

	[ObservableProperty]
	private double cursorPosition;

	[ObservableProperty]
	private string statusMessage = "No telemetry data available";

	[ObservableProperty]
	private string trackName = "TRACK";

	[ObservableProperty]
	private string sectorLabel = "--";

	[ObservableProperty]
	private string cornerLabel = "--";

	[ObservableProperty]
	private string segmentType = "--";

	public ObservableCollection<int> AvailableLaps { get; } = new();
	public ObservableCollection<CursorDataRow> CursorData { get; } = new();

	// Current lap data series
	public ObservableCollection<TelemetryDataPoint> SpeedData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> ThrottleData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> BrakeData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> SteeringData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> TireTempData { get; } = new();

	// Reference lap data series (for comparison)
	public ObservableCollection<TelemetryDataPoint> ReferenceSpeedData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> ReferenceThrottleData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> ReferenceBrakeData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> ReferenceSteeringData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> ReferenceTireTempData { get; } = new();

	/// <summary>
	/// Constructor for runtime with dependency injection
	/// </summary>
	public TelemetryAnalysisViewModel(TelemetryBuffer telemetryBuffer)
	{
		_telemetryBuffer = telemetryBuffer;
		InitializeCursorDataTable();
	}

	/// <summary>
	/// Parameterless constructor for designer support
	/// </summary>
	public TelemetryAnalysisViewModel()
		: this(new TelemetryBuffer())
	{
	}

	public void UpdateTrackContext(TrackSegmentStatus status)
	{
		TrackName = status.TrackName;
		SectorLabel = status.SectorName;
		CornerLabel = status.CornerLabel;
		SegmentType = status.SegmentType;
	}

	private void InitializeCursorDataTable()
	{
		CursorData.Add(new CursorDataRow { Parameter = "vSpeed", Unit = "km/h", CurrentValue = "--", ReferenceValue = "--", Delta = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "nThrottle", Unit = "%", CurrentValue = "--", ReferenceValue = "--", Delta = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "nBrake", Unit = "%", CurrentValue = "--", ReferenceValue = "--", Delta = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "rSteer", Unit = "°", CurrentValue = "--", ReferenceValue = "--", Delta = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "tTyreCentre_FL", Unit = "°C", CurrentValue = "--", ReferenceValue = "--", Delta = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "tTyreCentre_FR", Unit = "°C", CurrentValue = "--", ReferenceValue = "--", Delta = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "tTyreCentre_RL", Unit = "°C", CurrentValue = "--", ReferenceValue = "--", Delta = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "tTyreCentre_RR", Unit = "°C", CurrentValue = "--", ReferenceValue = "--", Delta = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "fFuel", Unit = "L", CurrentValue = "--", ReferenceValue = "--", Delta = "--" });
	}

	[RelayCommand]
	private void SelectReferenceLap(int lapNumber)
	{
		if (lapNumber <= 0) return;
		
		SelectedReferenceLap = lapNumber;
	}

	partial void OnSelectedReferenceLapChanged(int? value)
	{
		if (!value.HasValue || value.Value <= 0)
		{
			ClearReferenceLapData();
			return;
		}

		LoadReferenceLapData();
	}

	[RelayCommand]
	private void PreviousSector()
	{
		// TODO: Implement sector-based navigation
		StatusMessage = "Sector navigation not yet implemented";
	}

	[RelayCommand]
	private void NextSector()
	{
		// TODO: Implement sector-based navigation
		StatusMessage = "Sector navigation not yet implemented";
	}

	[RelayCommand]
	private void ZoomIn()
	{
		// This will be handled by ScottPlot directly via user interaction
		StatusMessage = "Use mouse wheel or pinch gestures to zoom";
	}

	[RelayCommand]
	private void ZoomOut()
	{
		// This will be handled by ScottPlot directly via user interaction
		StatusMessage = "Use mouse wheel or pinch gestures to zoom";
	}

	[RelayCommand]
	private async Task ExportToCsv()
	{
		await ExportToCsvAsync();
	}

	/// <summary>
	/// Exports telemetry data to CSV file (public for testing)
	/// </summary>
	public async Task ExportToCsvAsync(string? filePath = null)
	{
		try
		{
			if (CurrentLap <= 0)
			{
				StatusMessage = "No lap selected for export";
				return;
			}

			var lapData = _telemetryBuffer.GetLapData(CurrentLap);
			if (lapData.Length == 0)
			{
				StatusMessage = $"No data found for lap {CurrentLap}";
				return;
			}

			var fileName = filePath ?? $"Telemetry_Lap{CurrentLap}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
			var csv = new StringBuilder();
			
			// Header
			csv.AppendLine("Time,Speed(km/h),Throttle(%),Brake(%),Steering(°),TyreFL(°C),TyreFR(°C),TyreRL(°C),TyreRR(°C),Fuel(L)");
			
			// Data rows
			double time = 0;
			foreach (var sample in lapData)
			{
				csv.AppendLine($"{time:F2},{sample.SpeedKph:F1},{sample.ThrottlePosition * 100:F1},{sample.BrakePosition * 100:F1}," +
					$"{sample.SteeringAngle:F1}," +
					$"{(sample.TyreTempsC.Length > 0 ? sample.TyreTempsC[0] : 0):F1}," +
					$"{(sample.TyreTempsC.Length > 1 ? sample.TyreTempsC[1] : 0):F1}," +
					$"{(sample.TyreTempsC.Length > 2 ? sample.TyreTempsC[2] : 0):F1}," +
					$"{(sample.TyreTempsC.Length > 3 ? sample.TyreTempsC[3] : 0):F1}," +
					$"{sample.FuelLiters:F2}");
				time += 0.01; // Assuming 100Hz sampling
			}

			await File.WriteAllTextAsync(fileName, csv.ToString());
			StatusMessage = $"Exported {lapData.Length} samples to {fileName}";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Export failed: {ex.Message}";
		}
	}

	/// <summary>
	/// Loads current lap data from the telemetry buffer into the plot data series
	/// </summary>
	public void LoadCurrentLapData(int lapNumber)
	{
		if (lapNumber <= 0) return;

		CurrentLap = lapNumber;
		var lapData = _telemetryBuffer.GetLapData(lapNumber);

		if (lapData.Length == 0)
		{
			StatusMessage = $"No data found for lap {lapNumber}";
			ClearCurrentLapData();
			return;
		}

		PopulateDataSeries(lapData, SpeedData, ThrottleData, BrakeData, SteeringData, TireTempData);
		StatusMessage = $"Loaded {lapData.Length} samples for lap {lapNumber}";
		RaiseDataSeriesUpdated();
	}

	private void LoadReferenceLapData()
	{
		if (!SelectedReferenceLap.HasValue || SelectedReferenceLap.Value <= 0) return;

		var lapData = _telemetryBuffer.GetLapData(SelectedReferenceLap.Value);

		if (lapData.Length == 0)
		{
			StatusMessage = $"No data found for reference lap {SelectedReferenceLap}";
			ClearReferenceLapData();
			return;
		}

		PopulateDataSeries(lapData, ReferenceSpeedData, ReferenceThrottleData, ReferenceBrakeData, ReferenceSteeringData, ReferenceTireTempData);
		StatusMessage = $"Loaded reference lap {SelectedReferenceLap} with {lapData.Length} samples";
		RaiseDataSeriesUpdated();
	}

	/// <summary>
	/// Loads reference lap data (public for testing)
	/// </summary>
	public void LoadReferenceLapData(int lapNumber)
	{
		SelectedReferenceLap = lapNumber;
	}

	private void PopulateDataSeries(
		TelemetrySampleDto[] samples,
		ObservableCollection<TelemetryDataPoint> speedSeries,
		ObservableCollection<TelemetryDataPoint> throttleSeries,
		ObservableCollection<TelemetryDataPoint> brakeSeries,
		ObservableCollection<TelemetryDataPoint> steeringSeries,
		ObservableCollection<TelemetryDataPoint> tireTempSeries)
	{
		speedSeries.Clear();
		throttleSeries.Clear();
		brakeSeries.Clear();
		steeringSeries.Clear();
		tireTempSeries.Clear();

		double time = 0;
		foreach (var sample in samples)
		{
			speedSeries.Add(new TelemetryDataPoint { Time = time, Value = sample.SpeedKph });
			throttleSeries.Add(new TelemetryDataPoint { Time = time, Value = sample.ThrottlePosition * 100 });
			brakeSeries.Add(new TelemetryDataPoint { Time = time, Value = sample.BrakePosition * 100 });
			steeringSeries.Add(new TelemetryDataPoint { Time = time, Value = sample.SteeringAngle });
			
			// Average tire temperature
			var avgTireTemp = sample.TyreTempsC.Length > 0 ? sample.TyreTempsC.Average() : 0;
			tireTempSeries.Add(new TelemetryDataPoint { Time = time, Value = avgTireTemp });

			time += 0.01; // 100Hz = 10ms = 0.01s
		}
	}

	/// <summary>
	/// Updates the available laps list from telemetry buffer without disrupting ComboBox selection
	/// </summary>
	public void RefreshAvailableLaps()
	{
		var laps = _telemetryBuffer.GetAvailableLaps();

		// If user has explicitly selected a reference lap, preserve it and skip updates
		// This prevents ComboBox binding disruption while a selection is active
		if (SelectedReferenceLap.HasValue && laps.Contains(SelectedReferenceLap.Value))
		{
			// Lap still exists and user selected it explicitly, don't update collection
			if (laps.Length > 0)
			{
				StatusMessage = $"{laps.Length} laps available (reference lap {SelectedReferenceLap} selected)";
			}
			return;
		}

		// Check if list has actually changed by comparing both length and content
		bool needsUpdate = AvailableLaps.Count != laps.Length;
		if (!needsUpdate && laps.Length > 0)
		{
			// Verify all items match
			for (int i = 0; i < laps.Length; i++)
			{
				if (AvailableLaps[i] != laps[i])
				{
					needsUpdate = true;
					break;
				}
			}
		}

		// If list hasn't changed, just update message and exit without touching collections
		if (!needsUpdate)
		{
			if (laps.Length > 0)
			{
				StatusMessage = $"{laps.Length} laps available for analysis";
			}
			return;
		}

		// Only update if list actually changed - add/remove items instead of clearing
		// to avoid disrupting ComboBox binding
		var toRemove = AvailableLaps.Where(l => !laps.Contains(l)).ToList();
		foreach (var lap in toRemove)
		{
			AvailableLaps.Remove(lap);
		}

		var toAdd = laps.Where(l => !AvailableLaps.Contains(l)).ToList();
		foreach (var lap in toAdd)
		{
			AvailableLaps.Add(lap);
		}

		// Verify selection is still valid
		if (laps.Length > 0)
		{
			if (!SelectedReferenceLap.HasValue || !laps.Contains(SelectedReferenceLap.Value))
			{
				SelectedReferenceLap = laps[0];
			}
			StatusMessage = $"{laps.Length} laps available for analysis";
		}
		else
		{
			SelectedReferenceLap = null;
			StatusMessage = "No telemetry data available yet";
		}
	}

	/// <summary>
	/// Updates cursor data table when user moves crosshair
	/// </summary>
	public void UpdateCursorData(double position)
	{
		CursorPosition = position;

		// Find index in data arrays based on time position
		var currentIndex = (int)(position * 100); // 100Hz sampling
		var referenceIndex = currentIndex;

		// Extract current lap values
		var currentValues = ExtractValuesAtIndex(SpeedData, ThrottleData, BrakeData, SteeringData, TireTempData, currentIndex);
		var referenceValues = ExtractValuesAtIndex(ReferenceSpeedData, ReferenceThrottleData, ReferenceBrakeData, ReferenceSteeringData, ReferenceTireTempData, referenceIndex);

		// Update cursor data table
		UpdateCursorDataRow(0, "vSpeed", currentValues.Speed, referenceValues.Speed, "km/h");
		UpdateCursorDataRow(1, "nThrottle", currentValues.Throttle, referenceValues.Throttle, "%");
		UpdateCursorDataRow(2, "nBrake", currentValues.Brake, referenceValues.Brake, "%");
		UpdateCursorDataRow(3, "rSteer", currentValues.Steering, referenceValues.Steering, "°");
		UpdateCursorDataRow(4, "tTyreCentre_FL", currentValues.TireTemp, referenceValues.TireTemp, "°C");
		UpdateCursorDataRow(5, "tTyreCentre_FR", currentValues.TireTemp, referenceValues.TireTemp, "°C");
		UpdateCursorDataRow(6, "tTyreCentre_RL", currentValues.TireTemp, referenceValues.TireTemp, "°C");
		UpdateCursorDataRow(7, "tTyreCentre_RR", currentValues.TireTemp, referenceValues.TireTemp, "°C");
		UpdateCursorDataRow(8, "fFuel", currentValues.Fuel, referenceValues.Fuel, "L");
	}

	private (double Speed, double Throttle, double Brake, double Steering, double TireTemp, double Fuel) ExtractValuesAtIndex(
		ObservableCollection<TelemetryDataPoint> speedData,
		ObservableCollection<TelemetryDataPoint> throttleData,
		ObservableCollection<TelemetryDataPoint> brakeData,
		ObservableCollection<TelemetryDataPoint> steeringData,
		ObservableCollection<TelemetryDataPoint> tireTempData,
		int index)
	{
		if (index < 0 || speedData.Count == 0)
			return (0, 0, 0, 0, 0, 0);

		index = Math.Min(index, speedData.Count - 1);

		return (
			index < speedData.Count ? speedData[index].Value : 0,
			index < throttleData.Count ? throttleData[index].Value : 0,
			index < brakeData.Count ? brakeData[index].Value : 0,
			index < steeringData.Count ? steeringData[index].Value : 0,
			index < tireTempData.Count ? tireTempData[index].Value : 0,
			0 // Fuel not in series yet
		);
	}

	private void UpdateCursorDataRow(int rowIndex, string parameter, double currentValue, double referenceValue, string unit)
	{
		if (rowIndex >= CursorData.Count) return;

		var row = CursorData[rowIndex];
		row.Parameter = parameter;
		row.Unit = unit;
		row.CurrentValue = currentValue > 0 ? $"{currentValue:F1}" : "--";
		row.ReferenceValue = referenceValue > 0 ? $"{referenceValue:F1}" : "--";

		if (currentValue > 0 && referenceValue > 0)
		{
			var delta = currentValue - referenceValue;
			row.Delta = $"{(delta >= 0 ? "▲" : "▼")} {Math.Abs(delta):F1}";
		}
		else
		{
			row.Delta = "--";
		}
	}

	private void ClearCurrentLapData()
	{
		SpeedData.Clear();
		ThrottleData.Clear();
		BrakeData.Clear();
		SteeringData.Clear();
		TireTempData.Clear();
		RaiseDataSeriesUpdated();
	}

	private void ClearReferenceLapData()
	{
		ReferenceSpeedData.Clear();
		ReferenceThrottleData.Clear();
		ReferenceBrakeData.Clear();
		ReferenceSteeringData.Clear();
		ReferenceTireTempData.Clear();
		RaiseDataSeriesUpdated();
	}

	private void RaiseDataSeriesUpdated()
	{
		DataSeriesUpdated?.Invoke(this, EventArgs.Empty);
	}
}

/// <summary>
/// Data model for cursor data table rows
/// </summary>
public partial class CursorDataRow : ObservableObject
{
	[ObservableProperty]
	private string parameter = string.Empty;

	[ObservableProperty]
	private string currentValue = "--";

	[ObservableProperty]
	private string referenceValue = "--";

	[ObservableProperty]
	private string delta = "--";

	[ObservableProperty]
	private string unit = string.Empty;
}

public class TelemetryDataPoint
{
	public double Time { get; set; }
	public double Value { get; set; }
}
