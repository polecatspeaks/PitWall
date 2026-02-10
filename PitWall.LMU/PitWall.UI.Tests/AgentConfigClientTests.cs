using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;
using PitWall.UI.Services;
using Xunit;

namespace PitWall.UI.Tests
{
    public class AgentConfigClientTests
    {
        [Fact]
        public async Task GetConfigAsync_ReturnsParsedConfig()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"enableLLM\":true,\"llmProvider\":\"Ollama\",\"requirePitForLlm\":true}", Encoding.UTF8, "application/json")
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            var result = await api.GetConfigAsync(CancellationToken.None);

            Assert.True(result.EnableLLM);
            Assert.Equal("Ollama", result.LLMProvider);
            Assert.True(result.RequirePitForLlm);
        }

        [Fact]
        public async Task UpdateConfigAsync_ReturnsUpdatedConfig()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"enableLLM\":false,\"llmProvider\":\"OpenAI\",\"requirePitForLlm\":false}", Encoding.UTF8, "application/json")
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            var result = await api.UpdateConfigAsync(new AgentConfigUpdateDto { LLMProvider = "OpenAI" }, CancellationToken.None);

            Assert.False(result.EnableLLM);
            Assert.Equal("OpenAI", result.LLMProvider);
            Assert.False(result.RequirePitForLlm);
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
