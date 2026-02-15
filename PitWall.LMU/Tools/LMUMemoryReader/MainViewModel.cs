using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace LMUMemoryReader;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _outputDirectory = string.Empty;
    private string _outputFilePath = string.Empty;
    private string _sessionInfo = "Session: unavailable";
    private bool _isLogging;
    private bool _isConnected;
    private long _dataPointsCaptured;
    private bool _hasValidSample;
    private DateTime? _lastSampleUtc;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => SetField(ref _outputDirectory, value);
    }

    public string OutputFilePath
    {
        get => _outputFilePath;
        set
        {
            if (SetField(ref _outputFilePath, value))
            {
                OnPropertyChanged(nameof(OutputFilePathText));
            }
        }
    }

    public string SessionInfo
    {
        get => _sessionInfo;
        set => SetField(ref _sessionInfo, value);
    }

    public bool IsLogging
    {
        get => _isLogging;
        set
        {
            if (SetField(ref _isLogging, value))
            {
                OnPropertyChanged(nameof(StartStopText));
            }
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetField(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(ConnectionStatusText));
                OnPropertyChanged(nameof(ConnectionStatusBrush));
            }
        }
    }

    public long DataPointsCaptured
    {
        get => _dataPointsCaptured;
        set
        {
            if (SetField(ref _dataPointsCaptured, value))
            {
                OnPropertyChanged(nameof(DataPointsText));
            }
        }
    }

    public bool HasValidSample
    {
        get => _hasValidSample;
        set
        {
            if (SetField(ref _hasValidSample, value))
            {
                OnPropertyChanged(nameof(DataValidityText));
                OnPropertyChanged(nameof(DataValidityBrush));
            }
        }
    }

    public DateTime? LastSampleUtc
    {
        get => _lastSampleUtc;
        set
        {
            if (SetField(ref _lastSampleUtc, value))
            {
                OnPropertyChanged(nameof(LastSampleText));
            }
        }
    }

    public string StartStopText => IsLogging ? "Stop Logging" : "Start Logging";

    public string ConnectionStatusText => IsConnected ? "Connected to LMU shared memory" : "Disconnected";

    public IBrush ConnectionStatusBrush => IsConnected ? Brushes.ForestGreen : Brushes.IndianRed;

    public string DataValidityText => HasValidSample ? "Data: Valid" : "Data: Not valid";

    public IBrush DataValidityBrush => HasValidSample ? Brushes.ForestGreen : Brushes.Goldenrod;

    public string LastSampleText => LastSampleUtc.HasValue
        ? $"Last Sample (UTC): {LastSampleUtc:HH:mm:ss}"
        : "Last Sample (UTC): --";

    public string DataPointsText => $"Data Points: {DataPointsCaptured:N0}";

    public string OutputFilePathText => string.IsNullOrWhiteSpace(OutputFilePath)
        ? "Output File: (none)"
        : $"Output File: {OutputFilePath}";

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
