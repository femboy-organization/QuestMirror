using System.Diagnostics;
using SPECTRA.Core.Diagnostics;
using Avalonia;

namespace SPECTRA.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppLog.Initialize();
        Trace.Listeners.Add(new AppLogTraceListener());
        Trace.AutoFlush = true;

        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
        AppLog.Info("Startup",
            $"Starting SPECTRA {version}; PID={Environment.ProcessId}; OS={Environment.OSVersion}; " +
            $"runtime={Environment.Version}; baseDirectory={AppContext.BaseDirectory}");
        AppLog.Info("Startup", $"Diagnostics file: {AppLog.LogFilePath}");

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
                AppLog.Exception("Crash", $"Unhandled exception. IsTerminating={eventArgs.IsTerminating}.", exception);
            else
                AppLog.Error("Crash",
                    $"Unhandled non-Exception object. IsTerminating={eventArgs.IsTerminating}; value={eventArgs.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            AppLog.Exception("BackgroundTask", "An unobserved task exception was detected.", eventArgs.Exception);
            eventArgs.SetObserved();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            AppLog.Info("Shutdown", "SPECTRA process is exiting.");
            AppLog.Flush();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            AppLog.Info("Shutdown", "SPECTRA desktop lifetime ended normally.");
        }
        catch (Exception exception)
        {
            AppLog.Exception("Startup", "SPECTRA terminated after an unhandled exception.", exception);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
