using Avalonia;
using System;
using System.Threading.Tasks;

namespace LMUMemoryReader;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            StartupLogger.Info("Starting application.");
            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
                StartupLogger.Error("Unhandled exception", eventArgs.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
                StartupLogger.Error("Unobserved task exception", eventArgs.Exception);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            StartupLogger.Info("Application exited normally.");
        }
        catch (Exception ex)
        {
            StartupLogger.Error("Fatal startup failure", ex);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
