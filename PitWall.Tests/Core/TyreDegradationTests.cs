using PitWall.Core;
using PitWall.Models;
using Xunit;

namespace PitWall.Tests.Core
{
    public class TyreDegradationTests
    {
        [Fact]
        public void RecordLap_StoresWearPerTyre()
        {
            var tyres = new TyreDegradation();
            tyres.RecordLap(1, 90, 91, 89, 92);

            Assert.Equal(90, tyres.GetLatestWear(TyrePosition.FrontLeft));
            Assert.Equal(92, tyres.GetLatestWear(TyrePosition.RearRight));
        }

        [Fact]
        public void GetAverageWearPerLap_ComputesPositiveRate()
        {
            var tyres = new TyreDegradation();
            tyres.RecordLap(1, 90, 90, 90, 90);
            tyres.RecordLap(2, 88, 87, 86, 85);

            Assert.Equal(2, tyres.GetAverageWearPerLap(TyrePosition.FrontLeft), 2);
            Assert.Equal(5, tyres.GetAverageWearPerLap(TyrePosition.RearRight), 2);
        }

        [Fact]
        public void PredictLapsUntilThreshold_WhenAverageKnown_ReturnsFloor()
        {
            var tyres = new TyreDegradation();
            tyres.RecordLap(1, 90, 90, 90, 90);
            tyres.RecordLap(2, 85, 85, 85, 85); // wear 5 per lap

            int laps = tyres.PredictLapsUntilThreshold(TyrePosition.FrontLeft, threshold: 60);

            Assert.Equal(5, laps); // (85-60)/5 = 5
        }

        [Fact]
        public void PredictLapsUntilThreshold_WhenNoData_ReturnsMaxInt()
        {
            var tyres = new TyreDegradation();
            int laps = tyres.PredictLapsUntilThreshold(TyrePosition.FrontLeft, 60);
            Assert.Equal(int.MaxValue, laps);
        }

        [Fact]
        public void PredictLapsUntilThreshold_WhenAverageZero_ReturnsMaxInt()
        {
            var tyres = new TyreDegradation();
            tyres.RecordLap(1, 90, 90, 90, 90);
            tyres.RecordLap(2, 90, 90, 90, 90);
            int laps = tyres.PredictLapsUntilThreshold(TyrePosition.FrontLeft, 60);
            Assert.Equal(int.MaxValue, laps);
        }
    }
}
