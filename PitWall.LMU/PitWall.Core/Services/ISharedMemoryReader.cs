using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Core.Models;

namespace PitWall.Core.Services
{
    public interface ISharedMemoryReader : IDisposable
    {
        bool IsConnected { get; }
        int PollingFrequency { get; }

        Task StartAsync(int frequencyHz = 100, CancellationToken token = default);
        Task StopAsync();
        TelemetrySample? GetLatestTelemetry();
        IAsyncEnumerable<TelemetrySample> StreamSamples();

        event EventHandler<TelemetrySample> OnTelemetryUpdate;
        event EventHandler<bool> OnConnectionStateChanged;
        event EventHandler<Exception> OnError;
    }
}
