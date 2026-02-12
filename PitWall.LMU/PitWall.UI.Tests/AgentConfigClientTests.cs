using System;
using System.Collections.Generic;
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
        public async Task GetConfigAsync_RequestsCorrectEndpoint()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpHandler(req =>
            {
                capturedRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await api.GetConfigAsync(CancellationToken.None);

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
            Assert.Contains("/agent/config", capturedRequest.RequestUri?.ToString());
        }

        [Fact]
        public async Task UpdateConfigAsync_SendsPutToCorrectEndpoint()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpHandler(req =>
            {
                capturedRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await api.UpdateConfigAsync(new AgentConfigUpdateDto(), CancellationToken.None);

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Put, capturedRequest.Method);
            Assert.Contains("/agent/config", capturedRequest.RequestUri?.ToString());
        }

        [Fact]
        public async Task DiscoverEndpointsAsync_RequestsCorrectEndpoint()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpHandler(req =>
            {
                capturedRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"endpoints\":[\"http://localhost:11434\"]}", Encoding.UTF8, "application/json")
                };
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            var result = await api.DiscoverEndpointsAsync(CancellationToken.None);

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
            Assert.Contains("/agent/llm/discover", capturedRequest.RequestUri?.ToString());
            Assert.Single(result);
            Assert.Equal("http://localhost:11434", result[0]);
        }

        [Fact]
        public async Task GetConfigAsync_HttpError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.GetConfigAsync(CancellationToken.None));
        }

        [Fact]
        public async Task GetConfigAsync_NotFound_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.GetConfigAsync(CancellationToken.None));
        }

        [Fact]
        public async Task UpdateConfigAsync_HttpError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.UpdateConfigAsync(new AgentConfigUpdateDto(), CancellationToken.None));
        }

        [Fact]
        public async Task DiscoverEndpointsAsync_HttpError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.DiscoverEndpointsAsync(CancellationToken.None));
        }

        [Fact]
        public async Task GetConfigAsync_NetworkError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => throw new HttpRequestException("Network error"));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.GetConfigAsync(CancellationToken.None));
        }

        [Fact]
        public async Task UpdateConfigAsync_NetworkError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => throw new HttpRequestException("Network error"));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.UpdateConfigAsync(new AgentConfigUpdateDto(), CancellationToken.None));
        }

        [Fact]
        public async Task DiscoverEndpointsAsync_NetworkError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => throw new HttpRequestException("Network error"));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.DiscoverEndpointsAsync(CancellationToken.None));
        }

        [Fact]
        public async Task GetHealthAsync_ReturnsParsedHealth()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"llmEnabled\":true,\"llmAvailable\":true,\"provider\":\"Ollama\",\"model\":\"llama3.2\",\"endpoint\":\"http://localhost:11434\"}", Encoding.UTF8, "application/json")
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            var result = await api.GetHealthAsync(CancellationToken.None);

            Assert.True(result.LlmEnabled);
            Assert.True(result.LlmAvailable);
            Assert.Equal("Ollama", result.Provider);
            Assert.Equal("llama3.2", result.Model);
            Assert.Equal("http://localhost:11434", result.Endpoint);
        }

        [Fact]
        public async Task GetHealthAsync_RequestsCorrectEndpoint()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpHandler(req =>
            {
                capturedRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await api.GetHealthAsync(CancellationToken.None);

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
            Assert.Contains("/agent/health", capturedRequest.RequestUri?.ToString());
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
        public async Task GetHealthAsync_NetworkError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => throw new HttpRequestException("Network error"));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.GetHealthAsync(CancellationToken.None));
        }

        [Fact]
        public async Task TestLlmAsync_ReturnsParsedResult()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"llmEnabled\":true,\"available\":true}", Encoding.UTF8, "application/json")
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            var result = await api.TestLlmAsync(CancellationToken.None);

            Assert.True(result.LlmEnabled);
            Assert.True(result.Available);
        }

        [Fact]
        public async Task TestLlmAsync_RequestsCorrectEndpoint()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpHandler(req =>
            {
                capturedRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentConfigClient(client);

            await api.TestLlmAsync(CancellationToken.None);

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
            Assert.Contains("/agent/llm/test", capturedRequest.RequestUri?.ToString());
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

        [Fact]
        public async Task TestLlmAsync_NetworkError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => throw new HttpRequestException("Network error"));
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
