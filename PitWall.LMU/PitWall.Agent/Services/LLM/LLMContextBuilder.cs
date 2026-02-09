using PitWall.Agent.Models;

namespace PitWall.Agent.Services.LLM
{
    public static class LLMContextBuilder
    {
        public static string BuildSystemPrompt(RaceContext ctx)
        {
            return $@"You are a professional race engineer for Le Mans endurance racing.

CURRENT SITUATION:
- Track: {ctx.TrackName}
- Car: {ctx.CarName}
- Lap: {ctx.CurrentLap}/{ctx.TotalLaps}
- Position: P{ctx.Position}
- Fuel: {ctx.FuelLevel:F1}L ({ctx.FuelLapsRemaining:F1} laps)
- Tires: {ctx.AverageTireWear:F0}% remaining, {ctx.TireLapsOnSet} laps old
- Weather: {ctx.CurrentWeather}, Track {ctx.TrackTemp:F0}C

PREDICTIONS:
- Optimal pit lap: {ctx.OptimalPitLap}
- Strategy confidence: {ctx.StrategyConfidence:F0}%

Respond as a concise race engineer. Be direct and factual. Focus on actionable information.";
        }
    }
}
