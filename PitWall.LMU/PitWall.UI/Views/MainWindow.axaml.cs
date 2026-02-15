using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using PitWall.UI.ViewModels;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    private readonly Dictionary<AvaPlot, VerticalLine> _cursorLines = new();
    private double? _cursorTimeSeconds;

    public MainWindow()
    {
        InitializeComponent();
        InitializePlots();
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
        Closed += OnClosed;
        KeyDown += OnKeyDown;
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
            _telemetryViewModel.PropertyChanged -= OnTelemetryPropertyChanged;
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

        AttachPlotInteractions(_speedPlot);
        AttachPlotInteractions(_throttlePlot);
        AttachPlotInteractions(_brakePlot);
        AttachPlotInteractions(_steeringPlot);
        AttachPlotInteractions(_tireTempPlot);
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
            _telemetryViewModel.PropertyChanged -= OnTelemetryPropertyChanged;
        }

        _telemetryViewModel = telemetry;
        _telemetryViewModel.DataSeriesUpdated += OnDataSeriesUpdated;
        _telemetryViewModel.PropertyChanged += OnTelemetryPropertyChanged;
        UpdateAllPlots();
    }

    private void OnTelemetryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_telemetryViewModel == null)
        {
            return;
        }

        if (e.PropertyName == nameof(TelemetryAnalysisViewModel.CursorPosition))
        {
            var time = _telemetryViewModel.CursorPosition;
            if (double.IsNaN(time) || double.IsInfinity(time))
            {
                return;
            }

            _cursorTimeSeconds = time;
            UpdateCursorLines();
        }
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

        // Sync cursor time from ViewModel before rebuilding plots,
        // so the green cursor line is positioned correctly after a plot rebuild.
        var vmCursorTime = _telemetryViewModel.CursorPosition;
        if (!double.IsNaN(vmCursorTime) && !double.IsInfinity(vmCursorTime) && vmCursorTime > 0)
        {
            _cursorTimeSeconds = vmCursorTime;
        }

        UpdatePlot(_speedPlot, _telemetryViewModel.SpeedData, Colors.OrangeRed, PlotAxisMode.SpeedZeroBased);
        UpdatePlot(_throttlePlot, _telemetryViewModel.ThrottleData, Colors.OrangeRed, PlotAxisMode.Percentage);
        UpdatePlot(_brakePlot, _telemetryViewModel.BrakeData, Colors.OrangeRed, PlotAxisMode.Percentage);
        UpdatePlot(_steeringPlot, _telemetryViewModel.SteeringData, Colors.OrangeRed, PlotAxisMode.SymmetricAuto);
        UpdatePlot(_tireTempPlot, _telemetryViewModel.TireTempData, Colors.OrangeRed, PlotAxisMode.AutoScale);
    }

    private enum PlotAxisMode
    {
        AutoScale,
        Percentage,      // Fixed 0-100
        SpeedZeroBased,  // 0 to max+10%
        SymmetricAuto    // -max to +max
    }

    private void UpdatePlot(
        AvaPlot? plotControl,
        System.Collections.Generic.IReadOnlyCollection<TelemetryDataPoint> currentData,
        Color currentColor,
        PlotAxisMode axisMode = PlotAxisMode.AutoScale)
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

        var cursorLine = plot.Add.VerticalLine(0, color: Colors.Lime);
        cursorLine.LineWidth = 2;
        if (_cursorTimeSeconds.HasValue)
        {
            cursorLine.X = _cursorTimeSeconds.Value;
            cursorLine.IsVisible = true;
        }
        else
        {
            cursorLine.IsVisible = false;
        }

        _cursorLines[plotControl] = cursorLine;

        // Set Y-axis ranges appropriate to the channel type
        switch (axisMode)
        {
            case PlotAxisMode.Percentage:
                plot.Axes.AutoScale();
                plot.Axes.SetLimitsY(-2, 105);
                break;

            case PlotAxisMode.SpeedZeroBased:
                plot.Axes.AutoScale();
                if (currentData.Count > 0)
                {
                    var maxSpeed = currentData.Max(p => p.Value);
                    plot.Axes.SetLimitsY(-5, Math.Max(50, maxSpeed * 1.1));
                }
                else
                {
                    plot.Axes.SetLimitsY(0, 350);
                }
                break;

            case PlotAxisMode.SymmetricAuto:
                plot.Axes.AutoScale();
                if (currentData.Count > 0)
                {
                    var maxAbs = currentData.Max(p => Math.Abs(p.Value));
                    maxAbs = Math.Max(1, maxAbs * 1.2);
                    plot.Axes.SetLimitsY(-maxAbs, maxAbs);
                }
                break;

            default:
                plot.Axes.AutoScale();
                break;
        }

        plotControl.InvalidateVisual();
    }

    private void AttachPlotInteractions(AvaPlot? plotControl)
    {
        if (plotControl == null)
        {
            return;
        }

        plotControl.PointerMoved += OnPlotPointerMoved;
        plotControl.PointerExited += OnPlotPointerExited;
    }

    private void OnPlotPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_telemetryViewModel == null || sender is not AvaPlot plotControl)
        {
            return;
        }

        var position = e.GetPosition(plotControl);
        var coordinates = plotControl.Plot.GetCoordinates(new Pixel(position.X, position.Y));
        var time = coordinates.X;
        if (double.IsNaN(time) || double.IsInfinity(time))
        {
            return;
        }

        _telemetryViewModel.IsMouseHovering = true;
        _cursorTimeSeconds = time;
        UpdateCursorLines();
        _telemetryViewModel.UpdateCursorData(time);
        var sample = _telemetryViewModel.GetSampleAtTime(time);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.UpdateHoverSample(sample);
        }
    }

    private void OnPlotPointerExited(object? sender, PointerEventArgs e)
    {
        if (_telemetryViewModel != null)
        {
            _telemetryViewModel.IsMouseHovering = false;

            // Restore the replay cursor position instead of hiding the line.
            // CursorPosition is kept in sync by UpdateReplayCursor during playback.
            var replayTime = _telemetryViewModel.CursorPosition;
            if (!double.IsNaN(replayTime) && !double.IsInfinity(replayTime) && replayTime > 0)
            {
                _cursorTimeSeconds = replayTime;
            }
            else
            {
                _cursorTimeSeconds = null;
            }
        }
        else
        {
            _cursorTimeSeconds = null;
        }
        UpdateCursorLines();
    }

    private void UpdateCursorLines()
    {
        foreach (var plotControl in _cursorLines.Keys.ToList())
        {
            if (_cursorTimeSeconds.HasValue)
            {
                _cursorLines[plotControl].X = _cursorTimeSeconds.Value;
                _cursorLines[plotControl].IsVisible = true;
            }
            else
            {
                _cursorLines[plotControl].IsVisible = false;
            }

            plotControl.Refresh();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (e.Key == Key.Space && IsTextInputTarget(e.Source))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.F1:
                vm.SelectedTabIndex = 0;
                e.Handled = true;
                break;
            case Key.F2:
                vm.SelectedTabIndex = 1;
                e.Handled = true;
                break;
            case Key.F3:
                vm.SelectedTabIndex = 2;
                e.Handled = true;
                break;
            case Key.F4:
                vm.SelectedTabIndex = 3;
                e.Handled = true;
                break;
            case Key.F5:
                vm.SelectedTabIndex = 4;
                e.Handled = true;
                break;
            case Key.F6:
                vm.RequestPitNow();
                e.Handled = true;
                break;
            case Key.F12:
                vm.TriggerEmergencyMode();
                e.Handled = true;
                break;
            case Key.Space:
                vm.PauseTelemetry();
                e.Handled = true;
                break;
            case Key.Escape:
                vm.DismissAlerts();
                e.Handled = true;
                break;
        }
    }

    private static bool IsTextInputTarget(object? source)
    {
        // Check for common text input controls that should receive Space key input.
        // Uses type inheritance check to catch TextBox subclasses like MaskedTextBox.
        return source is TextBox 
            || source is ComboBox 
            || source is AutoCompleteBox
            || source is NumericUpDown
            || (source?.GetType().IsAssignableTo(typeof(TextBox)) == true);
    }
}