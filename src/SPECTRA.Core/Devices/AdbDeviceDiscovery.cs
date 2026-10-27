using System.Diagnostics;

namespace SPECTRA.Core.Devices;

/// <summary>
/// Minimal, non-blocking ADB discovery. ADB resolution is intentionally explicit:
/// a bundled tools directory can be injected later, with PATH as a development fallback.
/// </summary>
public sealed class AdbDeviceDiscovery
{
    public async Task<AdbDiscoveryResult> ListDevicesAsync(
        string adbExecutable = "adb",
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = adbExecutable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("devices");
        startInfo.ArgumentList.Add("-l");

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                return new(false, Array.Empty<AdbDevice>(), "Could not start ADB.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* process already exited */ }
                return new(false, Array.Empty<AdbDevice>(), "ADB timed out after 8 seconds.");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
                return new(false, Array.Empty<AdbDevice>(),
                    string.IsNullOrWhiteSpace(stderr) ? $"ADB exited with code {process.ExitCode}." : stderr.Trim());

            var devices = stdout
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Select(ParseDevice)
                .Where(device => device is not null)
                .Cast<AdbDevice>()
                .ToArray();

            return new(true, devices, devices.Length == 0
                ? "ADB is available, but no device is connected."
                : $"Found {devices.Length} ADB device(s).");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new(false, Array.Empty<AdbDevice>(),
                $"ADB executable '{adbExecutable}' was not found. Install platform-tools or configure the bundled path.");
        }
    }

    private static AdbDevice? ParseDevice(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts[0] == "*")
            return null;

        var details = parts.Length > 2 ? string.Join(' ', parts.Skip(2)) : null;
        return new AdbDevice(parts[0], parts[1], details);
    }
}