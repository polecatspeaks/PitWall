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
            // DEBUG: Log raw JSON to diagnose brake value issue
            Console.WriteLine($"[Parser:RAW_JSON] {json}");
            
            var dto = JsonSerializer.Deserialize<TelemetrySampleDto>(json, Options);

            if (dto == null)
            {
                var snippet = json.Substring(0, Math.Min(100, json.Length));
                Console.WriteLine($"[Parser:ERROR] Deserialization returned NULL. Snippet: {snippet}");
                return new TelemetrySampleDto();
            }

            Console.WriteLine($"[Parser:VALUES] Speed={dto.SpeedKph:F1} Throttle={dto.ThrottlePosition:F3} Brake={dto.BrakePosition:F3} Steering={dto.SteeringAngle:F3}");

            if (dto.BrakePosition < 0 || dto.BrakePosition > 1.0)
            {
                Console.WriteLine($"[Parser:WARNING] Brake out of range: {dto.BrakePosition} (expected 0-1)");
            }

            if (dto.ThrottlePosition < 0 || dto.ThrottlePosition > 1.0)
            {
                Console.WriteLine($"[Parser:WARNING] Throttle out of range: {dto.ThrottlePosition} (expected 0-1)");
            }

            // DEFENSIVE: Normalize and validate pedal values (they might be 0-100 or 0-1)
            dto.ThrottlePosition = NormalizePedal(dto.ThrottlePosition);
            dto.BrakePosition = NormalizePedal(dto.BrakePosition);
            
            // DEFENSIVE: Normalize and validate steering (might be in different scales)
            dto.SteeringAngle = NormalizeSteering(dto.SteeringAngle);
            
            // DEFENSIVE: Clamp all values to safe ranges
            dto.ThrottlePosition = Math.Clamp(dto.ThrottlePosition, 0, 1);
            dto.BrakePosition = Math.Clamp(dto.BrakePosition, 0, 1);
            dto.SteeringAngle = Math.Clamp(dto.SteeringAngle, -1, 1);

            // DEBUG: Log deserialized and normalized values
            Console.WriteLine($"[Parser:NORMALIZED] Throttle={dto.ThrottlePosition:F3} Brake={dto.BrakePosition:F3} Steering={dto.SteeringAngle:F3}");

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
