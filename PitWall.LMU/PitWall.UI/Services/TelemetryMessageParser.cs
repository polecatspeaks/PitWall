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

            dto.TyreTempsC ??= Array.Empty<double>();
            return dto;
        }
    }
}
