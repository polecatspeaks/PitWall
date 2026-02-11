using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Agent.Models;
using PitWall.Agent.Services;
using PitWall.Core.Models;
using PitWall.Core.Storage;
using PitWall.Strategy;
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
    }
}
