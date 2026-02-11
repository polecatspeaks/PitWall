using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using PitWall.Agent.Models;

namespace PitWall.Agent.Services.LLM
{
    public class LocalSubnetEndpointEnumerator : ILlmEndpointEnumerator
    {
        private readonly AgentOptions _options;
        private readonly ILogger<LocalSubnetEndpointEnumerator> _logger;

        public LocalSubnetEndpointEnumerator(AgentOptions options, ILogger<LocalSubnetEndpointEnumerator> logger)
        {
            _options = options;
            _logger = logger;
        }

        public IEnumerable<Uri> GetCandidateEndpoints()
        {
            var prefix = string.IsNullOrWhiteSpace(_options.LLMDiscoverySubnetPrefix)
                ? GetLocalSubnetPrefix()
                : _options.LLMDiscoverySubnetPrefix;

            if (string.IsNullOrWhiteSpace(prefix))
            {
                _logger.LogWarning("Unable to determine subnet prefix for LLM discovery");
                return Array.Empty<Uri>();
            }

            var port = _options.LLMDiscoveryPort;
            var endpoints = new List<Uri>();

            _logger.LogDebug("Enumerating LLM discovery endpoints on {Prefix}.x:{Port}", prefix, port);

            for (var host = 1; host <= 254; host++)
            {
                endpoints.Add(new Uri($"http://{prefix}.{host}:{port}"));
            }

            _logger.LogDebug("Generated {EndpointCount} discovery endpoints.", endpoints.Count);

            return endpoints;
        }

        private string? GetLocalSubnetPrefix()
        {
            try
            {
                var address = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                    .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                    .Select(addr => addr.Address)
                    .FirstOrDefault(addr => addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                             && !IPAddress.IsLoopback(addr));

                if (address == null)
                {
                    return null;
                }

                var bytes = address.GetAddressBytes();
                return $"{bytes[0]}.{bytes[1]}.{bytes[2]}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to determine local subnet prefix");
                return null;
            }
        }
    }
}
