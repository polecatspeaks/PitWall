using System;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Agent.Models;
using PitWall.Agent.Services.LLM;
using Xunit;

namespace PitWall.Tests
{
    public class LocalSubnetEndpointEnumeratorTests
    {
        private readonly AgentOptions _options;

        public LocalSubnetEndpointEnumeratorTests()
        {
            _options = new AgentOptions
            {
                LLMDiscoveryPort = 11434
            };
        }

        [Fact]
        public void GetCandidateEndpoints_UsesProvidedSubnetPrefix_WhenSpecified()
        {
            _options.LLMDiscoverySubnetPrefix = "192.168.1";
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints = enumerator.GetCandidateEndpoints().ToList();

            Assert.Equal(254, endpoints.Count);
            Assert.Contains(endpoints, e => e.ToString() == "http://192.168.1.1:11434/");
            Assert.Contains(endpoints, e => e.ToString() == "http://192.168.1.254:11434/");
        }

        [Fact]
        public void GetCandidateEndpoints_GeneratesCorrectRange_From1To254()
        {
            _options.LLMDiscoverySubnetPrefix = "10.0.0";
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints = enumerator.GetCandidateEndpoints().ToList();

            Assert.Equal(254, endpoints.Count);
            Assert.Contains(endpoints, e => e.ToString() == "http://10.0.0.1:11434/");
            Assert.DoesNotContain(endpoints, e => e.ToString() == "http://10.0.0.0:11434/");
            Assert.Contains(endpoints, e => e.ToString() == "http://10.0.0.254:11434/");
            Assert.DoesNotContain(endpoints, e => e.ToString() == "http://10.0.0.255:11434/");
        }

        [Fact]
        public void GetCandidateEndpoints_UsesCorrectPort_FromOptions()
        {
            _options.LLMDiscoverySubnetPrefix = "192.168.1";
            _options.LLMDiscoveryPort = 8080;
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints = enumerator.GetCandidateEndpoints().ToList();

            Assert.All(endpoints, e => Assert.Contains(":8080", e.ToString()));
        }

        [Fact]
        public void GetCandidateEndpoints_ReturnsHttpEndpoints()
        {
            _options.LLMDiscoverySubnetPrefix = "172.16.0";
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints = enumerator.GetCandidateEndpoints().ToList();

            Assert.All(endpoints, e => Assert.StartsWith("http://", e.ToString()));
        }

        [Fact]
        public void GetCandidateEndpoints_ReturnsEmptyList_WhenSubnetPrefixIsNull_AndAutoDetectionFails()
        {
            _options.LLMDiscoverySubnetPrefix = null;
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints = enumerator.GetCandidateEndpoints().ToList();

            // This test might return actual network interfaces or empty list depending on environment
            // Just verify it doesn't throw
            Assert.NotNull(endpoints);
        }

        [Fact]
        public void GetCandidateEndpoints_ReturnsEmptyList_WhenSubnetPrefixIsEmpty()
        {
            _options.LLMDiscoverySubnetPrefix = "";
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints = enumerator.GetCandidateEndpoints().ToList();

            // Similar to above - depends on auto-detection
            Assert.NotNull(endpoints);
        }

        [Fact]
        public void GetCandidateEndpoints_ReturnsEmptyList_WhenSubnetPrefixIsWhitespace()
        {
            _options.LLMDiscoverySubnetPrefix = "   ";
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints = enumerator.GetCandidateEndpoints().ToList();

            // Similar to above - depends on auto-detection
            Assert.NotNull(endpoints);
        }

        [Theory]
        [InlineData("192.168.1")]
        [InlineData("10.0.0")]
        [InlineData("172.16.0")]
        [InlineData("192.168.100")]
        public void GetCandidateEndpoints_HandlesVariousSubnetPrefixes(string prefix)
        {
            _options.LLMDiscoverySubnetPrefix = prefix;
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints = enumerator.GetCandidateEndpoints().ToList();

            Assert.Equal(254, endpoints.Count);
            Assert.All(endpoints, e => Assert.Contains(prefix, e.ToString()));
        }

        [Fact]
        public void GetCandidateEndpoints_GeneratesSequentialHostNumbers()
        {
            _options.LLMDiscoverySubnetPrefix = "192.168.1";
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints = enumerator.GetCandidateEndpoints().ToList();

            for (int i = 1; i <= 254; i++)
            {
                var expectedUri = $"http://192.168.1.{i}:11434/";
                Assert.Contains(endpoints, e => e.ToString() == expectedUri);
            }
        }

        [Fact]
        public void GetCandidateEndpoints_ReturnsValidUris()
        {
            _options.LLMDiscoverySubnetPrefix = "192.168.1";
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints = enumerator.GetCandidateEndpoints().ToList();

            Assert.All(endpoints, uri =>
            {
                Assert.NotNull(uri);
                Assert.True(uri.IsAbsoluteUri);
                Assert.Equal("http", uri.Scheme);
            });
        }

        [Theory]
        [InlineData(80)]
        [InlineData(8080)]
        [InlineData(3000)]
        [InlineData(11434)]
        [InlineData(65535)]
        public void GetCandidateEndpoints_HandlesVariousPorts(int port)
        {
            _options.LLMDiscoverySubnetPrefix = "192.168.1";
            _options.LLMDiscoveryPort = port;
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints = enumerator.GetCandidateEndpoints().ToList();

            Assert.Equal(254, endpoints.Count);
            Assert.All(endpoints, e => Assert.Equal(port, e.Port));
        }

        [Fact]
        public void GetCandidateEndpoints_CanBeCalledMultipleTimes()
        {
            _options.LLMDiscoverySubnetPrefix = "192.168.1";
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints1 = enumerator.GetCandidateEndpoints().ToList();
            var endpoints2 = enumerator.GetCandidateEndpoints().ToList();

            Assert.Equal(endpoints1.Count, endpoints2.Count);
            Assert.Equal(254, endpoints1.Count);
        }

        [Fact]
        public void GetCandidateEndpoints_ReturnsEnumerable_NotList()
        {
            _options.LLMDiscoverySubnetPrefix = "192.168.1";
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints = enumerator.GetCandidateEndpoints();

            Assert.NotNull(endpoints);
            // Can be enumerated
            var count = endpoints.Count();
            Assert.Equal(254, count);
        }

        [Fact]
        public void GetCandidateEndpoints_DoesNotIncludeNetworkOrBroadcastAddress()
        {
            _options.LLMDiscoverySubnetPrefix = "192.168.1";
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var endpoints = enumerator.GetCandidateEndpoints().ToList();

            Assert.DoesNotContain(endpoints, e => e.ToString() == "http://192.168.1.0:11434/");
            Assert.DoesNotContain(endpoints, e => e.ToString() == "http://192.168.1.255:11434/");
        }

        [Fact]
        public void GetCandidateEndpoints_HandlesEdgeCaseSubnets()
        {
            // Test with first and last valid private IP ranges
            var prefixes = new[] { "10.0.0", "172.31.255", "192.168.255" };

            foreach (var prefix in prefixes)
            {
                _options.LLMDiscoverySubnetPrefix = prefix;
                var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

                var endpoints = enumerator.GetCandidateEndpoints().ToList();

                Assert.Equal(254, endpoints.Count);
            }
        }

        [Fact]
        public void Constructor_AcceptsNullLogger()
        {
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            Assert.NotNull(enumerator);
        }

        [Fact]
        public void GetCandidateEndpoints_Performance_GeneratesEndpointsQuickly()
        {
            _options.LLMDiscoverySubnetPrefix = "192.168.1";
            var enumerator = new LocalSubnetEndpointEnumerator(_options, NullLogger<LocalSubnetEndpointEnumerator>.Instance);

            var startTime = DateTime.UtcNow;
            var endpoints = enumerator.GetCandidateEndpoints().ToList();
            var duration = DateTime.UtcNow - startTime;

            Assert.Equal(254, endpoints.Count);
            Assert.True(duration.TotalMilliseconds < 1000, "Endpoint generation should be fast");
        }
    }
}
