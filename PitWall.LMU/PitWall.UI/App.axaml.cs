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

namespace PitWall.UI;

public partial class App : Application
{
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

            var apiBase = Environment.GetEnvironmentVariable("PITWALL_API_BASE") ?? "http://localhost:5236";
            var agentBase = Environment.GetEnvironmentVariable("PITWALL_AGENT_BASE") ?? "http://localhost:5139";

            var apiClient = new HttpClient { BaseAddress = new Uri(apiBase) };
            var agentClientHttp = new HttpClient { BaseAddress = new Uri(agentBase) };

            var wsBase = BuildWebSocketBase(apiBase);
            var telemetryClient = new TelemetryStreamClient(wsBase);
            var recommendationClient = new RecommendationClient(apiClient);
            var sessionClient = new SessionClient(apiClient);
            var agentClient = new AgentQueryClient(agentClientHttp);
            var agentConfigClient = new AgentConfigClient(agentClientHttp);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(recommendationClient, telemetryClient, agentClient, agentConfigClient, sessionClient),
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