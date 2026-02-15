using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using rF2SharedMemoryNet;
using rF2SharedMemoryNet.LMUData.Models;
using rF2SharedMemoryNet.RF2Data.Enums;
using rF2SharedMemoryNet.RF2Data.Structs;

namespace LMUMemoryReader;

public partial class MainWindow : Window
{
    private const string PluginFileName = "rFactor2SharedMemoryMapPlugin64.dll";
    private const string PluginRepoLatestReleaseUrl = "https://api.github.com/repos/Domaslau/rF2SharedMemoryNet/releases/latest";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly MainViewModel _viewModel = new();
    private RF2MemoryReader? _memoryReader;
    private readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writerLock = new(1, 1);
    private DateTime _nextReaderInitAttemptUtc = DateTime.MinValue;
    private string _lmuInstallPath = string.Empty;
    private bool _pluginCheckCompleted;

    private TelemetryFileWriter? _writer;
    private string? _pendingOutputFilePath;
    private long _dataPoints;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        LoadSettings();

        Opened += OnOpened;
        Closing += OnClosing;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            IncludeFields = true,
            TypeInfoResolver = TelemetryJsonContext.Default
        };
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _ = Task.Run(() => PollLoopAsync(_cts.Token));
        _ = Dispatcher.UIThread.InvokeAsync(EnsurePluginAsync);
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        _cts.Cancel();
        await StopLoggingAsync();
        _memoryReader?.Dispose();
        SaveSettings();
    }

    private void LoadSettings()
    {
        var settings = SettingsStore.Load();
        var defaultDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LMU-Telemetry");
        _viewModel.OutputDirectory = string.IsNullOrWhiteSpace(settings.OutputDirectory)
            ? defaultDirectory
            : settings.OutputDirectory;
        _lmuInstallPath = settings.LmuInstallPath;
        StartupLogger.SetLogDirectory(_viewModel.OutputDirectory);
    }

    private void SaveSettings()
    {
        SettingsStore.Save(new AppSettings
        {
            OutputDirectory = _viewModel.OutputDirectory,
            LmuInstallPath = _lmuInstallPath
        });
    }

    private async Task EnsurePluginAsync()
    {
        if (_pluginCheckCompleted)
        {
            return;
        }

        _pluginCheckCompleted = true;
        var pluginDirectory = await ResolvePluginDirectoryAsync();
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return;
        }

        var pluginPath = Path.Combine(pluginDirectory, PluginFileName);
        if (File.Exists(pluginPath))
        {
            return;
        }

        var shouldInstall = await ShowPluginPromptAsync(pluginDirectory);
        if (!shouldInstall)
        {
            return;
        }

        await DownloadAndInstallPluginAsync(pluginDirectory, pluginPath);
    }

    private async Task<string?> ResolvePluginDirectoryAsync()
    {
        if (!string.IsNullOrWhiteSpace(_lmuInstallPath))
        {
            return EnsurePluginsDirectory(_lmuInstallPath);
        }

        var defaultLmuPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam",
            "steamapps",
            "common",
            "Le Mans Ultimate");

        var defaultPluginDirectory = EnsurePluginsDirectory(defaultLmuPath);
        if (!string.IsNullOrWhiteSpace(defaultPluginDirectory))
        {
            _lmuInstallPath = defaultLmuPath;
            SaveSettings();
            return defaultPluginDirectory;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select LMU install folder",
            AllowMultiple = false
        });

        var selected = folders.FirstOrDefault();
        if (selected?.Path is { } path)
        {
            _lmuInstallPath = path.LocalPath;
            SaveSettings();
            return EnsurePluginsDirectory(_lmuInstallPath);
        }

        return null;
    }

    private static string? EnsurePluginsDirectory(string lmuInstallPath)
    {
        if (string.IsNullOrWhiteSpace(lmuInstallPath))
        {
            return null;
        }

        if (!Directory.Exists(lmuInstallPath))
        {
            return null;
        }

        var pluginDirectory = Path.Combine(lmuInstallPath, "Plugins");
        Directory.CreateDirectory(pluginDirectory);
        return pluginDirectory;
    }

    private async Task<bool> ShowPluginPromptAsync(string pluginDirectory)
    {
        var message = $"Shared memory plugin not found.\n\nInstall to:\n{pluginDirectory}\n\nDownload and install now?";
        var dialog = new Window
        {
            Title = "LMU Plugin Missing",
            Width = 420,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var text = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(16)
        };

        var installButton = new Button
        {
            Content = "Download && Install",
            Width = 140,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(16, 0, 16, 16)
        };

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(installButton);

        var layout = new StackPanel
        {
            Spacing = 8
        };
        layout.Children.Add(text);
        layout.Children.Add(buttonPanel);
        dialog.Content = layout;

        var tcs = new TaskCompletionSource<bool>();
        installButton.Click += (_, _) =>
        {
            tcs.TrySetResult(true);
            dialog.Close();
        };
        cancelButton.Click += (_, _) =>
        {
            tcs.TrySetResult(false);
            dialog.Close();
        };

        dialog.Closed += (_, _) => tcs.TrySetResult(false);
        await dialog.ShowDialog(this);
        return await tcs.Task;
    }

    private async Task DownloadAndInstallPluginAsync(string pluginDirectory, string pluginPath)
    {
        try
        {
            StartupLogger.Info("Downloading shared memory plugin.");
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LMUMemoryReader");

            using var response = await client.GetAsync(PluginRepoLatestReleaseUrl);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("assets", out var assets))
            {
                await ShowInfoDialogAsync("Plugin Download Failed", "Release assets not found.");
                return;
            }

            string? downloadUrl = null;
            bool isZip = false;

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                var url = asset.GetProperty("browser_download_url").GetString();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                if (name.Equals(PluginFileName, StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = url;
                    isZip = false;
                    break;
                }

                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                    name.Contains("SharedMemory", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = url;
                    isZip = true;
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                await ShowInfoDialogAsync("Plugin Download Failed", "No plugin asset found in the latest release.");
                return;
            }

            Directory.CreateDirectory(pluginDirectory);
            if (isZip)
            {
                var tempZip = Path.Combine(Path.GetTempPath(), $"lmu_plugin_{Guid.NewGuid():N}.zip");
                await using (var stream = await client.GetStreamAsync(downloadUrl))
                await using (var fileStream = File.Create(tempZip))
                {
                    await stream.CopyToAsync(fileStream);
                }

                using var archive = ZipFile.OpenRead(tempZip);
                var entry = archive.Entries.FirstOrDefault(e =>
                    e.Name.Equals(PluginFileName, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    await ShowInfoDialogAsync("Plugin Download Failed", "DLL not found in the downloaded archive.");
                    return;
                }

                entry.ExtractToFile(pluginPath, overwrite: true);
                File.Delete(tempZip);
            }
            else
            {
                await using var stream = await client.GetStreamAsync(downloadUrl);
                await using var fileStream = File.Create(pluginPath);
                await stream.CopyToAsync(fileStream);
            }

            await ShowInfoDialogAsync("Plugin Installed", "Shared memory plugin installed. Start or restart LMU to enable telemetry.");
        }
        catch (Exception ex)
        {
            StartupLogger.Error("Plugin install failed", ex);
            await ShowInfoDialogAsync("Plugin Install Failed", ex.Message);
        }
    }

    private async Task ShowInfoDialogAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var text = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(16)
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        okButton.Click += (_, _) => dialog.Close();

        var layout = new StackPanel { Spacing = 8 };
        layout.Children.Add(text);
        layout.Children.Add(okButton);
        dialog.Content = layout;

        await dialog.ShowDialog(this);
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Telemetry? telemetry = null;
            Scoring? scoring = null;
            Electronics electronics = new();
            var isConnected = false;
            var hasValidSample = false;

            try
            {
                if (_memoryReader == null && DateTime.UtcNow >= _nextReaderInitAttemptUtc)
                {
                    _memoryReader = new RF2MemoryReader(enableDMA: true);
                }

                if (_memoryReader != null)
                {
                    telemetry = await _memoryReader.GetTelemetryAsync();
                    scoring = await _memoryReader.GetScoringAsync();
                }

                isConnected = telemetry.HasValue && scoring.HasValue;
                if (isConnected && _memoryReader != null)
                {
                    electronics = _memoryReader.GetLMUElectronics();
                    hasValidSample = IsValidSample(telemetry.Value, scoring.Value);
                }
            }
            catch
            {
                isConnected = false;
                if (_memoryReader != null)
                {
                    _memoryReader.Dispose();
                    _memoryReader = null;
                }

                _nextReaderInitAttemptUtc = DateTime.UtcNow.AddSeconds(5);
            }

            if (isConnected && _viewModel.IsLogging && scoring.HasValue && telemetry.HasValue)
            {
                await _writerLock.WaitAsync(token);
                try
                {
                    EnsureWriter(scoring.Value);
                    if (_writer != null)
                    {
                        var entry = new TelemetryLogEntry
                        {
                            TimestampUtc = DateTime.UtcNow,
                            Telemetry = telemetry.Value,
                            Scoring = scoring.Value,
                            Electronics = electronics
                        };

                        await _writer.WriteSampleAsync(entry, token);
                        _dataPoints++;
                    }
                }
                finally
                {
                    _writerLock.Release();
                }
            }

            var sessionInfo = scoring.HasValue ? BuildSessionInfo(scoring.Value) : "Session: unavailable";
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _viewModel.IsConnected = isConnected;
                _viewModel.HasValidSample = hasValidSample;
                if (hasValidSample)
                {
                    _viewModel.LastSampleUtc = DateTime.UtcNow;
                }
                _viewModel.SessionInfo = sessionInfo;
                _viewModel.DataPointsCaptured = _dataPoints;
            }, DispatcherPriority.Background);

            try
            {
                await Task.Delay(PollInterval, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private void EnsureWriter(Scoring scoring)
    {
        if (_writer != null || string.IsNullOrWhiteSpace(_pendingOutputFilePath))
        {
            return;
        }

        var metadata = BuildSessionMetadata(scoring);
        _writer = new TelemetryFileWriter(_pendingOutputFilePath, metadata, _jsonOptions);
    }

    private static SessionMetadata BuildSessionMetadata(Scoring scoring)
    {
        var track = ByteStringHelper.FromNullTerminated(scoring.ScoringInfo.TrackName);
        var sessionType = scoring.ScoringInfo.Session.ToString();
        var car = GetPlayerCar(scoring);

        return new SessionMetadata
        {
            StartTimeUtc = DateTime.UtcNow,
            SessionType = string.IsNullOrWhiteSpace(sessionType) ? "Unknown" : sessionType,
            TrackName = string.IsNullOrWhiteSpace(track) ? "Unknown" : track,
            CarName = string.IsNullOrWhiteSpace(car) ? "Unknown" : car
        };
    }

    private static string BuildSessionInfo(Scoring scoring)
    {
        var track = ByteStringHelper.FromNullTerminated(scoring.ScoringInfo.TrackName);
        var sessionType = scoring.ScoringInfo.Session.ToString();
        var car = GetPlayerCar(scoring);

        if (string.IsNullOrWhiteSpace(track))
        {
            track = "Unknown";
        }

        if (string.IsNullOrWhiteSpace(car))
        {
            car = "Unknown";
        }

        if (string.IsNullOrWhiteSpace(sessionType))
        {
            sessionType = "Unknown";
        }

        return $"Track: {track} | Car: {car} | Session: {sessionType}";
    }

    private static string GetPlayerCar(Scoring scoring)
    {
        var vehicles = scoring.Vehicles ?? Array.Empty<VehicleScoring>();
        var player = vehicles.FirstOrDefault(vehicle => (ControlEntity)vehicle.Control == ControlEntity.Player);
        return ByteStringHelper.FromNullTerminated(player.VehicleName);
    }

    private static bool IsValidSample(Telemetry telemetry, Scoring scoring)
    {
        var track = ByteStringHelper.FromNullTerminated(scoring.ScoringInfo.TrackName);
        var vehicleCount = telemetry.NumVehicles;
        var scoringVehicles = scoring.Vehicles?.Length ?? 0;
        return (!string.IsNullOrWhiteSpace(track)) && (vehicleCount > 0 || scoringVehicles > 0);
    }

    private async void OnBrowseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        var selected = folders.FirstOrDefault();
        if (selected?.Path is { } path)
        {
            _viewModel.OutputDirectory = path.LocalPath;
            StartupLogger.SetLogDirectory(_viewModel.OutputDirectory);
            SaveSettings();
        }
    }

    private void OnStartStopClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel.IsLogging)
        {
            _ = StopLoggingAsync();
        }
        else
        {
            StartLogging();
        }
    }

    private void StartLogging()
    {
        if (_viewModel.IsLogging)
        {
            return;
        }

        Directory.CreateDirectory(_viewModel.OutputDirectory);
        StartupLogger.SetLogDirectory(_viewModel.OutputDirectory);
        var fileName = $"lmu_telemetry_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        var path = Path.Combine(_viewModel.OutputDirectory, fileName);
        _pendingOutputFilePath = path;
        _viewModel.OutputFilePath = path;
        _dataPoints = 0;
        _viewModel.DataPointsCaptured = 0;
        _viewModel.IsLogging = true;
    }

    private async Task StopLoggingAsync()
    {
        if (!_viewModel.IsLogging && _writer == null)
        {
            return;
        }

        _viewModel.IsLogging = false;
        _pendingOutputFilePath = null;

        await _writerLock.WaitAsync();
        try
        {
            if (_writer != null)
            {
                await _writer.DisposeAsync();
                _writer = null;
            }
        }
        finally
        {
            _writerLock.Release();
        }
    }

    private async void OnClearClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await StopLoggingAsync();
        _dataPoints = 0;
        _viewModel.DataPointsCaptured = 0;
        _viewModel.SessionInfo = "Session: unavailable";
        _viewModel.OutputFilePath = string.Empty;
    }

    private void OnExitClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}