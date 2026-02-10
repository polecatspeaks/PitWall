using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PitWall.UI.Models;

namespace PitWall.UI.ViewModels;

/// <summary>
/// ViewModel for telemetry analysis with waveform displays, lap comparison,
/// and historical data visualization using ScottPlot.
/// </summary>
public partial class TelemetryAnalysisViewModel : ViewModelBase
{
	[ObservableProperty]
	private int selectedReferenceLap = 1;

	[ObservableProperty]
	private int currentLap = 1;

	[ObservableProperty]
	private bool isPlayingBack;

	[ObservableProperty]
	private double cursorPosition;

	[ObservableProperty]
	private string cursorSpeed = "-- km/h";

	[ObservableProperty]
	private string cursorThrottle = "--%";

	[ObservableProperty]
	private string cursorBrake = "--%";

	[ObservableProperty]
	private string cursorSteering = "--°";

	[ObservableProperty]
	private string cursorTireTemp = "-- °C";

	public ObservableCollection<int> AvailableLaps { get; } = new();

	public ObservableCollection<TelemetryDataPoint> SpeedData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> ThrottleData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> BrakeData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> SteeringData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> TireTempData { get; } = new();

	public ObservableCollection<TelemetryDataPoint> ReferenceSpeedData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> ReferenceThrottleData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> ReferenceBrakeData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> ReferenceSteeringData { get; } = new();
	public ObservableCollection<TelemetryDataPoint> ReferenceTireTempData { get; } = new();

	[RelayCommand]
	private void SelectReferenceLap(int lapNumber)
	{
		SelectedReferenceLap = lapNumber;
		LoadReferenceLapData();
	}

	[RelayCommand]
	private void PreviousSector()
	{
		// TODO: Jump to previous sector in waveform
	}

	[RelayCommand]
	private void NextSector()
	{
		// TODO: Jump to next sector in waveform
	}

	[RelayCommand]
	private void ZoomIn()
	{
		// TODO: Zoom in on waveform display
	}

	[RelayCommand]
	private void ZoomOut()
	{
		// TODO: Zoom out on waveform display
	}

	[RelayCommand]
	private async Task ExportToCsvAsync()
	{
		// TODO: Export current lap data to CSV
		await Task.CompletedTask;
	}

	private void LoadReferenceLapData()
	{
		// TODO: Load reference lap data from telemetry buffer
	}

	public void UpdateCursorData(double position)
	{
		CursorPosition = position;
		// TODO: Extract values at cursor position from data arrays
	}
}

public class TelemetryDataPoint
{
	public double Time { get; set; }
	public double Value { get; set; }
}
