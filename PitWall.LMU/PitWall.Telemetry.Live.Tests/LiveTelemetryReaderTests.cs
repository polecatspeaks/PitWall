using System;
using System.Threading.Tasks;
using Moq;
using PitWall.Telemetry.Live.Models;
using PitWall.Telemetry.Live.Services;
using Xunit;

namespace PitWall.Telemetry.Live.Tests
{
    /// <summary>
    /// Tests for LiveTelemetryReader following TDD methodology.
    /// These tests are written FIRST, then implementation follows.
    /// </summary>
    public class LiveTelemetryReaderTests
    {
        [Fact]
        public async Task ReadAsync_WithValidData_ReturnsTelemetrySnapshot()
        {
            // Arrange
            var mockSource = new Mock<ITelemetryDataSource>();
            mockSource
                .Setup(m => m.IsAvailable())
                .Returns(true);
            mockSource
                .Setup(m => m.ReadSnapshotAsync())
                .ReturnsAsync(CreateSampleSnapshot());

            var reader = new LiveTelemetryReader(mockSource.Object);

            // Act
            var snapshot = await reader.ReadAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.True(snapshot.Timestamp <= DateTime.UtcNow);
            mockSource.Verify(m => m.ReadSnapshotAsync(), Times.Once);
        }

        [Fact]
        public async Task ReadAsync_WhenSourceNotAvailable_ReturnsNull()
        {
            // Arrange
            var mockSource = new Mock<ITelemetryDataSource>();
            mockSource
                .Setup(m => m.IsAvailable())
                .Returns(false);

            var reader = new LiveTelemetryReader(mockSource.Object);

            // Act
            var snapshot = await reader.ReadAsync();

            // Assert
            Assert.Null(snapshot);
            mockSource.Verify(m => m.ReadSnapshotAsync(), Times.Never);
        }

        [Fact]
        public async Task ReadAsync_IncludesPlayerVehicle()
        {
            // Arrange
            var mockSource = new Mock<ITelemetryDataSource>();
            mockSource.Setup(m => m.IsAvailable()).Returns(true);
            
            var testSnapshot = CreateSampleSnapshot();
            testSnapshot.PlayerVehicle = new VehicleTelemetry
            {
                VehicleId = 0,
                IsPlayer = true,
                Speed = 125.5,
                Rpm = 8500,
                Gear = 4
            };
            
            mockSource
                .Setup(m => m.ReadSnapshotAsync())
                .ReturnsAsync(testSnapshot);

            var reader = new LiveTelemetryReader(mockSource.Object);

            // Act
            var snapshot = await reader.ReadAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot.PlayerVehicle);
            Assert.True(snapshot.PlayerVehicle.IsPlayer);
            Assert.Equal(125.5, snapshot.PlayerVehicle.Speed);
        }

        [Fact]
        public async Task ReadAsync_IncludesMultipleVehicles()
        {
            // Arrange
            var mockSource = new Mock<ITelemetryDataSource>();
            mockSource.Setup(m => m.IsAvailable()).Returns(true);
            
            var testSnapshot = CreateSampleSnapshot();
            testSnapshot.AllVehicles.Add(new VehicleTelemetry { VehicleId = 0, IsPlayer = true });
            testSnapshot.AllVehicles.Add(new VehicleTelemetry { VehicleId = 1, IsPlayer = false });
            testSnapshot.AllVehicles.Add(new VehicleTelemetry { VehicleId = 2, IsPlayer = false });
            
            mockSource
                .Setup(m => m.ReadSnapshotAsync())
                .ReturnsAsync(testSnapshot);

            var reader = new LiveTelemetryReader(mockSource.Object);

            // Act
            var snapshot = await reader.ReadAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.Equal(3, snapshot.AllVehicles.Count);
            Assert.Single(snapshot.AllVehicles.FindAll(v => v.IsPlayer));
        }

        [Fact]
        public async Task ReadAsync_IncludesDamageData()
        {
            // Arrange
            var mockSource = new Mock<ITelemetryDataSource>();
            mockSource.Setup(m => m.IsAvailable()).Returns(true);
            
            var testSnapshot = CreateSampleSnapshot();
            testSnapshot.PlayerVehicle = new VehicleTelemetry
            {
                VehicleId = 0,
                IsPlayer = true,
                DentSeverity = new byte[] { 0, 5, 10, 15, 0, 0, 0, 0 },
                LastImpactMagnitude = 18855.0,
                LastImpactTime = 123.45
            };
            
            mockSource
                .Setup(m => m.ReadSnapshotAsync())
                .ReturnsAsync(testSnapshot);

            var reader = new LiveTelemetryReader(mockSource.Object);

            // Act
            var snapshot = await reader.ReadAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot.PlayerVehicle);
            Assert.NotNull(snapshot.PlayerVehicle.DentSeverity);
            Assert.Equal(8, snapshot.PlayerVehicle.DentSeverity.Length);
            Assert.Equal(18855.0, snapshot.PlayerVehicle.LastImpactMagnitude);
        }

        [Fact]
        public async Task ReadAsync_IncludesWheelData()
        {
            // Arrange
            var mockSource = new Mock<ITelemetryDataSource>();
            mockSource.Setup(m => m.IsAvailable()).Returns(true);
            
            var testSnapshot = CreateSampleSnapshot();
            testSnapshot.PlayerVehicle = new VehicleTelemetry
            {
                VehicleId = 0,
                IsPlayer = true,
                Wheels = new[]
                {
                    new WheelData { TempInner = 95.0, TempMid = 98.0, TempOuter = 92.0, Wear = 85.0, Flat = false },
                    new WheelData { TempInner = 96.0, TempMid = 99.0, TempOuter = 93.0, Wear = 86.0, Flat = false },
                    new WheelData { TempInner = 94.0, TempMid = 97.0, TempOuter = 91.0, Wear = 84.0, Flat = false },
                    new WheelData { TempInner = 95.0, TempMid = 98.0, TempOuter = 92.0, Wear = 85.0, Flat = true }
                }
            };
            
            mockSource
                .Setup(m => m.ReadSnapshotAsync())
                .ReturnsAsync(testSnapshot);

            var reader = new LiveTelemetryReader(mockSource.Object);

            // Act
            var snapshot = await reader.ReadAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot.PlayerVehicle);
            Assert.Equal(4, snapshot.PlayerVehicle.Wheels.Length);
            Assert.True(snapshot.PlayerVehicle.Wheels[3].Flat);
            Assert.Equal(95.0, snapshot.PlayerVehicle.Wheels[0].TempInner);
        }

        [Fact]
        public async Task ReadAsync_IncludesScoringInfo()
        {
            // Arrange
            var mockSource = new Mock<ITelemetryDataSource>();
            mockSource.Setup(m => m.IsAvailable()).Returns(true);
            
            var testSnapshot = CreateSampleSnapshot();
            testSnapshot.Scoring = new ScoringInfo
            {
                SessionType = 10, // Race
                NumVehicles = 25,
                SectorFlags = new[] { 0, 2, 11 }, // Green flags in sectors
                YellowFlagState = 0
            };
            
            mockSource
                .Setup(m => m.ReadSnapshotAsync())
                .ReturnsAsync(testSnapshot);

            var reader = new LiveTelemetryReader(mockSource.Object);

            // Act
            var snapshot = await reader.ReadAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot.Scoring);
            Assert.Equal(10, snapshot.Scoring.SessionType);
            Assert.Equal(25, snapshot.Scoring.NumVehicles);
            Assert.Equal(3, snapshot.Scoring.SectorFlags.Length);
        }

        [Fact]
        public async Task ReadAsync_IncludesSessionInfo()
        {
            // Arrange
            var mockSource = new Mock<ITelemetryDataSource>();
            mockSource.Setup(m => m.IsAvailable()).Returns(true);
            
            var testSnapshot = CreateSampleSnapshot();
            testSnapshot.Session = new SessionInfo
            {
                StartTimeUtc = DateTime.UtcNow,
                SessionType = "Race",
                TrackName = "Monza Curva Grande Circuit",
                NumVehicles = 25,
                TrackLength = 5744.0
            };
            
            mockSource
                .Setup(m => m.ReadSnapshotAsync())
                .ReturnsAsync(testSnapshot);

            var reader = new LiveTelemetryReader(mockSource.Object);

            // Act
            var snapshot = await reader.ReadAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot.Session);
            Assert.Equal("Monza Curva Grande Circuit", snapshot.Session.TrackName);
            Assert.Equal(5744.0, snapshot.Session.TrackLength);
        }

        [Fact]
        public void Constructor_WithNullSource_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new LiveTelemetryReader(null!));
        }

        [Fact]
        public async Task ReadAsync_WithSourceException_ReturnsNull()
        {
            // Arrange
            var mockSource = new Mock<ITelemetryDataSource>();
            mockSource.Setup(m => m.IsAvailable()).Returns(true);
            mockSource
                .Setup(m => m.ReadSnapshotAsync())
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            var reader = new LiveTelemetryReader(mockSource.Object);

            // Act
            var snapshot = await reader.ReadAsync();

            // Assert
            Assert.Null(snapshot);
        }

        [Fact]
        public async Task ReadAsync_GeneratesSessionId()
        {
            // Arrange
            var mockSource = new Mock<ITelemetryDataSource>();
            mockSource.Setup(m => m.IsAvailable()).Returns(true);
            
            var testSnapshot = new TelemetrySnapshot
            {
                Timestamp = DateTime.UtcNow,
                SessionId = string.Empty, // Empty session ID to test generation
                AllVehicles = new(),
                Session = new SessionInfo(),
                Scoring = new ScoringInfo()
            };
            
            mockSource
                .Setup(m => m.ReadSnapshotAsync())
                .ReturnsAsync(testSnapshot);

            var reader = new LiveTelemetryReader(mockSource.Object);

            // Act
            var snapshot = await reader.ReadAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.NotEmpty(snapshot.SessionId);
        }

        [Fact]
        public async Task ReadAsync_SetsTimestamp_WhenDefault()
        {
            // Arrange
            var mockSource = new Mock<ITelemetryDataSource>();
            mockSource.Setup(m => m.IsAvailable()).Returns(true);
            
            var testSnapshot = new TelemetrySnapshot
            {
                Timestamp = default, // Default timestamp to test auto-setting
                SessionId = "test-session",
                AllVehicles = new(),
                Session = new SessionInfo(),
                Scoring = new ScoringInfo()
            };
            
            mockSource
                .Setup(m => m.ReadSnapshotAsync())
                .ReturnsAsync(testSnapshot);

            var reader = new LiveTelemetryReader(mockSource.Object);

            // Act
            var beforeTime = DateTime.UtcNow;
            var snapshot = await reader.ReadAsync();
            var afterTime = DateTime.UtcNow;

            // Assert
            Assert.NotNull(snapshot);
            Assert.NotEqual(default, snapshot.Timestamp);
            Assert.True(snapshot.Timestamp >= beforeTime);
            Assert.True(snapshot.Timestamp <= afterTime);
        }

        private TelemetrySnapshot CreateSampleSnapshot()
        {
            return new TelemetrySnapshot
            {
                Timestamp = DateTime.UtcNow,
                SessionId = Guid.NewGuid().ToString(),
                AllVehicles = new(),
                Session = new SessionInfo(),
                Scoring = new ScoringInfo()
            };
        }
    }
}
