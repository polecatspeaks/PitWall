using System.Diagnostics;

namespace PitWall.JsonAnalyzer;

/// <summary>
/// Console progress bar with ETA estimation.
/// Updates at most every 100 samples or 1 second to avoid console spam.
/// </summary>
public sealed class ProgressReporter
{
    private readonly long _totalBytes;
    private readonly Stopwatch _stopwatch = new();
    private long _lastReportedSamples;
    private DateTime _lastReportTime = DateTime.MinValue;
    private const int BarWidth = 30;
    private const int MinSampleInterval = 100;
    private static readonly TimeSpan MinTimeInterval = TimeSpan.FromSeconds(1);

    public long SamplesProcessed { get; private set; }
    public long BytesProcessed { get; private set; }

    public ProgressReporter(long totalBytes)
    {
        _totalBytes = totalBytes;
    }

    public void Start()
    {
        _stopwatch.Start();
        Console.CursorVisible = false;
    }

    public void Update(long bytesProcessed, long samplesProcessed)
    {
        BytesProcessed = bytesProcessed;
        SamplesProcessed = samplesProcessed;

        var now = DateTime.UtcNow;
        var sampleDelta = samplesProcessed - _lastReportedSamples;
        var timeDelta = now - _lastReportTime;

        if (sampleDelta < MinSampleInterval && timeDelta < MinTimeInterval)
            return;

        _lastReportedSamples = samplesProcessed;
        _lastReportTime = now;

        Render();
    }

    public void Finish()
    {
        _stopwatch.Stop();
        Render();
        Console.WriteLine();
        Console.CursorVisible = true;

        var elapsed = _stopwatch.Elapsed;
        var mbPerSec = _totalBytes / 1024.0 / 1024.0 / elapsed.TotalSeconds;
        Console.WriteLine($"Completed in {FormatTime(elapsed)} | {mbPerSec:F1} MB/s | {SamplesProcessed:N0} samples");
    }

    private void Render()
    {
        double fraction = _totalBytes > 0 ? (double)BytesProcessed / _totalBytes : 0;
        fraction = Math.Clamp(fraction, 0, 1);

        int filled = (int)(fraction * BarWidth);
        string bar = new string('#', filled) + new string('.', BarWidth - filled);

        string eta = "calculating...";
        if (_stopwatch.Elapsed.TotalSeconds > 2 && fraction > 0.001)
        {
            var remaining = TimeSpan.FromSeconds(_stopwatch.Elapsed.TotalSeconds / fraction * (1 - fraction));
            eta = FormatTime(remaining);
        }

        string bytesStr = FormatBytes(BytesProcessed);
        string totalStr = FormatBytes(_totalBytes);

        Console.Write($"\r[{bar}] {fraction * 100:F1}% | {bytesStr} / {totalStr} | ~{eta} remaining | {SamplesProcessed:N0} samples");
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):F2} GB",
            >= 1024L * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            >= 1024L => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes} B"
        };
    }

    private static string FormatTime(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Minutes}:{ts.Seconds:D2}";
    }
}
