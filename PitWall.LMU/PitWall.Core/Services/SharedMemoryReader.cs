using System;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Core.Models;

namespace PitWall.Core.Services
{
    public class SharedMemoryReader : ISharedMemoryReader
    {
        private const string MEMORY_NAME = "Local\\LMU_Telemetry";
        private const int MEMORY_SIZE = 8192;

        private CancellationTokenSource? _cts;
        private TelemetrySample? _latest;

        public bool IsConnected { get; private set; }
        public int PollingFrequency { get; private set; } = 100;

        public event EventHandler<TelemetrySample>? OnTelemetryUpdate;
        public event EventHandler<bool>? OnConnectionStateChanged;
        public event EventHandler<Exception>? OnError;

        public TelemetrySample? GetLatestTelemetry() => _latest;

        public Task StartAsync(int frequencyHz = 100, CancellationToken token = default)
        {
            if (_cts != null) throw new InvalidOperationException("Already started");
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            PollingFrequency = frequencyHz;
            Task.Run(async () =>
            {
                try
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        // Placeholder: synthetic sample until real mapping implemented
                        var sample = new TelemetrySample(DateTime.UtcNow, 0, new double[4], 0, 0, 0, 0);
                        _latest = sample;
                        OnTelemetryUpdate?.Invoke(this, sample);
                        await Task.Delay(1000 / PollingFrequency, _cts.Token);
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, ex);
                }
            }, _cts.Token);
            IsConnected = true;
            OnConnectionStateChanged?.Invoke(this, true);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _cts?.Cancel();
            _cts = null;
            IsConnected = false;
            OnConnectionStateChanged?.Invoke(this, false);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _cts?.Cancel();
        }
    }
}
