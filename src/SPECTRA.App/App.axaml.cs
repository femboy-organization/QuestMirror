using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SPECTRA.Core.Diagnostics;

namespace SPECTRA.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AppLog.Debug("Application", "Loading Avalonia application resources.");
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                AppLog.Debug("Application", "Creating the main window.");
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
            AppLog.Info("Application", "Avalonia framework initialization completed.");
        }
        catch (Exception exception)
        {
            AppLog.Exception("Application", "Avalonia framework initialization failed.", exception);
            throw;
        }
    }
}
