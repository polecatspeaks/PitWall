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

        [Fact]
        public async Task GetHealthAsync_ReturnsParsedHealth()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpHandler(req =>
            {
                capturedRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"llmEnabled\":true,\"llmAvailable\":false,\"provider\":\"Ollama\",\"model\":\"llama3\",\"endpoint\":\"http://localhost:11434\"}",
                        Encoding.UTF8,
                        "application/json")
                };
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            var result = await api.GetHealthAsync(CancellationToken.None);

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
            Assert.Contains("/agent/health", capturedRequest.RequestUri?.ToString());
            Assert.True(result.LlmEnabled);
            Assert.False(result.LlmAvailable);
            Assert.Equal("Ollama", result.Provider);
            Assert.Equal("llama3", result.Model);
            Assert.Equal("http://localhost:11434", result.Endpoint);
        }

        [Fact]
        public async Task TestLlmAsync_ReturnsParsedResult()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpHandler(req =>
            {
                capturedRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"llmEnabled\":true,\"available\":true}", Encoding.UTF8, "application/json")
                };
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            var result = await api.TestLlmAsync(CancellationToken.None);

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
            Assert.Contains("/agent/llm/test", capturedRequest.RequestUri?.ToString());
            Assert.True(result.LlmEnabled);
            Assert.True(result.Available);
        }

        [Fact]
        public async Task GetHealthAsync_HttpError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.GetHealthAsync(CancellationToken.None));
        }

        [Fact]
        public async Task TestLlmAsync_HttpError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.TestLlmAsync(CancellationToken.None));
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
