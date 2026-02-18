using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Agent.Models;
using PitWall.Agent.Services;
using PitWall.Core.Models;
using PitWall.Core.Storage;
using PitWall.Strategy;
using PitWall.Telemetry.Live.Models;
using Xunit;

namespace PitWall.Tests
{
    public class RaceContextProviderTests
    {
        [Fact]
        public async Task BuildAsync_NoSessionId_ReturnsEmptyContext()
        {
            var writer = new FakeTelemetryWriter();
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest { Context = new Dictionary<string, object>() };
            var context = await provider.BuildAsync(request);
            
            Assert.Equal(0, context.FuelLevel);
            Assert.Equal(0, context.CurrentLap);
        }

        [Fact]
        public async Task BuildAsync_SessionIdWithCaseSensitivity_UsesLowerCase()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> { ["sessionId"] = "session1" } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.Equal(50.0, context.FuelLevel);
        }

        [Fact]
        public async Task BuildAsync_SessionIdUpperCase_Works()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> { ["SessionId"] = "session1" } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.Equal(50.0, context.FuelLevel);
        }

        [Fact]
        public async Task BuildAsync_NoSamples_ReturnsEmptyContext()
        {
            var writer = new FakeTelemetryWriter();
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> { ["sessionId"] = "empty" } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.Equal(0, context.FuelLevel);
        }

        [Fact]
        public async Task BuildAsync_WithAllContextValues_PopulatesAllFields()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> 
                { 
                    ["sessionId"] = "session1",
                    ["fuelCapacity"] = 100.0,
                    ["lastLapTime"] = 90.5,
                    ["bestLapTime"] = 89.0,
                    ["gapToAhead"] = 2.5,
                    ["gapToBehind"] = 1.2,
                    ["currentLap"] = 10.0,
                    ["totalLaps"] = 50.0,
                    ["position"] = 3.0,
                    ["optimalPitLap"] = 25.0,
                    ["trackName"] = "Spa",
                    ["carName"] = "Formula Pro",
                    ["currentWeather"] = "Rain",
                    ["trackTemp"] = 28.5,
                    ["inPitLane"] = true,
                    ["averageTireWear"] = 15.5,
                    ["tireLapsOnSet"] = 8.0
                } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.Equal(50.0, context.FuelLevel);
            Assert.Equal(100.0, context.FuelCapacity);
            Assert.Equal(90.5, context.LastLapTime);
            Assert.Equal(89.0, context.BestLapTime);
            Assert.Equal(2.5, context.GapToAhead);
            Assert.Equal(1.2, context.GapToBehind);
            Assert.Equal(10, context.CurrentLap);
            Assert.Equal(50, context.TotalLaps);
            Assert.Equal(3, context.Position);
            Assert.Equal(25, context.OptimalPitLap);
            Assert.Equal("Spa", context.TrackName);
            Assert.Equal("Formula Pro", context.CarName);
            Assert.Equal("Rain", context.CurrentWeather);
            Assert.Equal(28.5, context.TrackTemp);
            Assert.True(context.InPitLane);
            Assert.Equal(15.5, context.AverageTireWear);
            Assert.Equal(8, context.TireLapsOnSet);
        }

        [Fact]
        public async Task BuildAsync_FuelLapsRemaining_CalculatedCorrectly()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample(fuelLiters: 36.0));
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> { ["sessionId"] = "session1" } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.Equal(36.0, context.FuelLevel);
            Assert.Equal(20.0, context.FuelLapsRemaining);
        }

        [Fact]
        public async Task BuildAsync_DoubleValues_ParsedCorrectly()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> 
                { 
                    ["sessionId"] = "session1",
                    ["currentLap"] = 15.9
                } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.Equal(15, context.CurrentLap);
        }

        [Fact]
        public async Task BuildAsync_IntValues_ConvertedToDouble()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> 
                { 
                    ["sessionId"] = "session1",
                    ["currentLap"] = 10
                } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.Equal(10, context.CurrentLap);
        }

        [Fact]
        public async Task BuildAsync_FloatValues_ConvertedToDouble()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> 
                { 
                    ["sessionId"] = "session1",
                    ["trackTemp"] = 25.5f
                } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.Equal(25.5, context.TrackTemp, 1);
        }

        [Fact]
        public async Task BuildAsync_LongValues_ConvertedToDouble()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> 
                { 
                    ["sessionId"] = "session1",
                    ["totalLaps"] = 100L
                } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.Equal(100, context.TotalLaps);
        }

        [Fact]
        public async Task BuildAsync_StringDoubleValues_ParsedCorrectly()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> 
                { 
                    ["sessionId"] = "session1",
                    ["lastLapTime"] = "92.5"
                } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.Equal(92.5, context.LastLapTime);
        }

        [Fact]
        public async Task BuildAsync_InvalidStringDouble_ReturnsNull()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> 
                { 
                    ["sessionId"] = "session1",
                    ["lastLapTime"] = "invalid"
                } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.Equal(0.0, context.LastLapTime);
        }

        [Fact]
        public async Task BuildAsync_BoolValues_ParsedCorrectly()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> 
                { 
                    ["sessionId"] = "session1",
                    ["inPitLane"] = true
                } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.True(context.InPitLane);
        }

        [Fact]
        public async Task BuildAsync_StringBoolValues_ParsedCorrectly()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> 
                { 
                    ["sessionId"] = "session1",
                    ["inPitLane"] = "true"
                } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.True(context.InPitLane);
        }

        [Fact]
        public async Task BuildAsync_InvalidStringBool_ReturnsNull()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> 
                { 
                    ["sessionId"] = "session1",
                    ["inPitLane"] = "maybe"
                } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.False(context.InPitLane);
        }

        [Fact]
        public async Task BuildAsync_MissingValues_UsesDefaults()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> { ["sessionId"] = "session1" } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.Equal(string.Empty, context.TrackName);
            Assert.Equal("Clear", context.CurrentWeather);
            Assert.Equal(0.0, context.TrackTemp);
        }

        [Fact]
        public async Task BuildAsync_CancellationToken_ThrowsWhenCancelled()
        {
            var writer = new FakeTelemetryWriter();
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var cts = new CancellationTokenSource();
            cts.Cancel();
            
            var request = new AgentRequest { Context = new Dictionary<string, object>() };
            
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await provider.BuildAsync(request, cts.Token));
        }

        [Fact]
        public async Task BuildAsync_StrategyConfidence_SetFromEngine()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample());
            var strategyEngine = new StrategyEngine();
            var provider = new RaceContextProvider(writer, strategyEngine);
            
            var request = new AgentRequest 
            { 
                Context = new Dictionary<string, object> { ["sessionId"] = "session1" } 
            };
            var context = await provider.BuildAsync(request);
            
            Assert.True(context.StrategyConfidence >= 0.0);
            Assert.True(context.StrategyConfidence <= 1.0);
        }

        private static TelemetrySample CreateSample(double fuelLiters = 50.0)
        {
            return new TelemetrySample(
                DateTime.UtcNow,
                100.0,
                new[] { 80.0, 81.0, 82.0, 83.0 },
                fuelLiters,
                0.0,
                0.5,
                0.0);
        }

        private class FakeTelemetryWriter : ITelemetryWriter
        {
            private readonly Dictionary<string, List<TelemetrySample>> _samples = new();

            public void AddSample(string sessionId, TelemetrySample sample)
            {
                if (!_samples.ContainsKey(sessionId))
                {
                    _samples[sessionId] = new List<TelemetrySample>();
                }
                _samples[sessionId].Add(sample);
            }

            public void WriteSamples(string sessionId, List<TelemetrySample> samples)
            {
                if (!_samples.ContainsKey(sessionId))
                {
                    _samples[sessionId] = new List<TelemetrySample>();
                }
                _samples[sessionId].AddRange(samples);
            }
            
            public List<TelemetrySample> GetSamples(string sessionId)
            {
                return _samples.TryGetValue(sessionId, out var samples) 
                    ? samples 
                    : new List<TelemetrySample>();
            }
        }

        #region Live telemetry tests

        [Fact]
        public async Task BuildAsync_WithLiveProvider_PrefersLiveData()
        {
            var writer = new FakeTelemetryWriter();
            var engine = new StrategyEngine();
            var liveProvider = new FakeLiveTelemetryProvider(CreateLiveSnapshot());
            var provider = new RaceContextProvider(writer, engine, liveProvider);

            var request = new AgentRequest { Context = new Dictionary<string, object>() };
            var context = await provider.BuildAsync(request);

            Assert.True(context.IsLiveData);
            Assert.Equal(42.5, context.FuelLevel);
            Assert.Equal("Spa", context.TrackName);
            Assert.Equal("Formula Pro", context.CarName);
        }

        [Fact]
        public async Task BuildAsync_LiveProvider_MapsVehicleMotion()
        {
            var snapshot = CreateLiveSnapshot();
            snapshot.PlayerVehicle!.Speed = 285.3;
            snapshot.PlayerVehicle!.Rpm = 8500;
            snapshot.PlayerVehicle!.Gear = 5;

            var provider = CreateProviderWithLive(snapshot);
            var request = new AgentRequest { Context = new Dictionary<string, object>() };
            var context = await provider.BuildAsync(request);

            Assert.Equal(285.3, context.Speed);
            Assert.Equal(8500, context.Rpm);
            Assert.Equal(5, context.Gear);
        }

        [Fact]
        public async Task BuildAsync_LiveProvider_MapsTireData()
        {
            var snapshot = CreateLiveSnapshot();
            snapshot.PlayerVehicle!.Wheels = new WheelData[]
            {
                new() { TempMid = 90.0, Wear = 0.15, BrakeTemp = 400 },
                new() { TempMid = 92.0, Wear = 0.18, BrakeTemp = 420 },
                new() { TempMid = 88.0, Wear = 0.12, BrakeTemp = 380 },
                new() { TempMid = 91.0, Wear = 0.16, BrakeTemp = 410 },
            };

            var provider = CreateProviderWithLive(snapshot);
            var request = new AgentRequest { Context = new Dictionary<string, object>() };
            var context = await provider.BuildAsync(request);

            Assert.Equal(new[] { 90.0, 92.0, 88.0, 91.0 }, context.TireTemps);
            Assert.Equal(new[] { 0.15, 0.18, 0.12, 0.16 }, context.TireWear);
            Assert.Equal(new[] { 400.0, 420.0, 380.0, 410.0 }, context.BrakeTemps);
            Assert.Equal(0.1525, context.AverageTireWear, 4);
        }

        [Fact]
        public async Task BuildAsync_LiveProvider_MapsScoringData()
        {
            var snapshot = CreateLiveSnapshot();
            snapshot.Scoring = new ScoringInfo
            {
                YellowFlagState = 2,
                Vehicles = new List<VehicleScoringInfo>
                {
                    new() { VehicleId = 1, Place = 3, LapNumber = 12, LastLapTime = 91.2, BestLapTime = 89.5, TimeBehindNext = 1.8, PitState = 0 },
                    new() { VehicleId = 2, Place = 4, TimeBehindNext = 2.1 },
                }
            };

            var provider = CreateProviderWithLive(snapshot);
            var request = new AgentRequest { Context = new Dictionary<string, object>() };
            var context = await provider.BuildAsync(request);

            Assert.Equal(3, context.Position);
            Assert.Equal(12, context.CurrentLap);
            Assert.Equal(91.2, context.LastLapTime);
            Assert.Equal(89.5, context.BestLapTime);
            Assert.Equal(1.8, context.GapToAhead);
            Assert.Equal(2.1, context.GapToBehind);
            Assert.False(context.InPitLane);
            Assert.Equal(2, context.YellowFlagState);
        }

        [Fact]
        public async Task BuildAsync_LiveProvider_InPitLane_DetectedFromScoring()
        {
            var snapshot = CreateLiveSnapshot();
            snapshot.Scoring = new ScoringInfo
            {
                Vehicles = new List<VehicleScoringInfo>
                {
                    new() { VehicleId = 1, PitState = 1 },
                }
            };

            var provider = CreateProviderWithLive(snapshot);
            var request = new AgentRequest { Context = new Dictionary<string, object>() };
            var context = await provider.BuildAsync(request);

            Assert.True(context.InPitLane);
        }

        [Fact]
        public async Task BuildAsync_LiveProvider_FallsBackToContextForMissingFields()
        {
            var snapshot = CreateLiveSnapshot();
            var provider = CreateProviderWithLive(snapshot);

            var request = new AgentRequest
            {
                Context = new Dictionary<string, object>
                {
                    ["totalLaps"] = 50.0,
                    ["optimalPitLap"] = 25.0,
                    ["currentWeather"] = "Rain",
                    ["trackTemp"] = 32.0,
                    ["tireLapsOnSet"] = 8.0,
                }
            };
            var context = await provider.BuildAsync(request);

            Assert.Equal(50, context.TotalLaps);
            Assert.Equal(25, context.OptimalPitLap);
            Assert.Equal("Rain", context.CurrentWeather);
            Assert.Equal(32.0, context.TrackTemp);
            Assert.Equal(8, context.TireLapsOnSet);
        }

        [Fact]
        public async Task BuildAsync_LiveProviderReturnsNull_FallsBackToStored()
        {
            var writer = new FakeTelemetryWriter();
            writer.AddSample("session1", CreateSample(fuelLiters: 30.0));
            var engine = new StrategyEngine();
            var liveProvider = new FakeLiveTelemetryProvider(null);
            var provider = new RaceContextProvider(writer, engine, liveProvider);

            var request = new AgentRequest
            {
                Context = new Dictionary<string, object> { ["sessionId"] = "session1" }
            };
            var context = await provider.BuildAsync(request);

            Assert.False(context.IsLiveData);
            Assert.Equal(30.0, context.FuelLevel);
        }

        [Fact]
        public async Task BuildAsync_LiveSnapshotNoPlayerVehicle_FallsBackToStored()
        {
            var snapshot = new TelemetrySnapshot { PlayerVehicle = null };
            var writer = new FakeTelemetryWriter();
            writer.AddSample("s1", CreateSample(fuelLiters: 40.0));
            var engine = new StrategyEngine();
            var liveProvider = new FakeLiveTelemetryProvider(snapshot);
            var provider = new RaceContextProvider(writer, engine, liveProvider);

            var request = new AgentRequest
            {
                Context = new Dictionary<string, object> { ["sessionId"] = "s1" }
            };
            var context = await provider.BuildAsync(request);

            Assert.False(context.IsLiveData);
            Assert.Equal(40.0, context.FuelLevel);
        }

        [Fact]
        public async Task BuildAsync_LiveProvider_FuelLapsRemainingCalculated()
        {
            var snapshot = CreateLiveSnapshot();
            snapshot.PlayerVehicle!.Fuel = 36.0;
            var provider = CreateProviderWithLive(snapshot);

            var request = new AgentRequest { Context = new Dictionary<string, object>() };
            var context = await provider.BuildAsync(request);

            Assert.Equal(36.0, context.FuelLevel);
            Assert.Equal(20.0, context.FuelLapsRemaining);
        }

        [Fact]
        public async Task BuildAsync_LiveProvider_NullScoring_HandledGracefully()
        {
            var snapshot = CreateLiveSnapshot();
            snapshot.Scoring = null;
            var provider = CreateProviderWithLive(snapshot);

            var request = new AgentRequest { Context = new Dictionary<string, object>() };
            var context = await provider.BuildAsync(request);

            Assert.True(context.IsLiveData);
            Assert.Equal(0, context.Position);
            Assert.Equal(0, context.CurrentLap);
        }

        [Fact]
        public async Task BuildAsync_LiveProvider_GapBehind_NobodyBehind_ReturnsZero()
        {
            var snapshot = CreateLiveSnapshot();
            snapshot.Scoring = new ScoringInfo
            {
                Vehicles = new List<VehicleScoringInfo>
                {
                    new() { VehicleId = 1, Place = 5 }, // player is last
                }
            };
            var provider = CreateProviderWithLive(snapshot);

            var request = new AgentRequest { Context = new Dictionary<string, object>() };
            var context = await provider.BuildAsync(request);

            Assert.Equal(0.0, context.GapToBehind);
        }

        [Fact]
        public async Task BuildAsync_LiveProvider_StrategyConfidence_FromEngine()
        {
            var snapshot = CreateLiveSnapshot();
            var writer = new FakeTelemetryWriter();
            writer.AddSample("live-session", CreateSample());
            var engine = new StrategyEngine();
            var liveProvider = new FakeLiveTelemetryProvider(snapshot);
            var provider = new RaceContextProvider(writer, engine, liveProvider);

            var request = new AgentRequest
            {
                Context = new Dictionary<string, object> { ["sessionId"] = "live-session" }
            };
            var context = await provider.BuildAsync(request);

            Assert.True(context.IsLiveData);
            Assert.True(context.StrategyConfidence >= 0.0);
            Assert.True(context.StrategyConfidence <= 1.0);
        }

        private static TelemetrySnapshot CreateLiveSnapshot()
        {
            return new TelemetrySnapshot
            {
                Timestamp = DateTime.UtcNow,
                SessionId = "live-session-1",
                Session = new SessionInfo
                {
                    TrackName = "Spa",
                    CarName = "Formula Pro",
                    NumVehicles = 20,
                    TrackLength = 7004.0,
                },
                PlayerVehicle = new VehicleTelemetry
                {
                    VehicleId = 1,
                    IsPlayer = true,
                    Speed = 250.0,
                    Rpm = 7500,
                    Gear = 4,
                    Fuel = 42.5,
                    Wheels = new WheelData[]
                    {
                        new() { TempMid = 85, Wear = 0.10, BrakeTemp = 350 },
                        new() { TempMid = 86, Wear = 0.11, BrakeTemp = 360 },
                        new() { TempMid = 84, Wear = 0.09, BrakeTemp = 340 },
                        new() { TempMid = 85, Wear = 0.10, BrakeTemp = 350 },
                    },
                },
                Scoring = new ScoringInfo
                {
                    Vehicles = new List<VehicleScoringInfo>
                    {
                        new() { VehicleId = 1, Place = 5, LapNumber = 10, LastLapTime = 92.5, BestLapTime = 90.1, TimeBehindNext = 2.0, PitState = 0 },
                    }
                },
            };
        }

        private static RaceContextProvider CreateProviderWithLive(TelemetrySnapshot snapshot)
        {
            return new RaceContextProvider(
                new FakeTelemetryWriter(),
                new StrategyEngine(),
                new FakeLiveTelemetryProvider(snapshot));
        }

        private class FakeLiveTelemetryProvider : ILiveTelemetryProvider
        {
            private readonly TelemetrySnapshot? _snapshot;
            public FakeLiveTelemetryProvider(TelemetrySnapshot? snapshot) => _snapshot = snapshot;
            public TelemetrySnapshot? GetLatestSnapshot() => _snapshot;
        }

        #endregion
    }

    public class LiveTelemetryProviderAdapterTests
    {
        [Fact]
        public void GetLatestSnapshot_ReturnsPipelineSnapshot()
        {
            var source = new FakeDataSource();
            var pipeline = new PitWall.Telemetry.Live.Services.TelemetryPipelineService(source);
            var adapter = new LiveTelemetryProviderAdapter(pipeline);

            // Initially null
            Assert.Null(adapter.GetLatestSnapshot());
        }

        [Fact]
        public void Constructor_NullPipeline_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new LiveTelemetryProviderAdapter(null!));
        }

        private class FakeDataSource : PitWall.Telemetry.Live.Services.ITelemetryDataSource
        {
            public TelemetrySnapshot? Read() => null;
            public bool IsAvailable() => false;
            public Task<TelemetrySnapshot?> ReadSnapshotAsync() => Task.FromResult<TelemetrySnapshot?>(null);
        }
    }
}
