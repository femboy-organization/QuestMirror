using System.Diagnostics;
using SPECTRA.Core.Diagnostics;
using SPECTRA.Core.Processes;
using SPECTRA.Core.Tools;

namespace SPECTRA.Core.Streaming;

public sealed class ScrcpyStreamingService
{
    private readonly ToolLocator _tools;
    private readonly ProcessRunner _runner;

    public ScrcpyStreamingService(ToolLocator? tools = null, ProcessRunner? runner = null)
    {
        _tools = tools ?? new ToolLocator();
        _runner = runner ?? new ProcessRunner();
    }

    public async Task<ScrcpySession> StartAsync(
        string deviceSerial,
        StreamingOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(deviceSerial))
            throw new ArgumentException("Select a connected device before starting a stream.", nameof(deviceSerial));
        ArgumentNullException.ThrowIfNull(options);

        var executable = _tools.ScrcpyPath;
        if (Path.IsPathFullyQualified(executable) && !File.Exists(executable))
            throw new FileNotFoundException("scrcpy was not found in the application tools directory.", executable);

        var displayResult = await _runner.RunAsync(
            _tools.AdbPath,
            ["-s", deviceSerial, "shell", "wm", "size"],
            TimeSpan.FromSeconds(8),
            cancellationToken).ConfigureAwait(false);

        if (displayResult.TimedOut)
            throw new TimeoutException("ADB did not report the Quest display size within 8 seconds.");
        if (displayResult.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(displayResult.CombinedOutput)
                ? $"ADB exited with code {displayResult.ExitCode}."
                : displayResult.CombinedOutput.Trim();
            throw new InvalidOperationException($"Could not read the Quest display dimensions: {details}");
        }

        var displayOutput = $"{displayResult.StandardOutput}{Environment.NewLine}{displayResult.StandardError}";
        if (!ScrcpyVideoCrop.TryParseDisplaySize(displayOutput, out var displaySize))
        {
            AppLog.Error("Streaming", $"ADB returned no usable display dimensions. Output='{displayOutput.Trim()}'.");
            throw new InvalidOperationException(
                "Could not determine the Quest display dimensions. Check ADB and inspect the SPECTRA log.");
        }

        var crop = ScrcpyVideoCrop.CreateHalfDisplayCrop(displaySize, options.VideoEye);
        AppLog.Info("Streaming",
            $"Using {options.VideoEye.ToLowerInvariant()} eye crop={crop}; display={displaySize.Width}x{displaySize.Height}; " +
            $"rotationDegrees={options.RotationDegrees}.");

        var title = $"SPECTRA-{Guid.NewGuid():N}";
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.IsPathFullyQualified(executable)
                ? Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory
                : AppContext.BaseDirectory
        };

        startInfo.ArgumentList.Add($"--serial={deviceSerial}");
        startInfo.ArgumentList.Add($"--window-title={title}");
        startInfo.ArgumentList.Add("--video-source=display"); // Use the normal 2D Android display stream, not a camera/3D source.
        startInfo.ArgumentList.Add($"--crop={crop}");
        startInfo.ArgumentList.Add($"--angle={Math.Clamp(options.RotationDegrees, -180, 180)}");
        startInfo.ArgumentList.Add("--no-audio");
        startInfo.ArgumentList.Add("--no-control");
        startInfo.ArgumentList.Add("--window-borderless");
        startInfo.ArgumentList.Add($"--max-size={options.MaxSize}");
        startInfo.ArgumentList.Add($"--max-fps={options.FramesPerSecond}");
        startInfo.ArgumentList.Add($"--video-bit-rate={options.BitrateMbps}M");

        AppLog.Info("scrcpy",
            $"Launching '{executable}' for device '{deviceSerial}' with maxSize={options.MaxSize}, " +
            $"fps={options.FramesPerSecond}, bitrateMbps={options.BitrateMbps}, eye={options.VideoEye}, " +
            $"rotationDegrees={options.RotationDegrees}, crop='{crop}', workingDirectory='{startInfo.WorkingDirectory}'.");

        var stopwatch = Stopwatch.StartNew();
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Windows could not start scrcpy.");

            AppLog.Debug("Performance", $"scrcpy process creation completed in {stopwatch.ElapsedMilliseconds} ms; PID={process.Id}.");
            return new ScrcpySession(process, title);
        }
        catch (Exception exception)
        {
            AppLog.Exception("scrcpy", "Failed to start the scrcpy process.", exception);
            process.Dispose();
            throw;
        }
    }
}
