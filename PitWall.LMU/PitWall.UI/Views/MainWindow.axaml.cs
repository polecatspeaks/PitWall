using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PitWall.UI.ViewModels;
using ScottPlot;
using ScottPlot.Avalonia;
using System;
using System.Linq;
using System.Threading;

namespace PitWall.UI.Views;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cts;
    private TelemetryAnalysisViewModel? _telemetryViewModel;
    private AvaPlot? _speedPlot;
    private AvaPlot? _throttlePlot;
    private AvaPlot? _brakePlot;
    private AvaPlot? _steeringPlot;
    private AvaPlot? _tireTempPlot;

    public MainWindow()
    {
        InitializeComponent();
        InitializePlots();
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            _cts = new CancellationTokenSource();
            var sessionId = Environment.GetEnvironmentVariable("PITWALL_SESSION_ID") ?? "1";
            await vm.StartAsync(sessionId, _cts.Token);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_telemetryViewModel != null)
        {
            _telemetryViewModel.DataSeriesUpdated -= OnDataSeriesUpdated;
            _telemetryViewModel = null;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            vm.Stop();
        }
    }

    private void InitializePlots()
    {
        _speedPlot = this.FindControl<AvaPlot>("SpeedPlot");
        _throttlePlot = this.FindControl<AvaPlot>("ThrottlePlot");
        _brakePlot = this.FindControl<AvaPlot>("BrakePlot");
        _steeringPlot = this.FindControl<AvaPlot>("SteeringPlot");
        _tireTempPlot = this.FindControl<AvaPlot>("TireTempPlot");
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            AttachTelemetryViewModel(vm.TelemetryAnalysis);
        }
    }

    private void AttachTelemetryViewModel(TelemetryAnalysisViewModel telemetry)
    {
        if (_telemetryViewModel != null)
        {
            _telemetryViewModel.DataSeriesUpdated -= OnDataSeriesUpdated;
        }

        _telemetryViewModel = telemetry;
        _telemetryViewModel.DataSeriesUpdated += OnDataSeriesUpdated;
        UpdateAllPlots();
    }

    private void OnDataSeriesUpdated(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(UpdateAllPlots);
            return;
        }

        UpdateAllPlots();
    }

    private void UpdateAllPlots()
    {
        if (_telemetryViewModel == null)
        {
            return;
        }

        UpdatePlot(_speedPlot, _telemetryViewModel.SpeedData, _telemetryViewModel.ReferenceSpeedData, Colors.OrangeRed, Colors.DeepSkyBlue);
        UpdatePlot(_throttlePlot, _telemetryViewModel.ThrottleData, _telemetryViewModel.ReferenceThrottleData, Colors.OrangeRed, Colors.DeepSkyBlue);
        UpdatePlot(_brakePlot, _telemetryViewModel.BrakeData, _telemetryViewModel.ReferenceBrakeData, Colors.OrangeRed, Colors.DeepSkyBlue);
        UpdatePlot(_steeringPlot, _telemetryViewModel.SteeringData, _telemetryViewModel.ReferenceSteeringData, Colors.OrangeRed, Colors.DeepSkyBlue);
        UpdatePlot(_tireTempPlot, _telemetryViewModel.TireTempData, _telemetryViewModel.ReferenceTireTempData, Colors.OrangeRed, Colors.DeepSkyBlue);
    }

    private static void UpdatePlot(
        AvaPlot? plotControl,
        System.Collections.Generic.IReadOnlyCollection<TelemetryDataPoint> currentData,
        System.Collections.Generic.IReadOnlyCollection<TelemetryDataPoint> referenceData,
        Color currentColor,
        Color referenceColor)
    {
        if (plotControl == null)
        {
            return;
        }

        var plot = plotControl.Plot;
        plot.Clear();

        if (currentData.Count > 0)
        {
            var xs = currentData.Select(point => point.Time).ToArray();
            var ys = currentData.Select(point => point.Value).ToArray();
            plot.Add.SignalXY(xs, ys, color: currentColor);
        }

        if (referenceData.Count > 0)
        {
            var xs = referenceData.Select(point => point.Time).ToArray();
            var ys = referenceData.Select(point => point.Value).ToArray();
            plot.Add.SignalXY(xs, ys, color: referenceColor);
        }

        plot.Axes.AutoScale();
        plotControl.InvalidateVisual();
    }
}