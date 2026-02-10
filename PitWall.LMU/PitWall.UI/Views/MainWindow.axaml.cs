using Avalonia.Controls;
using Avalonia.Interactivity;
using PitWall.UI.ViewModels;
using System;
using System.Threading;

namespace PitWall.UI.Views;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            _cts = new CancellationTokenSource();
            var sessionId = Environment.GetEnvironmentVariable("PITWALL_SESSION_ID") ?? "1";
            
            try
            {
                await vm.LoadSettingsAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load settings: {ex.Message}");
                // Continue with defaults
            }

            try
            {
                await vm.StartAsync(sessionId, _cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to start telemetry stream: {ex.Message}");
                Console.WriteLine("UI will continue with mock data. Ensure PitWall API is running at PITWALL_API_BASE.");
                // Continue without telemetry
            }
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (DataContext is MainWindowViewModel vm)
        {
            vm.Stop();
        }
    }
}