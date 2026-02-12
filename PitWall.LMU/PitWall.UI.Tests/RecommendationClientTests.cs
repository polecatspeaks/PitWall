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
    public class RecommendationClientTests
    {
        [Fact]
        public async Task GetRecommendationAsync_ReturnsParsedResult()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"sessionId\":\"s1\",\"recommendation\":\"Pit now\",\"confidence\":0.91}", Encoding.UTF8, "application/json")
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new RecommendationClient(client);

            var result = await api.GetRecommendationAsync("s1", CancellationToken.None);

            Assert.Equal("Pit now", result.Recommendation);
            Assert.Equal(0.91, result.Confidence);
        }

        [Fact]
        public async Task GetRecommendationAsync_HttpError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new RecommendationClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.GetRecommendationAsync("s1", CancellationToken.None));
        }

        [Fact]
        public async Task GetRecommendationAsync_NetworkError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => throw new HttpRequestException("Connection failed"));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new RecommendationClient(client);

            await Assert.ThrowsAsync<HttpRequestException>(
                async () => await api.GetRecommendationAsync("s1", CancellationToken.None));
        }

        [Fact]
        public async Task GetRecommendationAsync_InvalidJson_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("invalid json", Encoding.UTF8, "application/json")
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new RecommendationClient(client);

            await Assert.ThrowsAnyAsync<Exception>(
                async () => await api.GetRecommendationAsync("s1", CancellationToken.None));
        }

        [Fact]
        public async Task GetRecommendationAsync_EscapesSessionId()
        {
            Uri? capturedUri = null;
            var handler = new StubHttpHandler(req =>
            {
                capturedUri = req.RequestUri;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"recommendation\":\"\",\"confidence\":0}", Encoding.UTF8, "application/json")
                };
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new RecommendationClient(client);

            await api.GetRecommendationAsync("session with spaces", CancellationToken.None);

            Assert.NotNull(capturedUri);
            var query = capturedUri.Query.TrimStart('?');
            var parts = query.Split('=', 2);
            Assert.Equal("sessionId", parts[0]);
            Assert.Equal("session with spaces", Uri.UnescapeDataString(parts[1]));
        }

        [Fact]
        public async Task GetRecommendationAsync_GetsFromCorrectEndpoint()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpHandler(req =>
            {
                capturedRequest = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"recommendation\":\"\",\"confidence\":0}", Encoding.UTF8, "application/json")
                };
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new RecommendationClient(client);

            await api.GetRecommendationAsync("test-session", CancellationToken.None);

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
            Assert.Contains("api/recommend", capturedRequest.RequestUri?.ToString());
            Assert.Contains("sessionId=test-session", capturedRequest.RequestUri?.ToString());
        }

        [Fact]
        public async Task GetRecommendationAsync_CancellationToken_Cancels()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"recommendation\":\"\",\"confidence\":0}", Encoding.UTF8, "application/json")
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new RecommendationClient(client);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await api.GetRecommendationAsync("s1", cts.Token));
        }

        [Fact]
        public async Task GetRecommendationAsync_EmptySessionId_Works()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"recommendation\":\"No data\",\"confidence\":0}", Encoding.UTF8, "application/json")
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new RecommendationClient(client);

            var result = await api.GetRecommendationAsync("", CancellationToken.None);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetRecommendationAsync_SpecialCharacters_EscapesCorrectly()
        {
            Uri? capturedUri = null;
            var handler = new StubHttpHandler(req =>
            {
                capturedUri = req.RequestUri;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"recommendation\":\"\",\"confidence\":0}", Encoding.UTF8, "application/json")
                };
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new RecommendationClient(client);

            await api.GetRecommendationAsync("session&id=123", CancellationToken.None);

            Assert.NotNull(capturedUri);
            var query = capturedUri.Query.TrimStart('?');
            var parts = query.Split('=', 2);
            Assert.Equal("sessionId", parts[0]);
            Assert.Equal("session&id=123", Uri.UnescapeDataString(parts[1]));
        }

        [Fact]
        public async Task GetRecommendationAsync_LowConfidence_ReturnsValue()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"sessionId\":\"s1\",\"recommendation\":\"Uncertain\",\"confidence\":0.1}", Encoding.UTF8, "application/json")
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new RecommendationClient(client);

            var result = await api.GetRecommendationAsync("s1", CancellationToken.None);

            Assert.Equal(0.1, result.Confidence);
            Assert.Equal("Uncertain", result.Recommendation);
        }

        [Fact]
        public async Task GetRecommendationAsync_HighConfidence_ReturnsValue()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"sessionId\":\"s1\",\"recommendation\":\"Box now!\",\"confidence\":0.99}", Encoding.UTF8, "application/json")
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var api = new RecommendationClient(client);

            var result = await api.GetRecommendationAsync("s1", CancellationToken.None);

            Assert.Equal(0.99, result.Confidence);
            Assert.Equal("Box now!", result.Recommendation);
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
