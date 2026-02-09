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
