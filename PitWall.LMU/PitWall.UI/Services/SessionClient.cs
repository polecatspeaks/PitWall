using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PitWall.UI.Services
{
    public class SessionClient : ISessionClient
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly HttpClient _httpClient;

        public SessionClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<int> GetSessionCountAsync(CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync("/api/sessions/count", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<SessionCountResponse>(json, Options);

            return result?.SessionCount ?? 0;
        }

        private record SessionCountResponse(int SessionCount);
    }
}
