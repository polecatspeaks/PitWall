using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Agent.Models;
using PitWall.Agent.Services.LLM;
using Xunit;

namespace PitWall.Tests
{
    public class LlmDiscoveryServiceTests
    {
        [Fact]
        public async Task DiscoverAsync_FiltersToHealthyEndpoints()
        {
            var handler = new StubHttpHandler(request =>
            {
                if (request.RequestUri?.Host == "192.168.1.10")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            });

            var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(200)
            };

            var options = new AgentOptions
            {
                EnableLLM = true,
                EnableLLMDiscovery = true,
                LLMDiscoveryTimeoutMs = 200
            };

            var enumerator = new StubEndpointEnumerator(new[]
            {
                new Uri("http://192.168.1.10:11434"),
                new Uri("http://192.168.1.11:11434")
            });

            var service = new OllamaDiscoveryService(
                httpClient,
                options,
                enumerator,
                NullLogger<OllamaDiscoveryService>.Instance);

            var results = await service.DiscoverAsync(CancellationToken.None);

            Assert.Single(results);
            Assert.Equal("http://192.168.1.10:11434", results[0]);
        }

        private sealed class StubEndpointEnumerator : ILlmEndpointEnumerator
        {
            private readonly IReadOnlyList<Uri> _endpoints;

            public StubEndpointEnumerator(IReadOnlyList<Uri> endpoints)
            {
                _endpoints = endpoints;
            }

            public IEnumerable<Uri> GetCandidateEndpoints()
            {
                return _endpoints;
            }
        }

        private sealed class StubHttpHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }
    }
}
