using System;
using System.Collections.Generic;
using System.Linq;
using PitWall.Models;

namespace PitWall.Core
{
    /// <summary>
    /// Tracks tyre wear per lap and projects laps until a wear threshold.
    /// </summary>
    public class TyreDegradation
    {
        private readonly Dictionary<TyrePosition, List<LapTyreWear>> _wearHistory = new();

        public void RecordLap(int lapNumber, double fl, double fr, double rl, double rr)
        {
            Add(TyrePosition.FrontLeft, lapNumber, fl);
            Add(TyrePosition.FrontRight, lapNumber, fr);
            Add(TyrePosition.RearLeft, lapNumber, rl);
            Add(TyrePosition.RearRight, lapNumber, rr);
        }

        public double GetLatestWear(TyrePosition position)
        {
            if (_wearHistory.TryGetValue(position, out var list) && list.Count > 0)
            {
                return list[list.Count - 1].Wear;
            }
            return 0.0;
        }

        public double GetAverageWearPerLap(TyrePosition position)
        {
            if (!_wearHistory.TryGetValue(position, out var list) || list.Count < 2)
            {
                return 0.0;
            }

            double totalDrop = 0.0;
            int intervals = 0;
            for (int i = 1; i < list.Count; i++)
            {
                double drop = list[i - 1].Wear - list[i].Wear;
                totalDrop += drop;
                intervals++;
            }

            return intervals > 0 ? totalDrop / intervals : 0.0;
        }

        public int PredictLapsUntilThreshold(TyrePosition position, double threshold)
        {
            if (!_wearHistory.ContainsKey(position) || _wearHistory[position].Count == 0)
            {
                return int.MaxValue;
            }

            double latest = GetLatestWear(position);
            if (latest <= threshold)
            {
                return 0;
            }

            double avgDrop = GetAverageWearPerLap(position);
            if (avgDrop <= 0)
            {
                return int.MaxValue;
            }

            double laps = (latest - threshold) / avgDrop;
            return (int)Math.Floor(laps);
        }

        private void Add(TyrePosition position, int lapNumber, double wear)
        {
            if (!_wearHistory.TryGetValue(position, out var list))
            {
                list = new List<LapTyreWear>();
                _wearHistory[position] = list;
            }

            list.Add(new LapTyreWear { Lap = lapNumber, Wear = wear });
        }

        private class LapTyreWear
        {
            public int Lap { get; set; }
            public double Wear { get; set; }
        }
    }
}
