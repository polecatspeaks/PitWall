using PitWall.Core.Models;

namespace PitWall.Core.Utilities
{
    public static class DataValidator
    {
        public static bool IsValid(TelemetrySample sample)
        {
            if (sample.SpeedKph < 0 || sample.SpeedKph > 600) return false;
            if (sample.FuelLiters < 0 || sample.FuelLiters > 500) return false;
            if (sample.TyreTempsC == null || sample.TyreTempsC.Length != 4) return false;
            foreach (var t in sample.TyreTempsC)
            {
                if (t < -50 || t > 300) return false;
            }
            return true;
        }

        public static bool IsSessionActive(TelemetrySample sample) => true;
    }
}
