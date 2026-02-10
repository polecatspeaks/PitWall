using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Threading;
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

            // Add global exception handlers
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                Console.WriteLine($"[FATAL] Unhandled exception: {ex?.Message}");
                Console.WriteLine($"Stack trace: {ex?.StackTrace}");
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Console.WriteLine($"[ERROR] Unobserved task exception: {args.Exception?.Message}");
                Console.WriteLine($"Stack trace: {args.Exception?.StackTrace}");
                args.SetObserved();
            };

            // Handle UI thread exceptions
            Dispatcher.UIThread.UnhandledException += (sender, args) =>
            {
                Console.WriteLine($"[ERROR] UI Thread exception: {args.Exception?.Message}");
                Console.WriteLine($"Stack trace: {args.Exception?.StackTrace}");
                args.Handled = true; // Prevent crash, continue running
            };

            var apiBase = Environment.GetEnvironmentVariable("PITWALL_API_BASE") ?? "http://localhost:5000";
            Console.WriteLine($"PitWall UI starting with API base: {apiBase}");
            
            var httpClient = new HttpClient { BaseAddress = new Uri(apiBase) };

            var wsBase = BuildWebSocketBase(apiBase);
            var telemetryClient = new TelemetryStreamClient(wsBase);
            var recommendationClient = new RecommendationClient(httpClient);
            var agentClient = new AgentQueryClient(httpClient);
            var agentConfigClient = new AgentConfigClient(httpClient);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(recommendationClient, telemetryClient, agentClient, agentConfigClient),
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