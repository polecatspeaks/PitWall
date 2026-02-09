using PitWall.UI.Models;
using PitWall.UI.Services;
using Xunit;

namespace PitWall.UI.Tests
{
    public class TelemetryMessageParserTests
    {
        [Fact]
        public void Parse_ValidTelemetryMessage_MapsFields()
        {
            var json = "{\"timestamp\":\"2026-02-09T12:00:00Z\",\"speedKph\":241.2,\"throttle\":0.8,\"brake\":0.1,\"steering\":-0.05,\"tyreTemps\":[90,92,88,91],\"fuelLiters\":45.2}";

            var result = TelemetryMessageParser.Parse(json);

            Assert.Equal(241.2, result.SpeedKph);
            Assert.Equal(0.8, result.Throttle);
            Assert.Equal(0.1, result.Brake);
            Assert.Equal(-0.05, result.Steering);
            Assert.Equal(4, result.TyreTempsC.Length);
            Assert.Equal(45.2, result.FuelLiters);
        }

        [Fact]
        public void Parse_MissingArray_UsesEmptyTemps()
        {
            var json = "{\"speedKph\":200,\"fuelLiters\":40.0}";

            var result = TelemetryMessageParser.Parse(json);

            Assert.NotNull(result.TyreTempsC);
            Assert.Empty(result.TyreTempsC);
        }
    }
}
