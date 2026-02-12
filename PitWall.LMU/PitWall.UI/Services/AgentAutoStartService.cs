using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PitWall.UI.Services
{
    /// <summary>
    /// Ensures the local agent is running when the UI starts.
    /// </summary>
    public sealed class AgentAutoStartService
    {
        private const string AutoStartEnvVar = "PITWALL_AGENT_AUTOSTART";
        private readonly IApiProbe _probe;
        private readonly IProcessLauncher _launcher;
        private readonly ILogger _logger;
        private int _startAttempted;

        /// <summary>
        /// Creates a new agent auto-start service.
        /// </summary>
        public AgentAutoStartService(IApiProbe probe, IProcessLauncher launcher, ILogger logger)
        {
            _probe = probe ?? throw new ArgumentNullException(nameof(probe));
            _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts the agent if it is unavailable and the environment allows auto-start.
        /// </summary>
        public async Task EnsureAgentRunningAsync(Uri agentBase, string baseDirectory, CancellationToken cancellationToken)
        {
            if (!ShouldAutoStart())
            {
                _logger.LogInformation("Agent auto-start disabled via {EnvVar}.", AutoStartEnvVar);
                return;
            }

            if (!agentBase.IsLoopback)
            {
                _logger.LogInformation("Agent auto-start skipped for non-loopback base {AgentBase}.", agentBase);
                return;
            }

            if (await _probe.IsAvailableAsync(agentBase, cancellationToken))
            {
                _logger.LogInformation("Agent already responding at {AgentBase}.", agentBase);
                return;
            }

            if (Interlocked.Exchange(ref _startAttempted, 1) != 0)
            {
                return;
            }

            _logger.LogInformation("Agent auto-start attempting for {AgentBase}.", agentBase);

            if (!TryBuildAgentStartInfo(baseDirectory, agentBase, out var startInfo))
            {
                _logger.LogWarning("Agent auto-start failed: PitWall.Agent project not found from {BaseDirectory}.", baseDirectory);
                return;
            }

            try
            {
                var process = _launcher.Start(startInfo);
                if (process == null)
                {
                    _logger.LogWarning("Agent auto-start failed to launch process.");
                    return;
                }

                _logger.LogInformation("Agent auto-start launched using {WorkingDirectory}.", startInfo.WorkingDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent auto-start failed.");
            }
        }

        /// <summary>
        /// Builds a process start info for the agent project if found.
        /// </summary>
        public static bool TryBuildAgentStartInfo(string baseDirectory, Uri agentBase, out ProcessStartInfo startInfo)
        {
            startInfo = null!;
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return false;
            }

            if (!TryFindAgentProjectRoot(baseDirectory, out var rootPath, out var agentProjectPath))
            {
                return false;
            }

            var info = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = rootPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            info.ArgumentList.Add("run");
            info.ArgumentList.Add("--project");
            info.ArgumentList.Add(agentProjectPath);
            info.ArgumentList.Add("--no-build");

            info.Environment["ASPNETCORE_URLS"] = agentBase.ToString().TrimEnd('/');

            startInfo = info;
            return true;
        }

        private static bool ShouldAutoStart()
        {
            var value = Environment.GetEnvironmentVariable(AutoStartEnvVar);
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return !value.Equals("false", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("0", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryFindAgentProjectRoot(string baseDirectory, out string rootPath, out string agentProjectPath)
        {
            rootPath = string.Empty;
            agentProjectPath = string.Empty;

            var current = new DirectoryInfo(baseDirectory);
            for (var depth = 0; depth < 8 && current != null; depth++)
            {
                var candidate = Path.Combine(current.FullName, "PitWall.Agent", "PitWall.Agent.csproj");
                if (File.Exists(candidate))
                {
                    rootPath = current.FullName;
                    agentProjectPath = candidate;
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }
    }
}
