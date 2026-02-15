using System;
using System.Text.Json;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public static class TelemetryMessageParser
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static TelemetrySampleDto Parse(string json)
        {
            var dto = JsonSerializer.Deserialize<TelemetrySampleDto>(json, Options);

            if (dto == null)
            {
                return new TelemetrySampleDto();
            }

            // Normalize and validate pedal values (they might be 0-100 or 0-1)
            dto.ThrottlePosition = NormalizePedal(dto.ThrottlePosition);
            dto.BrakePosition = NormalizePedal(dto.BrakePosition);
            
            // Normalize and validate steering (might be in different scales)
            dto.SteeringAngle = NormalizeSteering(dto.SteeringAngle);
            
            // Clamp all values to safe ranges
            dto.ThrottlePosition = Math.Clamp(dto.ThrottlePosition, 0, 1);
            dto.BrakePosition = Math.Clamp(dto.BrakePosition, 0, 1);
            dto.SteeringAngle = Math.Clamp(dto.SteeringAngle, -1, 1);

            dto.TyreTempsC ??= Array.Empty<double>();
            return dto;
        }

        private static double NormalizePedal(double value)
        {
            return value > 1.0 ? value / 100.0 : value;
        }

        private static double NormalizeSteering(double value)
        {
            return Math.Abs(value) > 1.0 && Math.Abs(value) <= 100.0 ? value / 100.0 : value;
        }
    }
}
