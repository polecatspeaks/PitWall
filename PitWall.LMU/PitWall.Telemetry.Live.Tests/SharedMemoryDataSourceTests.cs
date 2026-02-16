using System;
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
