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
            await vm.StartAsync(sessionId, _cts.Token);
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