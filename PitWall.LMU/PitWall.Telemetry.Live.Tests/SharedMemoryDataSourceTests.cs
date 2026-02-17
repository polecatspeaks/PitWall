using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using PitWall.Core.Models;
using PitWall.Core.Services;
using PitWall.Telemetry.Live.Models;
using PitWall.Telemetry.Live.Services;
using Xunit;

namespace PitWall.Telemetry.Live.Tests
{
    /// <summary>
    /// TDD tests for SharedMemoryDataSource — the bridge between
    /// ISharedMemoryReader (PitWall.Core) and ITelemetryDataSource (PitWall.Telemetry.Live).
    /// Written FIRST per TDD RED phase.
    /// </summary>
    public class SharedMemoryDataSourceTests
    {
        #region IsAvailable

        [Fact]
        public void IsAvailable_WhenReaderIsConnected_ReturnsTrue()
        {
            // Arrange
            var mockReader = new Mock<ISharedMemoryReader>();
            mockReader.Setup(r => r.IsConnected).Returns(true);
            var source = new SharedMemoryDataSource(mockReader.Object);

            // Act
            var result = source.IsAvailable();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsAvailable_WhenReaderIsNotConnected_ReturnsFalse()
        {
            // Arrange
            var mockReader = new Mock<ISharedMemoryReader>();
            mockReader.Setup(r => r.IsConnected).Returns(false);
            var source = new SharedMemoryDataSource(mockReader.Object);

            // Act
            var result = source.IsAvailable();

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Constructor

        [Fact]
        public void Constructor_WithNullReader_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SharedMemoryDataSource(null!));
        }

        [Fact]
        public void Constructor_ImplementsITelemetryDataSource()
        {
            // Arrange
            var mockReader = new Mock<ISharedMemoryReader>();

            // Act
            var source = new SharedMemoryDataSource(mockReader.Object);

            // Assert
            Assert.IsAssignableFrom<ITelemetryDataSource>(source);
        }

        #endregion

        #region ReadSnapshotAsync — basic

        [Fact]
        public async Task ReadSnapshotAsync_WhenNoLatestTelemetry_ReturnsNull()
        {
            // Arrange
            var mockReader = new Mock<ISharedMemoryReader>();
            mockReader.Setup(r => r.IsConnected).Returns(true);
            mockReader.Setup(r => r.GetLatestTelemetry()).Returns((TelemetrySample?)null);
            var source = new SharedMemoryDataSource(mockReader.Object);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.Null(snapshot);
        }

        [Fact]
        public async Task ReadSnapshotAsync_WithValidSample_ReturnsSnapshot()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var sample = new TelemetrySample(
                now, SpeedKph: 250.5, TyreTempsC: new[] { 95.0, 96.0, 94.0, 95.0 },
                FuelLiters: 42.0, Brake: 0.7, Throttle: 0.0, Steering: 0.05);
            var mockReader = new Mock<ISharedMemoryReader>();
            mockReader.Setup(r => r.IsConnected).Returns(true);
            mockReader.Setup(r => r.GetLatestTelemetry()).Returns(sample);
            var source = new SharedMemoryDataSource(mockReader.Object);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.True(snapshot.Timestamp <= DateTime.UtcNow);
            Assert.True(snapshot.Timestamp >= now.AddSeconds(-1));
        }

        #endregion

        #region ReadSnapshotAsync — player vehicle mapping

        [Fact]
        public async Task ReadSnapshotAsync_MapsSpeedToPlayerVehicle()
        {
            // Arrange
            var sample = CreateSample(speed: 250.5);
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(250.5, snapshot.PlayerVehicle.Speed);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsFuelToPlayerVehicle()
        {
            // Arrange
            var sample = CreateSample(fuel: 42.0);
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(42.0, snapshot.PlayerVehicle.Fuel);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsBrakeToPlayerVehicle()
        {
            // Arrange
            var sample = CreateSample(brake: 0.73);
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(0.73, snapshot.PlayerVehicle.Brake, precision: 2);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsThrottleToPlayerVehicle()
        {
            // Arrange
            var sample = CreateSample(throttle: 0.95);
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(0.95, snapshot.PlayerVehicle.Throttle, precision: 2);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsSteeringToPlayerVehicle()
        {
            // Arrange
            var sample = CreateSample(steering: -0.15);
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(-0.15, snapshot.PlayerVehicle.Steering, precision: 2);
        }

        [Fact]
        public async Task ReadSnapshotAsync_PlayerVehicleIsMarkedAsPlayer()
        {
            // Arrange
            var sample = CreateSample();
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.True(snapshot.PlayerVehicle.IsPlayer);
            Assert.Equal(0, snapshot.PlayerVehicle.VehicleId);
        }

        #endregion

        #region ReadSnapshotAsync — wheel/tyre mapping

        [Fact]
        public async Task ReadSnapshotAsync_MapsTyreTempsToWheels()
        {
            // Arrange
            var tyreTemps = new[] { 95.0, 96.0, 94.0, 97.0 };
            var sample = new TelemetrySample(
                DateTime.UtcNow, SpeedKph: 200.0, TyreTempsC: tyreTemps,
                FuelLiters: 50.0, Brake: 0.0, Throttle: 0.5, Steering: 0.0);
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot?.PlayerVehicle);
            var wheels = snapshot.PlayerVehicle.Wheels;
            Assert.Equal(4, wheels.Length);

            // TempMid gets the value from the single tyre temp per wheel
            Assert.Equal(95.0, wheels[0].TempMid);
            Assert.Equal(96.0, wheels[1].TempMid);
            Assert.Equal(94.0, wheels[2].TempMid);
            Assert.Equal(97.0, wheels[3].TempMid);
        }

        [Fact]
        public async Task ReadSnapshotAsync_HandlesNullTyreTemps()
        {
            // Arrange — TelemetrySample could have null tyre array
            var sample = new TelemetrySample(
                DateTime.UtcNow, SpeedKph: 200.0, TyreTempsC: null!,
                FuelLiters: 50.0, Brake: 0.0, Throttle: 0.5, Steering: 0.0);
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(4, snapshot.PlayerVehicle.Wheels.Length);
            // Temps should default to 0 when null
            Assert.Equal(0.0, snapshot.PlayerVehicle.Wheels[0].TempMid);
        }

        [Fact]
        public async Task ReadSnapshotAsync_HandlesShortTyreTempsArray()
        {
            // Arrange — edge case: fewer than 4 elements
            var sample = new TelemetrySample(
                DateTime.UtcNow, SpeedKph: 200.0, TyreTempsC: new[] { 95.0, 96.0 },
                FuelLiters: 50.0, Brake: 0.0, Throttle: 0.5, Steering: 0.0);
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(4, snapshot.PlayerVehicle.Wheels.Length);
            Assert.Equal(95.0, snapshot.PlayerVehicle.Wheels[0].TempMid);
            Assert.Equal(96.0, snapshot.PlayerVehicle.Wheels[1].TempMid);
            Assert.Equal(0.0, snapshot.PlayerVehicle.Wheels[2].TempMid); // default
            Assert.Equal(0.0, snapshot.PlayerVehicle.Wheels[3].TempMid); // default
        }

        #endregion

        #region ReadSnapshotAsync — AllVehicles

        [Fact]
        public async Task ReadSnapshotAsync_PlayerAddedToAllVehicles()
        {
            // Arrange
            var sample = CreateSample();
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.Single(snapshot.AllVehicles);
            Assert.True(snapshot.AllVehicles[0].IsPlayer);
        }

        #endregion

        #region ReadSnapshotAsync — session info

        [Fact]
        public async Task ReadSnapshotAsync_SetsSessionId()
        {
            // Arrange
            var sample = CreateSample();
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.NotEmpty(snapshot.SessionId);
        }

        [Fact]
        public async Task ReadSnapshotAsync_SessionIdIsStableAcrossCalls()
        {
            // Arrange
            var sample = CreateSample();
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot1 = await source.ReadSnapshotAsync();
            var snapshot2 = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot1);
            Assert.NotNull(snapshot2);
            Assert.Equal(snapshot1.SessionId, snapshot2.SessionId);
        }

        [Fact]
        public async Task ReadSnapshotAsync_SetsBasicSessionInfo()
        {
            // Arrange
            var sample = CreateSample();
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot?.Session);
            Assert.Equal(1, snapshot.Session.NumVehicles);
        }

        #endregion

        #region ReadSnapshotAsync — scoring info

        [Fact]
        public async Task ReadSnapshotAsync_InitializesScoringInfo()
        {
            // Arrange
            var sample = CreateSample();
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot?.Scoring);
            Assert.Equal(1, snapshot.Scoring.NumVehicles);
            Assert.Equal(3, snapshot.Scoring.SectorFlags.Length);
        }

        #endregion

        #region ReadSnapshotAsync — lat/lon mapping

        [Fact]
        public async Task ReadSnapshotAsync_MapsLatLonToPosition()
        {
            // Arrange
            var sample = new TelemetrySample(
                DateTime.UtcNow, SpeedKph: 200.0, TyreTempsC: new[] { 90.0, 90.0, 90.0, 90.0 },
                FuelLiters: 50.0, Brake: 0.0, Throttle: 0.5, Steering: 0.0)
            {
                Latitude = 51.501,
                Longitude = -0.142
            };
            var source = CreateSourceWithSample(sample);

            // Act
            var snapshot = await source.ReadSnapshotAsync();

            // Assert
            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(51.501, snapshot.PlayerVehicle.PosX, precision: 3);
            Assert.Equal(-0.142, snapshot.PlayerVehicle.PosZ, precision: 3);
        }

        #endregion

        #region Dispose

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            // Arrange
            var mockReader = new Mock<ISharedMemoryReader>();
            var source = new SharedMemoryDataSource(mockReader.Object);

            // Act & Assert — should not throw
            var ex = Record.Exception(() => source.Dispose());
            Assert.Null(ex);
        }

        #endregion

        #region ReadSnapshotAsync — engine/drivetrain mapping

        [Fact]
        public async Task ReadSnapshotAsync_MapsRpmToPlayerVehicle()
        {
            var sample = CreateSample() with { Rpm = 8500.0 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(8500.0, snapshot.PlayerVehicle.Rpm);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsGearToPlayerVehicle()
        {
            var sample = CreateSample() with { Gear = 4 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(4, snapshot.PlayerVehicle.Gear);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsFuelToPlayerVehicle_Expanded()
        {
            var sample = CreateSample(fuel: 65.0) with { FuelCapacity = 110.0 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(65.0, snapshot.PlayerVehicle.Fuel);
        }

        #endregion

        #region ReadSnapshotAsync — damage mapping

        [Fact]
        public async Task ReadSnapshotAsync_MapsLastImpactMagnitude()
        {
            var sample = CreateSample() with { LastImpactMagnitude = 15000.0 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(15000.0, snapshot.PlayerVehicle.LastImpactMagnitude);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsLastImpactTime()
        {
            var sample = CreateSample() with { LastImpactTime = 123.45 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(123.45, snapshot.PlayerVehicle.LastImpactTime);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsDentSeverity()
        {
            var dentBase64 = Convert.ToBase64String(new byte[] { 0, 10, 20, 0, 0, 30, 0, 0 });
            var sample = CreateSample() with { DentSeverity = dentBase64 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.NotNull(snapshot.PlayerVehicle.DentSeverity);
            Assert.Equal(8, snapshot.PlayerVehicle.DentSeverity.Length);
            Assert.Equal(10, snapshot.PlayerVehicle.DentSeverity[1]);
            Assert.Equal(20, snapshot.PlayerVehicle.DentSeverity[2]);
            Assert.Equal(30, snapshot.PlayerVehicle.DentSeverity[5]);
        }

        [Fact]
        public async Task ReadSnapshotAsync_NullDentSeverity_SetsNullOnVehicle()
        {
            var sample = CreateSample() with { DentSeverity = null };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Null(snapshot.PlayerVehicle.DentSeverity);
        }

        #endregion

        #region ReadSnapshotAsync — tyre condition mapping

        [Fact]
        public async Task ReadSnapshotAsync_MapsTyreWearToWheels()
        {
            var sample = CreateSample() with { TyreWear = new[] { 0.95, 0.90, 0.85, 0.80 } };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(0.95, snapshot.PlayerVehicle.Wheels[0].Wear);
            Assert.Equal(0.90, snapshot.PlayerVehicle.Wheels[1].Wear);
            Assert.Equal(0.85, snapshot.PlayerVehicle.Wheels[2].Wear);
            Assert.Equal(0.80, snapshot.PlayerVehicle.Wheels[3].Wear);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsTyrePressureToWheels()
        {
            var sample = CreateSample() with { TyrePressure = new[] { 26.5, 26.3, 25.8, 25.6 } };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(26.5, snapshot.PlayerVehicle.Wheels[0].Pressure, precision: 1);
            Assert.Equal(26.3, snapshot.PlayerVehicle.Wheels[1].Pressure, precision: 1);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsFlatTyresToWheels()
        {
            var sample = CreateSample() with { TyreFlat = new[] { false, true, false, false } };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.False(snapshot.PlayerVehicle.Wheels[0].Flat);
            Assert.True(snapshot.PlayerVehicle.Wheels[1].Flat);
            Assert.False(snapshot.PlayerVehicle.Wheels[2].Flat);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsDetachedWheels()
        {
            var sample = CreateSample() with { WheelDetached = new[] { false, false, true, false } };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.False(snapshot.PlayerVehicle.Wheels[0].Detached);
            Assert.True(snapshot.PlayerVehicle.Wheels[2].Detached);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsBrakeTempsToWheels()
        {
            var sample = CreateSample() with { BrakeTempsC = new[] { 450.0, 460.0, 400.0, 410.0 } };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(450.0, snapshot.PlayerVehicle.Wheels[0].BrakeTemp);
            Assert.Equal(460.0, snapshot.PlayerVehicle.Wheels[1].BrakeTemp);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsSuspDeflectionToWheels()
        {
            var sample = CreateSample() with { SuspDeflection = new[] { 0.05, 0.06, 0.04, 0.045 } };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(0.05, snapshot.PlayerVehicle.Wheels[0].SuspDeflection, precision: 3);
            Assert.Equal(0.06, snapshot.PlayerVehicle.Wheels[1].SuspDeflection, precision: 3);
        }

        [Fact]
        public async Task ReadSnapshotAsync_NullWheelArrays_DoesNotThrow()
        {
            var sample = CreateSample() with
            {
                TyreWear = null,
                TyrePressure = null,
                TyreFlat = null,
                WheelDetached = null,
                BrakeTempsC = null,
                SuspDeflection = null
            };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(0.0, snapshot.PlayerVehicle.Wheels[0].Wear);
            Assert.Equal(0.0, snapshot.PlayerVehicle.Wheels[0].Pressure);
            Assert.False(snapshot.PlayerVehicle.Wheels[0].Flat);
            Assert.False(snapshot.PlayerVehicle.Wheels[0].Detached);
        }

        #endregion

        #region ReadSnapshotAsync — session metadata mapping

        [Fact]
        public async Task ReadSnapshotAsync_MapsTrackName()
        {
            var sample = CreateSample() with { TrackName = "Monza" };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.Session);
            Assert.Equal("Monza", snapshot.Session.TrackName);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsSessionType()
        {
            var sample = CreateSample() with { SessionType = "Race" };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.Session);
            Assert.Equal("Race", snapshot.Session.SessionType);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsTrackLength()
        {
            var sample = CreateSample() with { TrackLength = 5793.0 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.Session);
            Assert.Equal(5793.0, snapshot.Session.TrackLength);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsNumVehiclesToSession()
        {
            var sample = CreateSample() with { NumVehicles = 25 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.Session);
            Assert.Equal(25, snapshot.Session.NumVehicles);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsDriverNameToPlayerScoring()
        {
            var sample = CreateSample() with { DriverName = "Max Verstappen" };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.Scoring);
            Assert.NotEmpty(snapshot.Scoring.Vehicles);
            Assert.Equal("Max Verstappen", snapshot.Scoring.Vehicles[0].DriverName);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsVehicleClassToCarName()
        {
            var sample = CreateSample() with { VehicleClass = "LMDh" };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.Session);
            Assert.Equal("LMDh", snapshot.Session.CarName);
        }

        #endregion

        #region ReadSnapshotAsync — timing/scoring mapping

        [Fact]
        public async Task ReadSnapshotAsync_MapsLapNumber()
        {
            var sample = CreateSample() with { LapNumber = 15 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.Scoring);
            Assert.NotEmpty(snapshot.Scoring.Vehicles);
            Assert.Equal(15, snapshot.Scoring.Vehicles[0].LapNumber);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsElapsedTime()
        {
            var sample = CreateSample() with { ElapsedTime = 1234.5 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.PlayerVehicle);
            Assert.Equal(1234.5, snapshot.PlayerVehicle.ElapsedTime);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsSectorFlags()
        {
            var sample = CreateSample() with { SectorFlags = new[] { 2, 0, 2 } };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.Scoring);
            Assert.Equal(2, snapshot.Scoring.SectorFlags[0]);
            Assert.Equal(0, snapshot.Scoring.SectorFlags[1]);
            Assert.Equal(2, snapshot.Scoring.SectorFlags[2]);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsYellowFlagState()
        {
            var sample = CreateSample() with { YellowFlagState = 1 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.Scoring);
            Assert.Equal(1, snapshot.Scoring.YellowFlagState);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsPlace()
        {
            var sample = CreateSample() with { Place = 3 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.Scoring);
            Assert.NotEmpty(snapshot.Scoring.Vehicles);
            Assert.Equal(3, snapshot.Scoring.Vehicles[0].Place);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsBestLapTime()
        {
            var sample = CreateSample() with { BestLapTime = 81.234 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.Scoring);
            Assert.NotEmpty(snapshot.Scoring.Vehicles);
            Assert.Equal(81.234, snapshot.Scoring.Vehicles[0].BestLapTime, precision: 3);
        }

        [Fact]
        public async Task ReadSnapshotAsync_MapsLastLapTime()
        {
            var sample = CreateSample() with { LastLapTime = 82.567 };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.Scoring);
            Assert.NotEmpty(snapshot.Scoring.Vehicles);
            Assert.Equal(82.567, snapshot.Scoring.Vehicles[0].LastLapTime, precision: 3);
        }

        #endregion

        #region ReadSnapshotAsync — multi-vehicle support

        [Fact]
        public async Task ReadSnapshotAsync_WithOtherVehicles_MapsToAllVehicles()
        {
            var otherCars = new List<VehicleSampleData>
            {
                new VehicleSampleData { VehicleId = 1, Speed = 230.0, Place = 2, DriverName = "Driver B" },
                new VehicleSampleData { VehicleId = 2, Speed = 220.0, Place = 3, DriverName = "Driver C" },
            };
            var sample = CreateSample() with { NumVehicles = 3, OtherVehicles = otherCars };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot);
            // Player + 2 other = 3 total
            Assert.Equal(3, snapshot.AllVehicles.Count);
            Assert.True(snapshot.AllVehicles[0].IsPlayer);
            Assert.False(snapshot.AllVehicles[1].IsPlayer);
            Assert.False(snapshot.AllVehicles[2].IsPlayer);
        }

        [Fact]
        public async Task ReadSnapshotAsync_OtherVehicles_MapsPosition()
        {
            var otherCars = new List<VehicleSampleData>
            {
                new VehicleSampleData { VehicleId = 1, PosX = 100.5, PosZ = 200.3 },
            };
            var sample = CreateSample() with { NumVehicles = 2, OtherVehicles = otherCars };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot);
            Assert.Equal(2, snapshot.AllVehicles.Count);
            Assert.Equal(100.5, snapshot.AllVehicles[1].PosX);
            Assert.Equal(200.3, snapshot.AllVehicles[1].PosZ);
        }

        [Fact]
        public async Task ReadSnapshotAsync_OtherVehicles_MapsScoringInfo()
        {
            var otherCars = new List<VehicleSampleData>
            {
                new VehicleSampleData
                {
                    VehicleId = 1,
                    Place = 5,
                    BestLapTime = 79.5,
                    LastLapTime = 80.1,
                    TimeBehindLeader = 12.3,
                    DriverName = "Hamilton"
                },
            };
            var sample = CreateSample() with { NumVehicles = 2, OtherVehicles = otherCars };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot?.Scoring);
            Assert.Equal(2, snapshot.Scoring.Vehicles.Count);
            var otherScoring = snapshot.Scoring.Vehicles[1];
            Assert.Equal(5, otherScoring.Place);
            Assert.Equal(79.5, otherScoring.BestLapTime, precision: 1);
            Assert.Equal("Hamilton", otherScoring.DriverName);
        }

        [Fact]
        public async Task ReadSnapshotAsync_NullOtherVehicles_OnlyPlayerInAllVehicles()
        {
            var sample = CreateSample() with { OtherVehicles = null };
            var source = CreateSourceWithSample(sample);

            var snapshot = await source.ReadSnapshotAsync();

            Assert.NotNull(snapshot);
            Assert.Single(snapshot.AllVehicles);
            Assert.True(snapshot.AllVehicles[0].IsPlayer);
        }

        #endregion

        #region Helpers

        private static TelemetrySample CreateSample(
            double speed = 200.0,
            double fuel = 50.0,
            double brake = 0.5,
            double throttle = 0.8,
            double steering = 0.0)
        {
            return new TelemetrySample(
                DateTime.UtcNow,
                SpeedKph: speed,
                TyreTempsC: new[] { 90.0, 91.0, 89.0, 90.0 },
                FuelLiters: fuel,
                Brake: brake,
                Throttle: throttle,
                Steering: steering);
        }

        private static SharedMemoryDataSource CreateSourceWithSample(TelemetrySample sample)
        {
            var mockReader = new Mock<ISharedMemoryReader>();
            mockReader.Setup(r => r.IsConnected).Returns(true);
            mockReader.Setup(r => r.GetLatestTelemetry()).Returns(sample);
            return new SharedMemoryDataSource(mockReader.Object);
        }

        #endregion
    }
}
