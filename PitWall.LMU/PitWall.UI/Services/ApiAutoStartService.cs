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
    /// Probes API availability for auto-start decisions.
    /// </summary>
    public interface IApiProbe
    {
        /// <summary>
        /// Returns true when the API is reachable and responsive.
        /// </summary>
        Task<bool> IsAvailableAsync(Uri apiBase, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Probes the API by requesting a lightweight endpoint.
    /// </summary>
    public sealed class ApiProbe : IApiProbe
    {
        private static readonly HttpClient SharedClient = new()
        {
            Timeout = TimeSpan.FromMilliseconds(500)
        };
        private readonly TimeSpan _timeout;
        private readonly string _probePath;

        /// <summary>
        /// Creates a probe with a small timeout for local API checks.
        /// </summary>
        public ApiProbe(string probePath = "/api/sessions/count", TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(probePath))
            {
                throw new ArgumentException("Probe path cannot be empty.", nameof(probePath));
            }

            _probePath = probePath;
            _timeout = timeout ?? TimeSpan.FromMilliseconds(500);
        }

        /// <inheritdoc />
        public async Task<bool> IsAvailableAsync(Uri apiBase, CancellationToken cancellationToken)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_timeout);

                var requestUri = new Uri(apiBase, _probePath);
                using var response = await SharedClient.GetAsync(requestUri, cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Abstraction for launching external processes.
    /// </summary>
    public interface IProcessLauncher
    {
        /// <summary>
        /// Starts a process from the provided start info.
        /// </summary>
        Process? Start(ProcessStartInfo startInfo);
    }

    /// <summary>
    /// Default process launcher implementation.
    /// </summary>
    public sealed class ProcessLauncher : IProcessLauncher
    {
        /// <inheritdoc />
        public Process? Start(ProcessStartInfo startInfo)
        {
            return Process.Start(startInfo);
        }
    }

    /// <summary>
    /// Ensures the local API is running when the UI starts.
    /// </summary>
    public sealed class ApiAutoStartService
    {
        private const string AutoStartEnvVar = "PITWALL_API_AUTOSTART";
        private readonly IApiProbe _probe;
        private readonly IProcessLauncher _launcher;
        private readonly ILogger _logger;
        private int _startAttempted;

        /// <summary>
        /// Creates a new auto-start service.
        /// </summary>
        public ApiAutoStartService(IApiProbe probe, IProcessLauncher launcher, ILogger logger)
        {
            _probe = probe ?? throw new ArgumentNullException(nameof(probe));
            _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts the API if it is unavailable and the environment allows auto-start.
        /// </summary>
        public async Task EnsureApiRunningAsync(Uri apiBase, string baseDirectory, CancellationToken cancellationToken)
        {
            if (!ShouldAutoStart())
            {
                _logger.LogInformation("API auto-start disabled via {EnvVar}.", AutoStartEnvVar);
                return;
            }

            if (!apiBase.IsLoopback)
            {
                _logger.LogInformation("API auto-start skipped for non-loopback base {ApiBase}.", apiBase);
                return;
            }

            if (await _probe.IsAvailableAsync(apiBase, cancellationToken))
            {
                _logger.LogInformation("API already responding at {ApiBase}.", apiBase);
                return;
            }

            if (Interlocked.Exchange(ref _startAttempted, 1) != 0)
            {
                return;
            }

            _logger.LogInformation("API auto-start attempting for {ApiBase}.", apiBase);

            if (!TryBuildApiStartInfo(baseDirectory, apiBase, out var startInfo))
            {
                _logger.LogWarning("API auto-start failed: PitWall.Api project not found from {BaseDirectory}.", baseDirectory);
                return;
            }

            try
            {
                var process = _launcher.Start(startInfo);
                if (process == null)
                {
                    _logger.LogWarning("API auto-start failed to launch process.");
                    return;
                }

                _logger.LogInformation("API auto-start launched using {WorkingDirectory}.", startInfo.WorkingDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API auto-start failed.");
            }
        }

        /// <summary>
        /// Builds a process start info for the API project if found.
        /// </summary>
        public static bool TryBuildApiStartInfo(string baseDirectory, Uri apiBase, out ProcessStartInfo startInfo)
        {
            startInfo = null!;
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return false;
            }

            if (!TryFindApiProjectRoot(baseDirectory, out var rootPath, out var apiProjectPath))
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
            info.ArgumentList.Add(apiProjectPath);
            info.ArgumentList.Add("--no-build");

            info.Environment["ASPNETCORE_URLS"] = apiBase.ToString().TrimEnd('/');

            var telemetryDb = Environment.GetEnvironmentVariable("LMU_TELEMETRY_DB");
            if (!string.IsNullOrWhiteSpace(telemetryDb))
            {
                info.Environment["LMU_TELEMETRY_DB"] = telemetryDb;
            }

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

        private static bool TryFindApiProjectRoot(string baseDirectory, out string rootPath, out string apiProjectPath)
        {
            rootPath = string.Empty;
            apiProjectPath = string.Empty;

            var current = new DirectoryInfo(baseDirectory);
            for (var depth = 0; depth < 8 && current != null; depth++)
            {
                var candidate = Path.Combine(current.FullName, "PitWall.Api", "PitWall.Api.csproj");
                if (File.Exists(candidate))
                {
                    rootPath = current.FullName;
                    apiProjectPath = candidate;
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }
    }
}
