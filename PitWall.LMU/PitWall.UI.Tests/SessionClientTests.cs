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
    public class SessionClientTests
    {
        [Fact]
        public void Constructor_WithHttpClient_InitializesSuccessfully()
        {
            var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
            
            var client = new SessionClient(httpClient);
            
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_WithNullLogger_UsesNullLogger()
        {
            var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
            
            var client = new SessionClient(httpClient, null);
            
            Assert.NotNull(client);
        }

        [Fact]
        public async Task GetSessionCountAsync_ValidResponse_ReturnsCount()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"sessionCount\":5}", Encoding.UTF8, "application/json")
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            var result = await client.GetSessionCountAsync(CancellationToken.None);

            Assert.Equal(5, result);
        }

        [Fact]
        public async Task GetSessionCountAsync_ZeroSessions_ReturnsZero()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"sessionCount\":0}", Encoding.UTF8, "application/json")
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            var result = await client.GetSessionCountAsync(CancellationToken.None);

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task GetSessionCountAsync_NullSessionCount_ReturnsZero()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            var result = await client.GetSessionCountAsync(CancellationToken.None);

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task GetSessionCountAsync_HttpError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await client.GetSessionCountAsync(CancellationToken.None));
        }

        [Fact]
        public async Task GetSessionCountAsync_MalformedJson_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("invalid json", Encoding.UTF8, "application/json")
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await client.GetSessionCountAsync(CancellationToken.None));
        }

        [Fact]
        public async Task GetSessionSummariesAsync_ValidResponse_ReturnsList()
        {
            var json = @"{
                ""sessions"": [
                    {
                        ""sessionId"": 1,
                        ""track"": ""Monza"",
                        ""car"": ""Ferrari""
                    },
                    {
                        ""sessionId"": 2,
                        ""track"": ""Spa"",
                        ""car"": ""Porsche""
                    }
                ]
            }";
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            var result = await client.GetSessionSummariesAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].SessionId);
            Assert.Equal("Monza", result[0].Track);
            Assert.Equal("Ferrari", result[0].Car);
        }

        [Fact]
        public async Task GetSessionSummariesAsync_EmptyList_ReturnsEmptyList()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"sessions\":[]}", Encoding.UTF8, "application/json")
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            var result = await client.GetSessionSummariesAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetSessionSummariesAsync_NullSessions_ReturnsEmptyList()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            var result = await client.GetSessionSummariesAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetSessionSummariesAsync_HttpError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await client.GetSessionSummariesAsync(CancellationToken.None));
        }

        [Fact]
        public async Task UpdateSessionMetadataAsync_ValidUpdate_ReturnsUpdatedSummary()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{
                    ""sessionId"": 1,
                    ""track"": ""Updated Track"",
                    ""car"": ""Updated Car""
                }", Encoding.UTF8, "application/json")
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);
            var update = new SessionMetadataUpdateDto
            {
                Track = "Updated Track",
                Car = "Updated Car"
            };

            var result = await client.UpdateSessionMetadataAsync(1, update, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(1, result.SessionId);
            Assert.Equal("Updated Track", result.Track);
            Assert.Equal("Updated Car", result.Car);
        }

        [Fact]
        public async Task UpdateSessionMetadataAsync_NullUpdate_DoesNotThrow()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""sessionId"": 1}", Encoding.UTF8, "application/json")
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);
            var update = new SessionMetadataUpdateDto();

            var result = await client.UpdateSessionMetadataAsync(1, update, CancellationToken.None);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task UpdateSessionMetadataAsync_HttpError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);
            var update = new SessionMetadataUpdateDto { Track = "Track" };

            await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await client.UpdateSessionMetadataAsync(1, update, CancellationToken.None));
        }

        [Fact]
        public async Task GetSessionSamplesAsync_ValidResponse_ReturnsSamples()
        {
            var json = @"{
                ""sessionId"": 1,
                ""sampleCount"": 2,
                ""samples"": [
                    {
                        ""speedKph"": 200.5,
                        ""throttle"": 0.8,
                        ""brake"": 0.1,
                        ""fuelLiters"": 45.0
                    },
                    {
                        ""speedKph"": 180.3,
                        ""throttle"": 0.6,
                        ""brake"": 0.3,
                        ""fuelLiters"": 44.5
                    }
                ]
            }";
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            var result = await client.GetSessionSamplesAsync(1, 0, 100, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(200.5, result[0].SpeedKph);
            Assert.Equal(0.8, result[0].ThrottlePosition);
        }

        [Fact]
        public async Task GetSessionSamplesAsync_EmptyList_ReturnsEmptyList()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""sessionId"": 1, ""sampleCount"": 0, ""samples"": []}", 
                    Encoding.UTF8, "application/json")
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            var result = await client.GetSessionSamplesAsync(1, 0, 100, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetSessionSamplesAsync_NullSamples_ReturnsEmptyList()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""sessionId"": 1}", Encoding.UTF8, "application/json")
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            var result = await client.GetSessionSamplesAsync(1, 0, 100, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetSessionSamplesAsync_HttpError_ThrowsException()
        {
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await client.GetSessionSamplesAsync(1, 0, 100, CancellationToken.None));
        }

        [Fact]
        public async Task GetSessionSamplesAsync_WithRowRange_PassesCorrectParameters()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpHandler(request =>
            {
                capturedRequest = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"{""samples"":[]}", Encoding.UTF8, "application/json")
                };
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            await client.GetSessionSamplesAsync(5, 100, 200, CancellationToken.None);

            Assert.NotNull(capturedRequest);
            var uri = capturedRequest.RequestUri?.ToString();
            Assert.NotNull(uri);
            Assert.Contains("/api/sessions/5/samples", uri);
            Assert.Contains("startRow=100", uri);
            Assert.Contains("endRow=200", uri);
        }

        [Fact]
        public async Task GetSessionCountAsync_CallsCorrectEndpoint()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpHandler(request =>
            {
                capturedRequest = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"sessionCount\":0}", Encoding.UTF8, "application/json")
                };
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            await client.GetSessionCountAsync(CancellationToken.None);

            Assert.NotNull(capturedRequest);
            Assert.Equal("/api/sessions/count", capturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        }

        [Fact]
        public async Task GetSessionSummariesAsync_CallsCorrectEndpoint()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpHandler(request =>
            {
                capturedRequest = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"sessions\":[]}", Encoding.UTF8, "application/json")
                };
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);

            await client.GetSessionSummariesAsync(CancellationToken.None);

            Assert.NotNull(capturedRequest);
            Assert.Equal("/api/sessions/summary", capturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        }

        [Fact]
        public async Task UpdateSessionMetadataAsync_CallsCorrectEndpoint()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpHandler(request =>
            {
                capturedRequest = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"sessionId\":42}", Encoding.UTF8, "application/json")
                };
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);
            var update = new SessionMetadataUpdateDto();

            await client.UpdateSessionMetadataAsync(42, update, CancellationToken.None);

            Assert.NotNull(capturedRequest);
            Assert.Equal("/api/sessions/42/metadata", capturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal(HttpMethod.Put, capturedRequest.Method);
        }

        [Fact]
        public async Task UpdateSessionMetadataAsync_SendsJsonContent()
        {
            string? capturedContent = null;
            var handler = new StubHttpHandler(request =>
            {
                if (request.Content != null)
                {
                    capturedContent = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"sessionId\":1}", Encoding.UTF8, "application/json")
                };
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var client = new SessionClient(httpClient);
            var update = new SessionMetadataUpdateDto { Track = "Monza", Car = "Ferrari" };

            await client.UpdateSessionMetadataAsync(1, update, CancellationToken.None);

            Assert.NotNull(capturedContent);
            Assert.Contains("monza", capturedContent.ToLower());
            Assert.Contains("ferrari", capturedContent.ToLower());
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
