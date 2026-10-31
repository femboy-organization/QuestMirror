using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SPECTRA.App.Platforms;
using SPECTRA.Core.Devices;
using SPECTRA.Core.Diagnostics;
using SPECTRA.Core.Settings;
using SPECTRA.Core.Streaming;

namespace SPECTRA.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly AdbService _adb = new();
    private readonly ScrcpyStreamingService _streaming = new();
    private readonly SettingsService _settingsService;
    private readonly IVideoSurface _videoSurface;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly AppSettings _settings;
    private Task? _monitorTask;
    private ScrcpySession? _session;
    private string? _sessionSerial;
    private string? _lastDeviceSnapshot;
    private AdbDevice? _selectedDevice;
    private string _connectionSummary = "Checking for a headset…";
    private string _activityTitle = "Ready";
    private string _activityMessage = "Connect your Quest by USB or configure Wireless debugging.";
    private string _pairingEndpoint = string.Empty;
    private string _pairingCode = string.Empty;
    private string _deviceEndpoint = string.Empty;
    private bool _automaticQuality;
    private int _selectedResolution;
    private int _selectedFps;
    private int _selectedBitrateMbps;
    private string _selectedEye = "Left";
    private double _rotationDegrees;
    private bool _isBusy;
    private bool _isStreaming;

    public MainViewModel(IVideoSurface videoSurface, SettingsService? settingsService = null)
    {
        _videoSurface = videoSurface ?? throw new ArgumentNullException(nameof(videoSurface));
        _settingsService = settingsService ?? new SettingsService();
        _settings = _settingsService.Load();

        _automaticQuality = _settings.AutomaticQuality;
        _selectedResolution = _settings.Resolution;
        _selectedFps = _settings.FramesPerSecond;
        _selectedBitrateMbps = _settings.BitrateMbps;
        _selectedEye = _settings.VideoEye;
        _rotationDegrees = _settings.RotationDegrees;
        _pairingEndpoint = _settings.PairingEndpoint;
        _deviceEndpoint = _settings.DeviceEndpoint;

        AppLog.Info("Settings",
            $"Settings loaded. automaticQuality={_automaticQuality}; resolution={_selectedResolution}; " +
            $"fps={_selectedFps}; bitrateMbps={_selectedBitrateMbps}; " +
            $"pairingEndpointConfigured={!string.IsNullOrWhiteSpace(_pairingEndpoint)}; " +
            $"deviceEndpointConfigured={!string.IsNullOrWhiteSpace(_deviceEndpoint)}.");

        Devices = [];
        ResolutionOptions = new ObservableCollection<int>(AppSettings.AllowedResolutions);
        FrameRateOptions = new ObservableCollection<int>(AppSettings.AllowedFrameRates);
        BitrateOptions = new ObservableCollection<int>(AppSettings.AllowedBitrates);
        EyeOptions = new ObservableCollection<string>(["Left", "Right"]);

        RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync, () => CanRefreshDevices);
        PairWirelessCommand = new AsyncRelayCommand(PairWirelessAsync, () => CanChooseDevice);
        ConnectWirelessCommand = new AsyncRelayCommand(ConnectWirelessAsync, () => CanChooseDevice);
        StartStreamingCommand = new AsyncRelayCommand(StartStreamingAsync, () => CanStartStream);
        StopStreamingCommand = new AsyncRelayCommand(StopStreamingAsync, () => CanStopStream);
    }

    public ObservableCollection<AdbDevice> Devices { get; }
    public ObservableCollection<int> ResolutionOptions { get; }
    public ObservableCollection<int> FrameRateOptions { get; }
    public ObservableCollection<int> BitrateOptions { get; }
    public ObservableCollection<string> EyeOptions { get; }

    public IAsyncRelayCommand RefreshDevicesCommand { get; }
    public IAsyncRelayCommand PairWirelessCommand { get; }
    public IAsyncRelayCommand ConnectWirelessCommand { get; }
    public IAsyncRelayCommand StartStreamingCommand { get; }
    public IAsyncRelayCommand StopStreamingCommand { get; }

    public AdbDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                AppLog.Debug("UI", $"Selected device changed to '{value?.Serial ?? "(none)"}' (state={value?.State ?? "(none)"}).");
                OnPropertyChanged(nameof(IsDeviceAuthorized));
                OnPropertyChanged(nameof(CanStartStream));
                OnPropertyChanged(nameof(DeviceStateText));
                OnPropertyChanged(nameof(ConnectionDotBrush));
                UpdateConnectionSummary();
                NotifyCommands();
            }
        }
    }

    public string ConnectionSummary
    {
        get => _connectionSummary;
        private set => SetProperty(ref _connectionSummary, value);
    }

    public string ActivityTitle
    {
        get => _activityTitle;
        private set => SetProperty(ref _activityTitle, value);
    }

    public string ActivityMessage
    {
        get => _activityMessage;
        private set => SetProperty(ref _activityMessage, value);
    }

    public string PairingEndpoint
    {
        get => _pairingEndpoint;
        set
        {
            if (SetProperty(ref _pairingEndpoint, value ?? string.Empty))
            {
                _settings.PairingEndpoint = _pairingEndpoint;
                PersistSettings();
            }
        }
    }

    public string PairingCode
    {
        get => _pairingCode;
        set => SetProperty(ref _pairingCode, value ?? string.Empty);
    }

    public string DeviceEndpoint
    {
        get => _deviceEndpoint;
        set
        {
            if (SetProperty(ref _deviceEndpoint, value ?? string.Empty))
            {
                _settings.DeviceEndpoint = _deviceEndpoint;
                PersistSettings();
            }
        }
    }

    public bool IsAutomaticQuality
    {
        get => _automaticQuality;
        set
        {
            if (SetProperty(ref _automaticQuality, value))
            {
                _settings.AutomaticQuality = value;
                OnPropertyChanged(nameof(QualityDescription));
                OnPropertyChanged(nameof(ManualQualityOpacity));
                OnPropertyChanged(nameof(CanEditQualityAndManualQuality));
                PersistSettings();
            }
        }
    }

    public int SelectedResolution
    {
        get => _selectedResolution;
        set
        {
            var safe = AppSettings.AllowedResolutions.Contains(value) ? value : 1920;
            if (SetProperty(ref _selectedResolution, safe))
            {
                _settings.Resolution = safe;
                PersistSettings();
                OnPropertyChanged(nameof(QualityDescription));
            }
        }
    }

    public int SelectedFps
    {
        get => _selectedFps;
        set
        {
            var safe = AppSettings.AllowedFrameRates.Contains(value) ? value : 60;
            if (SetProperty(ref _selectedFps, safe))
            {
                _settings.FramesPerSecond = safe;
                PersistSettings();
                OnPropertyChanged(nameof(QualityDescription));
            }
        }
    }

    public int SelectedBitrateMbps
    {
        get => _selectedBitrateMbps;
        set
        {
            var safe = AppSettings.AllowedBitrates.Contains(value) ? value : 8;
            if (SetProperty(ref _selectedBitrateMbps, safe))
            {
                _settings.BitrateMbps = safe;
                PersistSettings();
                OnPropertyChanged(nameof(QualityDescription));
            }
        }
    }

    public string SelectedEye
    {
        get => _selectedEye;
        set
        {
            var safe = string.Equals(value, "Right", StringComparison.OrdinalIgnoreCase) ? "Right" : "Left";
            if (SetProperty(ref _selectedEye, safe))
            {
                _settings.VideoEye = safe;
                PersistSettings();
                AppLog.Info("Settings", $"Preview eye selection changed to {safe}.");
            }
        }
    }

    public double RotationDegrees
    {
        get => _rotationDegrees;
        set
        {
            var safe = Math.Clamp(Math.Round(value), -180d, 180d);
            if (SetProperty(ref _rotationDegrees, safe))
            {
                _settings.RotationDegrees = (int)safe;
                OnPropertyChanged(nameof(RotationDescription));
                PersistSettings();
                AppLog.Info("Settings", $"Preview rotation changed to {(int)safe} degrees clockwise.");
            }
        }
    }

    public string RotationDescription => $"{(int)RotationDegrees}°";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                NotifyState();
        }
    }

    public bool IsStreaming
    {
        get => _isStreaming;
        private set
        {
            if (SetProperty(ref _isStreaming, value))
            {
                NotifyState();
                OnPropertyChanged(nameof(IsVideoPlaceholderVisible));
            }
        }
    }

    public bool IsVideoPlaceholderVisible => !IsStreaming;
    public bool IsDeviceAuthorized => SelectedDevice?.State == "device";
    public bool CanRefreshDevices => !IsBusy;
    public bool CanChooseDevice => !IsBusy && !IsStreaming;
    public bool CanEditQuality => !IsBusy && !IsStreaming;
    public bool CanEditVideo => !IsBusy && !IsStreaming;
    public bool CanEditQualityAndManualQuality => CanEditQuality && !IsAutomaticQuality;
    public double ManualQualityOpacity => IsAutomaticQuality ? 0.56 : 1.0;
    public IBrush ConnectionDotBrush => IsStreaming
        ? Brushes.SeaGreen
        : IsDeviceAuthorized ? Brushes.OliveDrab : Brushes.SandyBrown;
    public bool CanStartStream => IsDeviceAuthorized && !IsBusy && !IsStreaming;
    public bool CanStopStream => IsStreaming && !IsBusy;
    public string DeviceStateText => SelectedDevice is null
        ? "No device selected"
        : SelectedDevice.State switch
        {
            "device" => "Authorized by ADB",
            "unauthorized" => "Unlock the headset and accept the debugging prompt",
            "offline" => "Device is offline",
            _ => $"Device state: {SelectedDevice.State}"
        };

    public string QualityDescription => IsAutomaticQuality
        ? "Automatic preset · 1920 px · 60 FPS · 8 Mbps"
        : $"Manual · {SelectedResolution} px · {SelectedFps} FPS · {SelectedBitrateMbps} Mbps";

    public async Task InitializeAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        AppLog.Info("Lifecycle", "View-model initialization started.");

        try
        {
            await RefreshDevicesAsync();
            _monitorTask ??= MonitorDevicesAsync(_lifetime.Token);
            AppLog.Info("Performance", $"View-model initialization completed in {stopwatch.ElapsedMilliseconds} ms.");
        }
        catch (Exception exception)
        {
            AppLog.Exception("Lifecycle", "View-model initialization failed.", exception);
            throw;
        }
    }

    public async Task RefreshDevicesAsync()
    {
        if (!await _operationLock.WaitAsync(0, _lifetime.Token))
            return;

        try
        {
            IsBusy = true;
            ActivityTitle = "Refreshing devices";
            await RefreshDevicesCoreAsync(silent: false, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            AppLog.Exception("ADB", "Device refresh failed.", exception);
            ActivityTitle = "Device scan failed";
            ActivityMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
            _operationLock.Release();
        }
    }

    public async Task PairWirelessAsync()
    {
        if (!await _operationLock.WaitAsync(0, _lifetime.Token))
            return;

        try
        {
            IsBusy = true;
            ActivityTitle = "Pairing over Wi-Fi";
            AppLog.Info("ADB", $"Wireless pairing started; endpoint='{PairingEndpoint}'. Pairing code is deliberately not logged.");
            var result = await _adb.PairAsync(PairingEndpoint, PairingCode, _lifetime.Token);
            ActivityMessage = result.Message;
            ActivityTitle = result.IsSuccess ? "Wireless pairing complete" : "Wireless pairing failed";
            AppLog.Info("ADB", $"Wireless pairing completed; success={result.IsSuccess}. Response details are available in the UI.");
            if (result.IsSuccess)
            {
                PairingCode = string.Empty;
                ActivityMessage += " Enter the separate device endpoint below and connect.";
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            AppLog.Exception("ADB", "Wireless pairing threw an exception.", exception);
            ActivityTitle = "Wireless pairing failed";
            ActivityMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
            _operationLock.Release();
        }
    }

    public async Task ConnectWirelessAsync()
    {
        if (!await _operationLock.WaitAsync(0, _lifetime.Token))
            return;

        try
        {
            IsBusy = true;
            ActivityTitle = "Connecting over Wi-Fi";
            AppLog.Info("ADB", $"Wireless connection requested for endpoint '{DeviceEndpoint}'.");
            var result = await _adb.ConnectAsync(DeviceEndpoint, _lifetime.Token);
            ActivityTitle = result.IsSuccess ? "Wireless connection confirmed" : "Wireless connection failed";
            ActivityMessage = result.Message;
            AppLog.Info("ADB", $"Wireless connection completed; success={result.IsSuccess}; message='{result.Message.Trim()}'.");
            if (result.IsSuccess)
                await RefreshDevicesCoreAsync(silent: true, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            AppLog.Exception("ADB", "Wireless connection threw an exception.", exception);
            ActivityTitle = "Wireless connection failed";
            ActivityMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
            _operationLock.Release();
        }
    }

    public async Task StartStreamingAsync()
    {
        if (!await _operationLock.WaitAsync(0, _lifetime.Token))
            return;

        var stopwatch = Stopwatch.StartNew();
        ScrcpySession? session = null;
        try
        {
            if (!IsDeviceAuthorized || SelectedDevice is null)
            {
                ActivityTitle = "A device is required";
                ActivityMessage = SelectedDevice?.State == "unauthorized"
                    ? "Unlock the headset and accept the USB debugging prompt, then refresh devices."
                    : "Connect a Quest using USB or Wireless debugging before starting a stream.";
                AppLog.Warn("Streaming", $"Stream start rejected; selected device state='{SelectedDevice?.State ?? "(none)"}'.");
                return;
            }

            IsBusy = true;
            ActivityTitle = "Starting stream";
            ActivityMessage = "Starting scrcpy and waiting for its video window.";
            var serial = SelectedDevice.Serial;
            var options = StreamingOptions.FromSettings(_settings);
            AppLog.Info("Streaming",
                $"Starting stream; device='{serial}'; qualityMode={(options.IsAutomatic ? "automatic" : "manual")}; " +
                $"maxSize={options.MaxSize}; fps={options.FramesPerSecond}; bitrateMbps={options.BitrateMbps}; " +
                $"eye={options.VideoEye}; rotationDegrees={options.RotationDegrees}.");

            session = await _streaming.StartAsync(serial, options, _lifetime.Token);
            await _videoSurface.AttachAsync(session, _lifetime.Token);

            _session = session;
            _sessionSerial = serial;
            IsStreaming = true;
            ConnectionSummary = serial.Contains(':')
                ? "Streaming over Wi-Fi"
                : "Streaming over USB";
            ActivityTitle = "Streaming";
            ActivityMessage = $"Showing a flat 16:9 {options.VideoEye.ToLowerInvariant()} eye view, rotated {options.RotationDegrees}°, " +
                $"at {options.MaxSize} px, {options.FramesPerSecond} FPS, {options.BitrateMbps} Mbps.";
            AppLog.Info("Performance",
                $"Video stream is attached. scrcpyPid={session.ProcessId}; startupAndAttachMs={stopwatch.ElapsedMilliseconds}.");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            AppLog.Debug("Streaming", "Stream startup was cancelled because the application is closing.");
            _videoSurface.Detach();
            if (session is not null)
                await session.StopAsync();
        }
        catch (Exception exception)
        {
            AppLog.Exception("Streaming", "Could not start or attach the scrcpy stream.", exception);
            _videoSurface.Detach();
            if (session is not null)
                await session.StopAsync();
            ActivityTitle = "Could not start stream";
            ActivityMessage = exception.Message;
            IsStreaming = false;
        }
        finally
        {
            IsBusy = false;
            _operationLock.Release();
        }
    }

    public async Task StopStreamingAsync()
    {
        if (!await _operationLock.WaitAsync(0, _lifetime.Token))
            return;

        try
        {
            IsBusy = true;
            AppLog.Info("Streaming", "User requested that streaming stop.");
            await StopStreamingCoreAsync("Streaming stopped.");
        }
        catch (Exception exception)
        {
            AppLog.Exception("Streaming", "Error stopping the stream.", exception);
            ActivityTitle = "Error stopping stream";
            ActivityMessage = exception.Message;
            IsStreaming = false;
        }
        finally
        {
            IsBusy = false;
            _operationLock.Release();
        }
    }

    private async Task RefreshDevicesCoreAsync(bool silent, CancellationToken cancellationToken)
    {
        var previousSerial = SelectedDevice?.Serial;
        var result = await _adb.ListDevicesAsync(cancellationToken);
        var snapshot = result.IsAvailable
            ? string.Join(";", result.Devices.OrderBy(device => device.Serial, StringComparer.Ordinal)
                .Select(device => $"{device.Serial}={device.State}"))
            : "<unavailable>";

        if (!string.Equals(snapshot, _lastDeviceSnapshot, StringComparison.Ordinal))
        {
            _lastDeviceSnapshot = snapshot;
            if (!result.IsAvailable)
                AppLog.Warn("ADB", $"ADB is unavailable: {result.Message}");
            else if (result.Devices.Count == 0)
                AppLog.Info("ADB", "ADB is available, but no devices are currently connected.");
            else
                AppLog.Info("ADB", $"ADB device snapshot changed: {snapshot}.");
        }

        if (!result.IsAvailable)
        {
            if (_session is not null)
                await StopStreamingCoreAsync("ADB did not confirm the device connection; streaming has stopped.");
            Devices.Clear();
            SelectedDevice = null;
            ConnectionSummary = "ADB unavailable";
            if (!silent)
            {
                ActivityTitle = "ADB is unavailable";
                ActivityMessage = result.Message;
            }
            return;
        }

        if (_sessionSerial is not null &&
            !result.Devices.Any(device => device.Serial == _sessionSerial && device.State == "device"))
        {
            AppLog.Warn("ADB", $"The streaming device '{_sessionSerial}' is no longer connected or authorized.");
            await StopStreamingCoreAsync("The selected headset disconnected or is no longer authorized. Streaming has stopped.");
        }

        Devices.Clear();
        foreach (var device in result.Devices)
            Devices.Add(device);

        SelectedDevice = previousSerial is null
            ? Devices.FirstOrDefault(device => device.State == "device") ?? Devices.FirstOrDefault()
            : Devices.FirstOrDefault(device => device.Serial == previousSerial)
              ?? Devices.FirstOrDefault(device => device.State == "device")
              ?? Devices.FirstOrDefault();

        if (SelectedDevice is null)
            ConnectionSummary = "No headset connected";
        UpdateConnectionSummary();

        if (!silent)
        {
            ActivityTitle = result.Devices.Count == 0 ? "No headset detected" : "Device scan complete";
            ActivityMessage = result.Message;
        }
    }

    private async Task MonitorDevicesAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        AppLog.Debug("DeviceMonitor", "Background device monitoring started (3-second interval).");

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!await _operationLock.WaitAsync(0, cancellationToken))
                    continue;

                try
                {
                    try
                    {
                        if (_session is not null && _session.HasExited)
                        {
                            var endedSession = _session!;
                            var output = endedSession.RecentOutput;
                            AppLog.Error("Streaming",
                                $"scrcpy exited unexpectedly. PID={endedSession.ProcessId}; exitCode={endedSession.ExitCode?.ToString() ?? "unknown"}; " +
                                $"recentOutput='{output}'.");
                            _videoSurface.Detach();
                            await endedSession.StopAsync();
                            _session = null;
                            _sessionSerial = null;
                            IsStreaming = false;
                            ActivityTitle = "Stream ended unexpectedly";
                            ActivityMessage = string.IsNullOrWhiteSpace(output)
                                ? "scrcpy closed unexpectedly. Refresh devices and try again."
                                : $"scrcpy closed unexpectedly: {output}";
                        }

                        await RefreshDevicesCoreAsync(silent: true, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        AppLog.Exception("DeviceMonitor", "Background monitoring cycle failed; polling will retry.", exception);
                    }
                }
                finally
                {
                    _operationLock.Release();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AppLog.Debug("DeviceMonitor", "Background device monitoring stopped.");
        }
        catch (Exception exception)
        {
            AppLog.Exception("DeviceMonitor", "Background device monitoring stopped unexpectedly.", exception);
        }
    }

    private async Task StopStreamingCoreAsync(string message)
    {
        _videoSurface.Detach();
        var session = _session;
        _session = null;
        _sessionSerial = null;

        if (session is not null)
        {
            AppLog.Debug("Streaming", $"Stopping scrcpy session PID={session.ProcessId}.");
            await session.StopAsync();
        }

        IsStreaming = false;
        UpdateConnectionSummary();
        ActivityTitle = "Stream stopped";
        ActivityMessage = message;
    }

    private void UpdateConnectionSummary()
    {
        if (SelectedDevice is null)
        {
            if (ConnectionSummary != "ADB unavailable")
                ConnectionSummary = "No headset connected";
            OnPropertyChanged(nameof(ConnectionDotBrush));
            return;
        }

        ConnectionSummary = SelectedDevice.State switch
        {
            "device" when SelectedDevice.Serial.Contains(':') => IsStreaming ? "Streaming over Wi-Fi" : "Wireless device connected",
            "device" => IsStreaming ? "Streaming over USB" : "USB device connected",
            "unauthorized" => "Permission required",
            "offline" => "Device offline",
            _ => $"Device state: {SelectedDevice.State}"
        };
        OnPropertyChanged(nameof(ConnectionDotBrush));
    }

    private void PersistSettings() => _ = SaveSettingsSafelyAsync();

    private async Task SaveSettingsSafelyAsync()
    {
        try
        {
            await _settingsService.SaveAsync(_settings, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            AppLog.Exception("Settings", "Settings could not be saved.", exception);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ActivityTitle = "Settings could not be saved";
                ActivityMessage = exception.Message;
            });
        }
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(CanRefreshDevices));
        OnPropertyChanged(nameof(CanChooseDevice));
        OnPropertyChanged(nameof(CanEditQuality));
        OnPropertyChanged(nameof(CanEditVideo));
        OnPropertyChanged(nameof(CanEditQualityAndManualQuality));
        OnPropertyChanged(nameof(CanStartStream));
        OnPropertyChanged(nameof(CanStopStream));
        OnPropertyChanged(nameof(ManualQualityOpacity));
        OnPropertyChanged(nameof(ConnectionDotBrush));
        RefreshDevicesCommand.NotifyCanExecuteChanged();
        PairWirelessCommand.NotifyCanExecuteChanged();
        ConnectWirelessCommand.NotifyCanExecuteChanged();
        StartStreamingCommand.NotifyCanExecuteChanged();
        StopStreamingCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCommands()
    {
        StartStreamingCommand.NotifyCanExecuteChanged();
        StopStreamingCommand.NotifyCanExecuteChanged();
    }

    public async ValueTask DisposeAsync()
    {
        AppLog.Info("Lifecycle", "View-model disposal started.");
        _lifetime.Cancel();
        if (_monitorTask is not null)
        {
            try { await _monitorTask; }
            catch (OperationCanceledException) { }
        }

        await _operationLock.WaitAsync();
        try
        {
            await StopStreamingCoreAsync("Application is closing.");
        }
        finally
        {
            _operationLock.Release();
            _operationLock.Dispose();
            _lifetime.Dispose();
            AppLog.Info("Lifecycle", "View-model disposal completed.");
        }
    }
}
