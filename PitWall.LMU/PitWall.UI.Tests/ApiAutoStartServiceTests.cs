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
    public class ApiAutoStartServiceTests
    {
        [Fact]
        public async Task EnsureApiRunningAsync_AutostartDisabled_DoesNotLaunch()
        {
            var original = Environment.GetEnvironmentVariable("PITWALL_API_AUTOSTART");
            Environment.SetEnvironmentVariable("PITWALL_API_AUTOSTART", "false");

            try
            {
                var probe = new FakeApiProbe(false);
                var launcher = new FakeProcessLauncher();
                var service = new ApiAutoStartService(probe, launcher, NullLogger<ApiAutoStartService>.Instance);

                await service.EnsureApiRunningAsync(new Uri("http://localhost:5236"), AppContext.BaseDirectory, CancellationToken.None);

                Assert.False(launcher.StartCalled);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PITWALL_API_AUTOSTART", original);
            }
        }

        [Fact]
        public async Task EnsureApiRunningAsync_ApiAvailable_DoesNotLaunch()
        {
            var original = Environment.GetEnvironmentVariable("PITWALL_API_AUTOSTART");
            Environment.SetEnvironmentVariable("PITWALL_API_AUTOSTART", null);

            try
            {
                var probe = new FakeApiProbe(true);
                var launcher = new FakeProcessLauncher();
                var service = new ApiAutoStartService(probe, launcher, NullLogger<ApiAutoStartService>.Instance);

                await service.EnsureApiRunningAsync(new Uri("http://localhost:5236"), AppContext.BaseDirectory, CancellationToken.None);

                Assert.False(launcher.StartCalled);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PITWALL_API_AUTOSTART", original);
            }
        }

        [Fact]
        public async Task EnsureApiRunningAsync_ApiUnavailable_LaunchesApi()
        {
            var original = Environment.GetEnvironmentVariable("PITWALL_API_AUTOSTART");
            Environment.SetEnvironmentVariable("PITWALL_API_AUTOSTART", null);

            var root = Path.Combine(Path.GetTempPath(), "PitWallAutoStart", Guid.NewGuid().ToString("N"));
            var apiDir = Path.Combine(root, "PitWall.Api");
            var uiDir = Path.Combine(root, "PitWall.UI", "bin", "Debug", "net9.0");
            Directory.CreateDirectory(apiDir);
            Directory.CreateDirectory(uiDir);
            File.WriteAllText(Path.Combine(apiDir, "PitWall.Api.csproj"), "<Project></Project>");

            try
            {
                var probe = new FakeApiProbe(false);
                var launcher = new FakeProcessLauncher();
                var service = new ApiAutoStartService(probe, launcher, NullLogger<ApiAutoStartService>.Instance);

                await service.EnsureApiRunningAsync(new Uri("http://localhost:5236"), uiDir, CancellationToken.None);

                Assert.True(launcher.StartCalled);
                Assert.NotNull(launcher.StartInfo);
                Assert.Equal(root, launcher.StartInfo!.WorkingDirectory);
                Assert.Contains("run", launcher.StartInfo.ArgumentList);
                Assert.Contains("--project", launcher.StartInfo.ArgumentList);
                Assert.Contains(Path.Combine(apiDir, "PitWall.Api.csproj"), launcher.StartInfo.ArgumentList);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PITWALL_API_AUTOSTART", original);
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
