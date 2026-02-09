using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Agent.Models;
using PitWall.Agent.Services.LLM;
using Xunit;

namespace PitWall.Tests
{
    public class CloudLlmServiceTests
    {
        [Fact]
        public async Task OpenAi_QueryAsync_ReturnsResponseText()
        {
            var handler = new StubHttpHandler(request =>
            {
                var payload = "{\"choices\":[{\"message\":{\"content\":\"Hello driver\"}}]}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.openai.com")
            };

            var options = new AgentOptions
            {
                EnableLLM = true,
                LLMProvider = "OpenAI",
                OpenAIApiKey = "test-key",
                OpenAIModel = "gpt-4o-mini"
            };

            var service = new OpenAiLlmService(httpClient, options, NullLogger<OpenAiLlmService>.Instance);
            var response = await service.QueryAsync("How is my pace?", new RaceContext());

            Assert.True(response.Success);
            Assert.Equal("Hello driver", response.Answer);
            Assert.Equal("LLM", response.Source);
        }

        [Fact]
        public async Task Anthropic_QueryAsync_ReturnsResponseText()
        {
            var handler = new StubHttpHandler(request =>
            {
                var payload = "{\"content\":[{\"text\":\"Tire temps look good\"}]}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.anthropic.com")
            };

            var options = new AgentOptions
            {
                EnableLLM = true,
                LLMProvider = "Anthropic",
                AnthropicApiKey = "test-key",
                AnthropicModel = "claude-3-5-sonnet"
            };

            var service = new AnthropicLlmService(httpClient, options, NullLogger<AnthropicLlmService>.Instance);
            var response = await service.QueryAsync("How are the tires?", new RaceContext());

            Assert.True(response.Success);
            Assert.Equal("Tire temps look good", response.Answer);
            Assert.Equal("LLM", response.Source);
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
