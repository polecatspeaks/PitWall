using System.Collections.Generic;
using System.Linq;
using GameReaderCommon;
using PitWall.Models;

namespace PitWall.Core
{
    /// <summary>
    /// Provides telemetry by reading SimHub PluginManager properties.
    /// </summary>
    public class SimHubTelemetryProvider : ITelemetryProvider
    {
        private readonly IPluginPropertyProvider _propertyProvider;

        public SimHubTelemetryProvider(IPluginPropertyProvider propertyProvider)
        {
            _propertyProvider = propertyProvider;
        }

        public bool IsGameRunning => !string.IsNullOrEmpty(_propertyProvider.GameName);

        public Telemetry GetCurrentTelemetry()
        {
            return new Telemetry
            {
                FuelRemaining = ReadDouble("DataCorePlugin.GameData.NewData.Fuel"),
                FuelCapacity = ReadDouble("DataCorePlugin.GameData.NewData.FuelMaxCapacity"),
                LastLapTime = ReadDouble("DataCorePlugin.GameData.NewData.LastLapTime"),
                BestLapTime = ReadDouble("DataCorePlugin.GameData.NewData.BestLapTime"),
                CurrentLap = (int)ReadDouble("DataCorePlugin.GameData.NewData.CompletedLaps"),
                IsInPit = ReadBool("DataCorePlugin.GameData.NewData.IsInPit"),
                IsLapValid = ReadBool("DataCorePlugin.GameData.NewData.CurrentLapIsValid"),
                TrackName = ReadString("DataCorePlugin.GameData.NewData.TrackName"),
                CarName = ReadString("DataCorePlugin.GameData.NewData.CarName"),
                IsGameRunning = IsGameRunning,

                TyreWearFrontLeft = ReadDouble("DataCorePlugin.GameData.NewData.TyreWearFrontLeft"),
                TyreWearFrontRight = ReadDouble("DataCorePlugin.GameData.NewData.TyreWearFrontRight"),
                TyreWearRearLeft = ReadDouble("DataCorePlugin.GameData.NewData.TyreWearRearLeft"),
                TyreWearRearRight = ReadDouble("DataCorePlugin.GameData.NewData.TyreWearRearRight"),

                Opponents = ReadOpponents(),
                PlayerPosition = (int)ReadDouble("DataCorePlugin.GameData.NewData.Position")
            };
        }

        private double ReadDouble(string propertyName)
        {
            var value = GetPropertyValue(propertyName);
            if (value is double d) return d;
            if (value is float f) return f;
            if (value is int i) return i;
            return 0.0;
        }

        private bool ReadBool(string propertyName)
        {
            var value = GetPropertyValue(propertyName);
            if (value is bool b) return b;
            return false;
        }

        private string ReadString(string propertyName)
        {
            var value = GetPropertyValue(propertyName);
            return value?.ToString() ?? string.Empty;
        }

        private object? GetPropertyValue(string propertyName)
        {
            return _propertyProvider.GetPropertyValue(propertyName);
        }

        private List<OpponentData> ReadOpponents()
        {
            var opponents = new List<OpponentData>();
            // Read up to 10 nearest opponents (SimHub exposes Opponents.0 to Opponents.59)
            for (int i = 0; i < 10; i++)
            {
                var carName = ReadString($"Opponents.{i}.CarName");
                if (string.IsNullOrEmpty(carName)) continue;

                opponents.Add(new OpponentData
                {
                    Position = (int)ReadDouble($"Opponents.{i}.Position"),
                    CarName = carName,
                    GapSeconds = ReadDouble($"Opponents.{i}.GapSeconds"),
                    IsInPitLane = ReadBool($"Opponents.{i}.IsInPitLane"),
                    BestLapTime = ReadDouble($"Opponents.{i}.BestLapTime")
                });
            }
            return opponents;
        }
    }
}
