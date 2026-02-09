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
