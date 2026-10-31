using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using SPECTRA.App.ViewModels;
using SPECTRA.Core.Diagnostics;

namespace SPECTRA.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        PreviewArea.SizeChanged += OnPreviewAreaSizeChanged;
        _viewModel = new MainViewModel(VideoHost);
        DataContext = _viewModel;

        Loaded += async (_, _) =>
        {
            var stopwatch = Stopwatch.StartNew();
            AppLog.Info("Window", "Main window loaded; initializing the view model.");
            try
            {
                await _viewModel.InitializeAsync();
                AppLog.Info("Performance", $"Main-window initialization completed in {stopwatch.ElapsedMilliseconds} ms.");
            }
            catch (Exception exception)
            {
                AppLog.Exception("Window", "Main-window initialization failed.", exception);
                throw;
            }
        };

        Closed += async (_, _) =>
        {
            AppLog.Info("Window", "Main window closed; disposing application services.");
            try
            {
                await _viewModel.DisposeAsync();
                AppLog.Info("Window", "Application services disposed.");
            }
            catch (Exception exception)
            {
                AppLog.Exception("Shutdown", "An error occurred while disposing application services.", exception);
            }
        };
    }


    private void OnPreviewAreaSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        const double aspectRatio = 16d / 9d;
        var frameWidth = Math.Min(e.NewSize.Width, e.NewSize.Height * aspectRatio);
        var frameHeight = frameWidth / aspectRatio;
        VideoFrame.Width = Math.Max(1d, Math.Floor(frameWidth));
        VideoFrame.Height = Math.Max(1d, Math.Floor(frameHeight));
    }

    private void OnFullscreenClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
        AppLog.Info("Window", $"Fullscreen button used; new window state: {WindowState}.");
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && WindowState == WindowState.FullScreen)
        {
            WindowState = WindowState.Normal;
            AppLog.Debug("Window", "Fullscreen exited with Escape.");
            e.Handled = true;
        }
    }
}
