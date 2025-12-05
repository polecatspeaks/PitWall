using System;
using PitWall.Models;

namespace PitWall.Core
{
    /// <summary>
    /// Undercut/overcut strategy analyzer
    /// </summary>
    public class UndercutStrategy
    {
        private const double UNDERCUT_LAP_THRESHOLD = 15; // Laps needed to make up pit delta
        private const double OVERCUT_GAP_MULTIPLIER = 1.5; // Safety margin for overcut

        public bool CanUndercut(RaceSituation situation)
        {
            if (situation.GapToCarAhead <= 0)
            {
                return false; // No car ahead or already ahead
            }

            // Calculate net time loss from pit stop
            double netPitLoss = situation.PitStopDuration - situation.GapToCarAhead;

            // Check if we can make up the deficit with fresh tyre advantage
            if (situation.FreshTyreAdvantage <= 0)
            {
                return false; // No advantage on fresh tyres
            }

            // Calculate laps needed to overcome pit delta
            double lapsNeeded = netPitLoss / situation.FreshTyreAdvantage;

            // Undercut viable if we can make up time in reasonable stint
            // Also check opponent is on old enough tyres to warrant pitting soon
            return lapsNeeded < UNDERCUT_LAP_THRESHOLD && 
                   situation.GapToCarAhead < situation.PitStopDuration * 0.4;
        }

        public bool CanOvercut(RaceSituation situation)
        {
            if (situation.GapToCarBehind <= 0)
            {
                return false; // No car behind
            }

            // Overcut works when we have enough gap that car behind can't undercut us
            // They will pit and lose time, we stay out and build gap
            double safeGap = (situation.PitStopDuration - situation.GapToCarBehind) * OVERCUT_GAP_MULTIPLIER;

            // Check if our gap is large enough to withstand their undercut attempt
            // Also verify car behind is on old tyres (likely to pit)
            return situation.GapToCarBehind > situation.PitStopDuration * 0.3 &&
                   situation.OpponentTyreAge > situation.CurrentTyreLaps + 3;
        }

        public int CalculatePositionGain(RaceSituation situation)
        {
            // Simulate the undercut scenario
            double currentGap = situation.GapToCarAhead;
            double netPitLoss = situation.PitStopDuration - currentGap;

            // After pit, we're behind by netPitLoss seconds
            // Each lap we gain FreshTyreAdvantage seconds
            if (situation.FreshTyreAdvantage <= 0)
            {
                return 0; // No advantage
            }

            double lapsToOvercome = netPitLoss > 0 
                ? netPitLoss / situation.FreshTyreAdvantage
                : 0;

            // If we can overcome deficit within reasonable time, we gain position
            // Undercut window is typically 8-10 laps before opponent must pit
            if (lapsToOvercome <= 10)
            {
                return 1; // Gain 1 position
            }

            return 0; // No position gain
        }

        public double EstimateFreshTyreAdvantage(int currentTyreLaps, double tyreDegradationPerLap)
        {
            // Calculate advantage of fresh tyres vs current worn tyres
            // Degradation accumulates linearly (simplified model)
            double currentTyreLoss = currentTyreLaps * tyreDegradationPerLap;
            
            // Fresh tyres have 0 degradation, so advantage is the accumulated loss
            return currentTyreLoss;
        }
    }
}
