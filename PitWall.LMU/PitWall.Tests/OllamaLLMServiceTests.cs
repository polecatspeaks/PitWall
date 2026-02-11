using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using PitWall.Agent.Models;
using PitWall.Agent.Services.LLM;
using Xunit;

namespace PitWall.Tests
{
    public class OllamaLLMServiceTests
    {
        private readonly AgentOptions _options;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;

        public OllamaLLMServiceTests()
        {
            _options = new AgentOptions
            {
                EnableLLM = true,
                LLMEndpoint = "http://localhost:11434",
                LLMModel = "llama3.2",
                LLMTimeoutMs = 5000
            };

            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        }

        [Fact]
        public void IsEnabled_ReturnsTrue_WhenEnableLLMIsTrue()
        {
            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);

            Assert.True(service.IsEnabled);
        }

        [Fact]
        public void IsEnabled_ReturnsFalse_WhenEnableLLMIsFalse()
        {
            _options.EnableLLM = false;
            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);

            Assert.False(service.IsEnabled);
        }

        [Fact]
        public async Task TestConnectionAsync_ReturnsTrue_WhenServerRespondsSuccessfully()
        {
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/tags")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK
                });

            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);

            var result = await service.TestConnectionAsync();

            Assert.True(result);
            Assert.True(service.IsAvailable);
        }

        [Fact]
        public async Task TestConnectionAsync_ReturnsFalse_WhenServerReturnsError()
        {
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/tags")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError
                });

            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);

            var result = await service.TestConnectionAsync();

            Assert.False(result);
            Assert.False(service.IsAvailable);
        }

        [Fact]
        public async Task TestConnectionAsync_ReturnsFalse_WhenConnectionFails()
        {
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Connection failed"));

            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);

            var result = await service.TestConnectionAsync();

            Assert.False(result);
            Assert.False(service.IsAvailable);
        }

        [Fact]
        public async Task QueryAsync_ReturnsErrorResponse_WhenServiceNotAvailable()
        {
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException());

            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);
            await service.TestConnectionAsync();

            var context = new RaceContext { TrackName = "Monza", CurrentLap = 5 };
            var result = await service.QueryAsync("What's my fuel status?", context);

            Assert.False(result.Success);
            Assert.Equal("LLM service not available", result.Answer);
            Assert.Equal("LLM", result.Source);
            Assert.Equal("Ollama server not connected", result.Error);
        }

        [Fact]
        public async Task QueryAsync_ReturnsSuccessResponse_WithValidLLMResponse()
        {
            SetupSuccessfulConnection();

            var ollamaResponse = new
            {
                response = "You have enough fuel for 10 more laps.",
                done = true
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains("/api/generate") &&
                        req.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(ollamaResponse))
                });

            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);
            await service.TestConnectionAsync();

            var context = new RaceContext
            {
                TrackName = "Monza",
                CurrentLap = 5,
                FuelLevel = 50.0,
                FuelCapacity = 100.0
            };

            var result = await service.QueryAsync("What's my fuel status?", context);

            Assert.True(result.Success);
            Assert.Equal("You have enough fuel for 10 more laps.", result.Answer);
            Assert.Equal("LLM", result.Source);
            Assert.Equal(0.7, result.Confidence);
            Assert.True(result.ResponseTimeMs >= 0);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task QueryAsync_ReturnsTimeout_WhenRequestTakesTooLong()
        {
            SetupSuccessfulConnection();

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/generate")),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Request timed out"));

            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);
            await service.TestConnectionAsync();

            var context = new RaceContext { TrackName = "Monza" };
            var result = await service.QueryAsync("Test query", context);

            Assert.False(result.Success);
            Assert.Equal("LLM request timed out", result.Answer);
            Assert.Equal("Request timed out", result.Error);
            Assert.Equal(_options.LLMTimeoutMs, result.ResponseTimeMs);
        }

        [Fact]
        public async Task QueryAsync_HandlesHttpError_AndReturnsErrorResponse()
        {
            SetupSuccessfulConnection();

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/generate")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent("Internal server error")
                });

            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);
            await service.TestConnectionAsync();

            var context = new RaceContext { TrackName = "Monza" };
            var result = await service.QueryAsync("Test query", context);

            Assert.False(result.Success);
            Assert.Equal("Error querying LLM", result.Answer);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public async Task QueryAsync_HandlesException_AndMarksServiceUnavailable()
        {
            SetupSuccessfulConnection();

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/generate")),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new Exception("Network error"));

            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);
            await service.TestConnectionAsync();

            var context = new RaceContext { TrackName = "Monza" };
            var result = await service.QueryAsync("Test query", context);

            Assert.False(result.Success);
            Assert.False(service.IsAvailable);
            Assert.Equal("Error querying LLM", result.Answer);
            Assert.Equal("Network error", result.Error);
        }

        [Fact]
        public async Task QueryAsync_IncludesContextInPrompt()
        {
            SetupSuccessfulConnection();

            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/generate")),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        response = "Test response",
                        done = true
                    }))
                });

            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);
            await service.TestConnectionAsync();

            var context = new RaceContext
            {
                TrackName = "Monza",
                CurrentLap = 10,
                TotalLaps = 20
            };

            await service.QueryAsync("What's my position?", context);

            Assert.NotNull(capturedRequest);
            var requestContent = await capturedRequest!.Content!.ReadAsStringAsync();
            Assert.Contains(_options.LLMModel, requestContent);
            var requestJson = JsonSerializer.Deserialize<JsonElement>(requestContent);
            var prompt = requestJson.GetProperty("prompt").GetString();
            Assert.Contains("What's my position?", prompt);
        }

        [Fact]
        public async Task QueryAsync_UsesCorrectModelFromOptions()
        {
            SetupSuccessfulConnection();

            _options.LLMModel = "custom-model";
            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/generate")),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        response = "Test",
                        done = true
                    }))
                });

            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);
            await service.TestConnectionAsync();

            await service.QueryAsync("Test", new RaceContext());

            Assert.NotNull(capturedRequest);
            var requestContent = await capturedRequest!.Content!.ReadAsStringAsync();
            Assert.Contains("custom-model", requestContent);
        }

        [Fact]
        public async Task QueryAsync_HandlesMissingResponseField()
        {
            SetupSuccessfulConnection();

            var ollamaResponse = new
            {
                response = "",
                done = true
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/generate")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(ollamaResponse))
                });

            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);
            await service.TestConnectionAsync();

            var result = await service.QueryAsync("Test", new RaceContext());

            Assert.True(result.Success);
            Assert.Equal("", result.Answer);
        }

        [Fact]
        public async Task QueryAsync_HandlesNullResponseField()
        {
            SetupSuccessfulConnection();

            // Test with completely null result object
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/generate")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("null")
                });

            var service = new OllamaLLMService(_httpClient, _options, NullLogger<OllamaLLMService>.Instance);
            await service.TestConnectionAsync();

            var result = await service.QueryAsync("Test", new RaceContext());

            Assert.True(result.Success);
            Assert.Equal("No response from LLM", result.Answer);
        }

        private void SetupSuccessfulConnection()
        {
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/tags")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK
                });
        }
    }
}
