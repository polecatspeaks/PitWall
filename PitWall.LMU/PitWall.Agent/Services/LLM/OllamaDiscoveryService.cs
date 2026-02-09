using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PitWall.Agent.Models;

namespace PitWall.Agent.Services.LLM
{
    public class OllamaDiscoveryService : ILLMDiscoveryService
    {
        private readonly HttpClient _httpClient;
        private readonly AgentOptions _options;
        private readonly ILlmEndpointEnumerator _endpointEnumerator;
        private readonly ILogger<OllamaDiscoveryService> _logger;

        public OllamaDiscoveryService(
            HttpClient httpClient,
            AgentOptions options,
            ILlmEndpointEnumerator endpointEnumerator,
            ILogger<OllamaDiscoveryService> logger)
        {
            _httpClient = httpClient;
            _options = options;
            _endpointEnumerator = endpointEnumerator;
            _logger = logger;
        }

        public async Task<IReadOnlyList<string>> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            if (!_options.EnableLLMDiscovery)
            {
                return Array.Empty<string>();
            }

            var endpoints = _endpointEnumerator.GetCandidateEndpoints().ToList();
            if (endpoints.Count == 0)
            {
                return Array.Empty<string>();
            }

            var results = new List<string>();
            var gate = new SemaphoreSlim(_options.LLMDiscoveryMaxConcurrency);
            var tasks = endpoints.Select(async endpoint =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    if (await IsOllamaAvailableAsync(endpoint, cancellationToken))
                    {
                        lock (results)
                        {
                            results.Add(endpoint.ToString().TrimEnd('/'));
                        }
                    }
                }
                finally
                {
                    gate.Release();
                }
            });

            await Task.WhenAll(tasks);
            return results;
        }

        private async Task<bool> IsOllamaAvailableAsync(Uri endpoint, CancellationToken cancellationToken)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_options.LLMDiscoveryTimeoutMs);

                var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, "/api/tags"));
                var response = await _httpClient.SendAsync(request, cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
            {
                _logger.LogDebug(ex, "LLM discovery probe failed for {Endpoint}", endpoint);
                return false;
            }
        }
    }
}
