using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using PitWall.UI.ViewModels;
using PitWall.UI.Views;
using PitWall.UI.Services;
using System;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace PitWall.UI;

public partial class App : Application
{
    private ILoggerFactory? _loggerFactory;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "logs", "pitwall-ui-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    shared: true)
                .CreateLogger();

            _loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
            var appLogger = _loggerFactory.CreateLogger<App>();

            var apiBase = Environment.GetEnvironmentVariable("PITWALL_API_BASE") ?? "http://localhost:5236";
            var agentBase = Environment.GetEnvironmentVariable("PITWALL_AGENT_BASE") ?? "http://localhost:5139";

            var apiClient = new HttpClient { BaseAddress = new Uri(apiBase) };
            var agentClientHttp = new HttpClient { BaseAddress = new Uri(agentBase) };

            var apiAutoStart = new ApiAutoStartService(
                new ApiProbe(),
                new ProcessLauncher(),
                appLogger);
            _ = apiAutoStart.EnsureApiRunningAsync(new Uri(apiBase), AppContext.BaseDirectory, CancellationToken.None);

            var agentAutoStart = new AgentAutoStartService(
                new ApiProbe("/agent/health"),
                new ProcessLauncher(),
                appLogger);
            _ = agentAutoStart.EnsureAgentRunningAsync(new Uri(agentBase), AppContext.BaseDirectory, CancellationToken.None);

            var wsBase = BuildWebSocketBase(apiBase);
            var telemetryClient = new TelemetryStreamClient(wsBase, _loggerFactory.CreateLogger<TelemetryStreamClient>());
            var recommendationClient = new RecommendationClient(apiClient, _loggerFactory.CreateLogger<RecommendationClient>());
            var sessionClient = new SessionClient(apiClient, _loggerFactory.CreateLogger<SessionClient>());
            var agentClient = new AgentQueryClient(agentClientHttp, _loggerFactory.CreateLogger<AgentQueryClient>());
            var agentConfigClient = new AgentConfigClient(agentClientHttp, _loggerFactory.CreateLogger<AgentConfigClient>());

            appLogger.LogInformation("UI configured. API={ApiBase} Agent={AgentBase}", apiBase, agentBase);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    recommendationClient,
                    telemetryClient,
                    agentClient,
                    agentConfigClient,
                    sessionClient,
                    _loggerFactory.CreateLogger<MainWindowViewModel>(),
                    _loggerFactory),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private static Uri BuildWebSocketBase(string apiBase)
    {
        var uri = new Uri(apiBase);
        var scheme = uri.Scheme switch
        {
            "https" => "wss",
            "http" => "ws",
            _ => uri.Scheme
        };

        var builder = new UriBuilder(uri) { Scheme = scheme };
        return builder.Uri;
    }
}