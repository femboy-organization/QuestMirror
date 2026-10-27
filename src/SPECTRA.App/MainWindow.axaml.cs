using Avalonia.Controls;
using Avalonia.Interactivity;
using SPECTRA.Core.Devices;

namespace SPECTRA.App;

public partial class MainWindow : Window
{
    private readonly AdbDeviceDiscovery _deviceDiscovery = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnRefreshDevicesClick(object? sender, RoutedEventArgs e)
    {
        var activityStatus = this.FindControl<TextBlock>("ActivityStatusText");
        var activityDetails = this.FindControl<TextBlock>("ActivityDetailsText");
        var deviceStatus = this.FindControl<TextBlock>("DeviceStatusText");
        var deviceDetails = this.FindControl<TextBlock>("DeviceDetailsText");

        if (activityStatus is not null) activityStatus.Text = "Checking ADB…";
        if (activityDetails is not null) activityDetails.Text = "Looking for connected Android devices.";

        try
        {
            var result = await _deviceDiscovery.ListDevicesAsync();
            if (deviceStatus is not null)
                deviceStatus.Text = result.Devices.Count == 0
                    ? "No device detected"
                    : $"{result.Devices.Count} device(s) detected";
            if (deviceDetails is not null)
                deviceDetails.Text = result.Devices.Count == 0
                    ? result.Message
                    : string.Join(", ", result.Devices.Select(device => $"{device.Serial} ({device.State})"));
            if (activityStatus is not null)
                activityStatus.Text = result.IsAvailable ? "Scan complete" : "ADB unavailable";
            if (activityDetails is not null) activityDetails.Text = result.Message;
        }
        catch (Exception ex)
        {
            if (activityStatus is not null) activityStatus.Text = "Device scan failed";
            if (activityDetails is not null) activityDetails.Text = ex.Message;
        }
    }
}