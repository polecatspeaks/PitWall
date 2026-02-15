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
/// ViewModel for telemetry analysis with waveform displays
/// and historical data visualization using ScottPlot.
/// </summary>
public partial class TelemetryAnalysisViewModel : ViewModelBase
{
	private readonly TelemetryBuffer _telemetryBuffer;

	public event EventHandler? DataSeriesUpdated;

	[ObservableProperty]
	private int currentLap = 1;

	[ObservableProperty]
	private bool isPlayingBack;

	[ObservableProperty]
	private double cursorPosition = double.NaN;

	/// <summary>
	/// Formatted "LAP X / Y" string for display in the telemetry header.
	/// Automatically updated when <see cref="CurrentLap"/> changes.
	/// </summary>
	[ObservableProperty]
	private string lapDisplay = "LAP --";

	/// <summary>
	/// Called by CommunityToolkit source generator when <see cref="CurrentLap"/> changes.
	/// </summary>
	partial void OnCurrentLapChanged(int value)
	{
		var total = AvailableLaps.Count > 0 ? AvailableLaps.Count.ToString() : "--";
		LapDisplay = value > 0 ? $"LAP {value} / {total}" : "LAP --";
	}

	/// <summary>
	/// When true, mouse hover controls the cursor — replay cursor updates are suppressed.
	/// </summary>
	public bool IsMouseHovering { get; set; }

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
		CursorData.Add(new CursorDataRow { Parameter = "vSpeed", Unit = "km/h", CurrentValue = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "nThrottle", Unit = "%", CurrentValue = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "nBrake", Unit = "%", CurrentValue = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "rSteer", Unit = "°", CurrentValue = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "tTyreCentre_FL", Unit = "°C", CurrentValue = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "tTyreCentre_FR", Unit = "°C", CurrentValue = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "tTyreCentre_RL", Unit = "°C", CurrentValue = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "tTyreCentre_RR", Unit = "°C", CurrentValue = "--" });
		CursorData.Add(new CursorDataRow { Parameter = "fFuel", Unit = "L", CurrentValue = "--" });
	}

	/// <summary>
	/// Navigate to the previous lap in the telemetry buffer.
	/// </summary>
	[RelayCommand]
	private void PreviousLap()
	{
		if (CurrentLap <= 0)
		{
			StatusMessage = "No lap loaded";
			return;
		}

		var laps = _telemetryBuffer.GetAvailableLaps();
		var idx = Array.IndexOf(laps, CurrentLap);
		if (idx > 0)
		{
			LoadCurrentLapData(laps[idx - 1]);
		}
		else
		{
			StatusMessage = $"Already on first available lap ({CurrentLap})";
		}
	}

	/// <summary>
	/// Navigate to the next lap in the telemetry buffer.
	/// </summary>
	[RelayCommand]
	private void NextLap()
	{
		if (CurrentLap <= 0)
		{
			StatusMessage = "No lap loaded";
			return;
		}

		var laps = _telemetryBuffer.GetAvailableLaps();
		var idx = Array.IndexOf(laps, CurrentLap);
		if (idx >= 0 && idx < laps.Length - 1)
		{
			LoadCurrentLapData(laps[idx + 1]);
		}
		else
		{
			StatusMessage = $"Already on last available lap ({CurrentLap})";
		}
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
			if (CurrentLap < 0)
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
				time += 0.02; // 50Hz sampling
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
		if (lapNumber <= 0)
		{
			return;
		}

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

		// Use actual timestamps relative to first sample for the time axis.
		// Data is now interpolated to a uniform 50Hz grid.
		DateTime? firstTimestamp = samples.Length > 0 ? samples[0].Timestamp : null;

		for (int i = 0; i < samples.Length; i++)
		{
			var sample = samples[i];
			double time;
			if (firstTimestamp.HasValue && sample.Timestamp.HasValue)
			{
				time = (sample.Timestamp.Value - firstTimestamp.Value).TotalSeconds;
			}
			else
			{
				// Fallback: assume 50Hz spacing if timestamps are missing
				time = i * 0.02;
			}

			speedSeries.Add(new TelemetryDataPoint { Time = time, Value = sample.SpeedKph });
			throttleSeries.Add(new TelemetryDataPoint { Time = time, Value = sample.ThrottlePosition * 100 });
			brakeSeries.Add(new TelemetryDataPoint { Time = time, Value = sample.BrakePosition * 100 });
			steeringSeries.Add(new TelemetryDataPoint { Time = time, Value = sample.SteeringAngle });

			// Average tire temperature
			var avgTireTemp = sample.TyreTempsC.Length > 0 ? sample.TyreTempsC.Average() : 0;
			tireTempSeries.Add(new TelemetryDataPoint { Time = time, Value = avgTireTemp });
		}
	}

	/// <summary>
	/// Updates the available laps list from telemetry buffer
	/// </summary>
	public void RefreshAvailableLaps()
	{
		var laps = _telemetryBuffer.GetAvailableLaps();

		// Check if list has actually changed by comparing both length and content
		bool needsUpdate = AvailableLaps.Count != laps.Length;
		if (!needsUpdate && laps.Length > 0)
		{
			for (int i = 0; i < laps.Length; i++)
			{
				if (AvailableLaps[i] != laps[i])
				{
					needsUpdate = true;
					break;
				}
			}
		}

		if (!needsUpdate)
		{
			if (laps.Length > 0)
			{
				StatusMessage = $"{laps.Length} laps available for analysis";
			}
			return;
		}

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

		if (laps.Length > 0)
		{
			StatusMessage = $"{laps.Length} laps available for analysis";
		}
		else
		{
			StatusMessage = "No telemetry data available yet";
		}

		// Refresh the lap display with updated total count.
		OnCurrentLapChanged(CurrentLap);
	}

	/// <summary>
	/// Updates cursor data table when user moves crosshair.
	/// Uses raw sample data for individual tyre temperatures.
	/// </summary>
	public void UpdateCursorData(double position)
	{
		CursorPosition = position;

		// Get raw sample for individual tyre temps and fuel
		var sample = GetSampleAtTime(position);
		if (sample != null)
		{
			UpdateCursorDataFromSample(sample, (int)Math.Round(position / 0.02));
			return;
		}

		// Fallback to plot data series if no raw sample available
		var currentIndex = (int)Math.Round(position / 0.02); // 50Hz sampling
		var currentValues = ExtractValuesAtIndex(SpeedData, ThrottleData, BrakeData, SteeringData, TireTempData, currentIndex);

		UpdateCursorDataRow(0, "vSpeed", currentValues.Speed, "km/h");
		UpdateCursorDataRow(1, "nThrottle", currentValues.Throttle, "%");
		UpdateCursorDataRow(2, "nBrake", currentValues.Brake, "%");
		UpdateCursorDataRow(3, "rSteer", currentValues.Steering, "°");
		UpdateCursorDataRow(4, "tTyreCentre_FL", currentValues.TireTemp, "°C");
		UpdateCursorDataRow(5, "tTyreCentre_FR", currentValues.TireTemp, "°C");
		UpdateCursorDataRow(6, "tTyreCentre_RL", currentValues.TireTemp, "°C");
		UpdateCursorDataRow(7, "tTyreCentre_RR", currentValues.TireTemp, "°C");
		UpdateCursorDataRow(8, "fFuel", currentValues.Fuel, "L");
		OnPropertyChanged(nameof(CursorData));
	}

	/// <summary>
	/// Updates the replay cursor and data table using the active sample.
	/// Skipped when mouse hover is active.  When the replay moves to a
	/// different lap the displayed plot is auto-switched so the cursor
	/// follows the replay progression.
	/// </summary>
	public void UpdateReplayCursor(TelemetrySampleDto sample)
	{
		if (sample == null || sample.LapNumber < 0)
		{
			return;
		}

		// Don't overwrite cursor data when the user is hovering over the plot.
		if (IsMouseHovering)
		{
			return;
		}

		// Auto-switch displayed lap to follow the replay.
		if (sample.LapNumber != CurrentLap && sample.LapNumber > 0)
		{
			LoadCurrentLapData(sample.LapNumber);
		}

		var lapData = _telemetryBuffer.GetLapData(sample.LapNumber);
		if (lapData.Length == 0)
		{
			return;
		}

		var index = Array.FindIndex(lapData, s => ReferenceEquals(s, sample));
		if (index < 0 && sample.Timestamp.HasValue)
		{
			var timestamp = sample.Timestamp.Value;
			index = Array.FindIndex(
				lapData,
				s => s.Timestamp.HasValue && s.Timestamp.Value == timestamp);
		}

		if (index < 0)
		{
			index = Math.Max(0, lapData.Length - 1);
		}

		var timeSeconds = Math.Clamp(index, 0, lapData.Length - 1) * 0.02; // 50Hz = 0.02s per sample
		CursorPosition = timeSeconds;
		UpdateCursorDataFromSample(sample, index);
	}

	public TelemetrySampleDto? GetSampleAtTime(double timeSeconds)
	{
		if (CurrentLap < 0)
		{
			return null;
		}

		var lapData = _telemetryBuffer.GetLapData(CurrentLap);
		if (lapData.Length == 0)
		{
			return null;
		}

		var index = (int)Math.Round(timeSeconds / 0.02); // 50Hz = 0.02s per sample
		index = Math.Clamp(index, 0, lapData.Length - 1);
		return lapData[index];
	}

	private (double? Speed, double? Throttle, double? Brake, double? Steering, double? TireTemp, double? Fuel) ExtractValuesAtIndex(
		ObservableCollection<TelemetryDataPoint> speedData,
		ObservableCollection<TelemetryDataPoint> throttleData,
		ObservableCollection<TelemetryDataPoint> brakeData,
		ObservableCollection<TelemetryDataPoint> steeringData,
		ObservableCollection<TelemetryDataPoint> tireTempData,
		int index)
	{
		if (index < 0)
		{
			return (null, null, null, null, null, null);
		}

		return (
			speedData.Count > 0 ? speedData[Math.Min(index, speedData.Count - 1)].Value : null,
			throttleData.Count > 0 ? throttleData[Math.Min(index, throttleData.Count - 1)].Value : null,
			brakeData.Count > 0 ? brakeData[Math.Min(index, brakeData.Count - 1)].Value : null,
			steeringData.Count > 0 ? steeringData[Math.Min(index, steeringData.Count - 1)].Value : null,
			tireTempData.Count > 0 ? tireTempData[Math.Min(index, tireTempData.Count - 1)].Value : null,
			null // Fuel not in series yet
		);
	}

	private void UpdateCursorDataRow(int rowIndex, string parameter, double? currentValue, string unit)
	{
		if (rowIndex >= CursorData.Count) return;

		var row = CursorData[rowIndex];
		row.Parameter = parameter;
		row.Unit = unit;
		row.CurrentValue = FormatCursorValue(currentValue);
	}

	private static string FormatCursorValue(double? value)
	{
		return value.HasValue ? $"{value.Value:F1}" : "--";
	}

	private void UpdateCursorDataFromSample(TelemetrySampleDto sample, int index)
	{
		var current = ExtractFullValuesFromSample(sample);

		UpdateCursorDataRow(0, "vSpeed", current.Speed, "km/h");
		UpdateCursorDataRow(1, "nThrottle", current.Throttle, "%");
		UpdateCursorDataRow(2, "nBrake", current.Brake, "%");
		UpdateCursorDataRow(3, "rSteer", current.Steering, "°");
		UpdateCursorDataRow(4, "tTyreCentre_FL", current.TyreFL, "°C");
		UpdateCursorDataRow(5, "tTyreCentre_FR", current.TyreFR, "°C");
		UpdateCursorDataRow(6, "tTyreCentre_RL", current.TyreRL, "°C");
		UpdateCursorDataRow(7, "tTyreCentre_RR", current.TyreRR, "°C");
		UpdateCursorDataRow(8, "fFuel", current.Fuel, "L");
		OnPropertyChanged(nameof(CursorData));
	}

	private record struct FullSampleValues(
		double? Speed, double? Throttle, double? Brake, double? Steering,
		double? TyreFL, double? TyreFR, double? TyreRL, double? TyreRR,
		double? Fuel);

	private static FullSampleValues ExtractFullValuesFromSample(TelemetrySampleDto? sample)
	{
		if (sample == null)
		{
			return default;
		}

		var tyres = sample.TyreTempsC;
		return new FullSampleValues(
			sample.SpeedKph,
			sample.ThrottlePosition * 100.0,
			sample.BrakePosition * 100.0,
			sample.SteeringAngle,
			tyres is { Length: > 0 } ? tyres[0] : null,
			tyres is { Length: > 1 } ? tyres[1] : null,
			tyres is { Length: > 2 } ? tyres[2] : null,
			tyres is { Length: > 3 } ? tyres[3] : null,
			sample.FuelLiters);
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
	private string unit = string.Empty;
}

public class TelemetryDataPoint
{
	public double Time { get; set; }
	public double Value { get; set; }
}
