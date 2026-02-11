using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Services;
using Xunit;

namespace PitWall.UI.Tests
{
    public class AgentQueryClientTests
    {
        [Fact]
        public async Task SendQueryAsync_ReturnsParsedResponse()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"answer\":\"Box this lap\",\"source\":\"RulesEngine\",\"confidence\":0.92,\"success\":true}", Encoding.UTF8, "application/json")
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentQueryClient(client);

            var result = await api.SendQueryAsync("Fuel?", CancellationToken.None);

            Assert.Equal("Box this lap", result.Answer);
            Assert.Equal("RulesEngine", result.Source);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task SendQueryAsync_HttpError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentQueryClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.SendQueryAsync("Test", CancellationToken.None));
        }

        [Fact]
        public async Task SendQueryAsync_NetworkError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => throw new HttpRequestException("Network error"));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentQueryClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.SendQueryAsync("Test", CancellationToken.None));
        }

        [Fact]
        public async Task SendQueryAsync_EmptyResponse_ReturnsDefault()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentQueryClient(client);

            var result = await api.SendQueryAsync("Test", CancellationToken.None);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task SendQueryAsync_NullResponse_ReturnsDefault()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentQueryClient(client);

            var result = await api.SendQueryAsync("Test", CancellationToken.None);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task SendQueryAsync_CaseInsensitiveJson_ParsesCorrectly()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"Answer\":\"Test\",\"SOURCE\":\"Engine\"}", Encoding.UTF8, "application/json")
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentQueryClient(client);

            var result = await api.SendQueryAsync("Test", CancellationToken.None);

            Assert.Equal("Test", result.Answer);
            Assert.Equal("Engine", result.Source);
        }

        [Fact]
        public async Task SendQueryAsync_PostsToCorrectEndpoint()
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
            var api = new AgentQueryClient(client);

            await api.SendQueryAsync("Test", CancellationToken.None);

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Post, capturedRequest.Method);
            Assert.Contains("agent/query", capturedRequest.RequestUri?.ToString());
        }

        [Fact]
        public async Task SendQueryAsync_SendsJsonContent()
        {
            HttpContent? capturedContent = null;
            var handler = new StubHttpHandler(req =>
            {
                capturedContent = req.Content;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentQueryClient(client);

            await api.SendQueryAsync("Test query", CancellationToken.None);

            Assert.NotNull(capturedContent);
            var content = await capturedContent.ReadAsStringAsync();
            Assert.Contains("Test query", content);
        }

        [Fact]
        public async Task SendQueryAsync_CancellationToken_Cancels()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentQueryClient(client);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await api.SendQueryAsync("Test", cts.Token));
        }

        [Fact]
        public async Task SendQueryAsync_ComplexResponse_ParsesAllFields()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"answer\":\"Complex answer\",\"source\":\"LLM\",\"confidence\":0.85,\"success\":true,\"metadata\":{}}", 
                    Encoding.UTF8, "application/json")
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new AgentQueryClient(client);

            var result = await api.SendQueryAsync("Complex query", CancellationToken.None);

            Assert.Equal("Complex answer", result.Answer);
            Assert.Equal("LLM", result.Source);
            Assert.True(result.Success);
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
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(_handler(request));
            }
        }
    }
}
