using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.UI.Services;
using Xunit;

namespace PitWall.UI.Tests
{
    public class AgentAutoStartServiceTests
    {
        [Fact]
        public async Task EnsureAgentRunningAsync_AutostartDisabled_DoesNotLaunch()
        {
            var original = Environment.GetEnvironmentVariable("PITWALL_AGENT_AUTOSTART");
            Environment.SetEnvironmentVariable("PITWALL_AGENT_AUTOSTART", "false");

            try
            {
                var probe = new FakeApiProbe(false);
                var launcher = new FakeProcessLauncher();
                var service = new AgentAutoStartService(probe, launcher, NullLogger<AgentAutoStartService>.Instance);

                await service.EnsureAgentRunningAsync(new Uri("http://localhost:5139"), AppContext.BaseDirectory, CancellationToken.None);

                Assert.False(launcher.StartCalled);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PITWALL_AGENT_AUTOSTART", original);
            }
        }

        [Fact]
        public async Task EnsureAgentRunningAsync_AgentAvailable_DoesNotLaunch()
        {
            var original = Environment.GetEnvironmentVariable("PITWALL_AGENT_AUTOSTART");
            Environment.SetEnvironmentVariable("PITWALL_AGENT_AUTOSTART", null);

            try
            {
                var probe = new FakeApiProbe(true);
                var launcher = new FakeProcessLauncher();
                var service = new AgentAutoStartService(probe, launcher, NullLogger<AgentAutoStartService>.Instance);

                await service.EnsureAgentRunningAsync(new Uri("http://localhost:5139"), AppContext.BaseDirectory, CancellationToken.None);

                Assert.False(launcher.StartCalled);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PITWALL_AGENT_AUTOSTART", original);
            }
        }

        [Fact]
        public async Task EnsureAgentRunningAsync_AgentUnavailable_LaunchesAgent()
        {
            var original = Environment.GetEnvironmentVariable("PITWALL_AGENT_AUTOSTART");
            Environment.SetEnvironmentVariable("PITWALL_AGENT_AUTOSTART", null);

            var root = Path.Combine(Path.GetTempPath(), "PitWallAgentAutoStart", Guid.NewGuid().ToString("N"));
            var agentDir = Path.Combine(root, "PitWall.Agent");
            var uiDir = Path.Combine(root, "PitWall.UI", "bin", "Debug", "net9.0");
            Directory.CreateDirectory(agentDir);
            Directory.CreateDirectory(uiDir);
            File.WriteAllText(Path.Combine(agentDir, "PitWall.Agent.csproj"), "<Project></Project>");

            try
            {
                var probe = new FakeApiProbe(false);
                var launcher = new FakeProcessLauncher();
                var service = new AgentAutoStartService(probe, launcher, NullLogger<AgentAutoStartService>.Instance);

                await service.EnsureAgentRunningAsync(new Uri("http://localhost:5139"), uiDir, CancellationToken.None);

                Assert.True(launcher.StartCalled);
                Assert.NotNull(launcher.StartInfo);
                Assert.Equal(root, launcher.StartInfo!.WorkingDirectory);
                Assert.Contains("run", launcher.StartInfo.ArgumentList);
                Assert.Contains("--project", launcher.StartInfo.ArgumentList);
                Assert.Contains(Path.Combine(agentDir, "PitWall.Agent.csproj"), launcher.StartInfo.ArgumentList);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PITWALL_AGENT_AUTOSTART", original);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private sealed class FakeApiProbe : IApiProbe
        {
            private readonly bool _available;

            public FakeApiProbe(bool available)
            {
                _available = available;
            }

            public Task<bool> IsAvailableAsync(Uri apiBase, CancellationToken cancellationToken)
            {
                return Task.FromResult(_available);
            }
        }

        private sealed class FakeProcessLauncher : IProcessLauncher
        {
            public bool StartCalled { get; private set; }
            public ProcessStartInfo? StartInfo { get; private set; }

            public Process? Start(ProcessStartInfo startInfo)
            {
                StartCalled = true;
                StartInfo = startInfo;
                return null;
            }
        }
    }
}
