using PitWall.UI.ViewModels;
using Xunit;

namespace PitWall.UI.Tests
{
    public class DashboardViewModelModelTests
    {
        [Fact]
        public void RelativePositionEntry_DefaultValues()
        {
            var entry = new RelativePositionEntry();
            
            Assert.Equal(string.Empty, entry.Relation);
            Assert.Equal(0, entry.Position);
            Assert.Equal(string.Empty, entry.CarNumber);
            Assert.Equal(string.Empty, entry.Driver);
            Assert.Equal(string.Empty, entry.Gap);
            Assert.Equal(string.Empty, entry.Class);
        }

        [Fact]
        public void RelativePositionEntry_SetAndGetProperties()
        {
            var entry = new RelativePositionEntry
            {
                Relation = "AHEAD",
                Position = 5,
                CarNumber = "42",
                Driver = "Test Driver",
                Gap = "+2.5s",
                Class = "GT3"
            };
            
            Assert.Equal("AHEAD", entry.Relation);
            Assert.Equal(5, entry.Position);
            Assert.Equal("42", entry.CarNumber);
            Assert.Equal("Test Driver", entry.Driver);
            Assert.Equal("+2.5s", entry.Gap);
            Assert.Equal("GT3", entry.Class);
        }

        [Fact]
        public void RelativePositionEntry_ModifyProperties()
        {
            var entry = new RelativePositionEntry
            {
                Position = 1,
                Gap = "+0.0s"
            };
            
            entry.Position = 2;
            entry.Gap = "+1.2s";
            
            Assert.Equal(2, entry.Position);
            Assert.Equal("+1.2s", entry.Gap);
        }

        [Fact]
        public void StandingsEntry_DefaultValues()
        {
            var entry = new StandingsEntry();
            
            Assert.Equal(0, entry.Position);
            Assert.Equal(string.Empty, entry.Class);
            Assert.Equal(string.Empty, entry.CarNumber);
            Assert.Equal(string.Empty, entry.Driver);
            Assert.Equal(string.Empty, entry.Gap);
            Assert.Equal(0, entry.Laps);
        }

        [Fact]
        public void StandingsEntry_SetAndGetProperties()
        {
            var entry = new StandingsEntry
            {
                Position = 3,
                Class = "LMP2",
                CarNumber = "23",
                Driver = "John Smith",
                Gap = "+45.2s",
                Laps = 25
            };
            
            Assert.Equal(3, entry.Position);
            Assert.Equal("LMP2", entry.Class);
            Assert.Equal("23", entry.CarNumber);
            Assert.Equal("John Smith", entry.Driver);
            Assert.Equal("+45.2s", entry.Gap);
            Assert.Equal(25, entry.Laps);
        }

        [Fact]
        public void StandingsEntry_ModifyProperties()
        {
            var entry = new StandingsEntry
            {
                Position = 1,
                Laps = 10
            };
            
            entry.Position = 2;
            entry.Laps = 15;
            
            Assert.Equal(2, entry.Position);
            Assert.Equal(15, entry.Laps);
        }

        [Fact]
        public void RelativePositionEntry_AllStringPropertiesCanBeNull()
        {
            var entry = new RelativePositionEntry
            {
                Relation = null!,
                CarNumber = null!,
                Driver = null!,
                Gap = null!,
                Class = null!
            };
            
            Assert.Null(entry.Relation);
            Assert.Null(entry.CarNumber);
            Assert.Null(entry.Driver);
            Assert.Null(entry.Gap);
            Assert.Null(entry.Class);
        }

        [Fact]
        public void StandingsEntry_AllStringPropertiesCanBeNull()
        {
            var entry = new StandingsEntry
            {
                Class = null!,
                CarNumber = null!,
                Driver = null!,
                Gap = null!
            };
            
            Assert.Null(entry.Class);
            Assert.Null(entry.CarNumber);
            Assert.Null(entry.Driver);
            Assert.Null(entry.Gap);
        }
    }
}
