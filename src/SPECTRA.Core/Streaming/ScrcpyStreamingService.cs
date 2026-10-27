using System.Diagnostics;
using SPECTRA.Core.Tools;

namespace SPECTRA.Core.Streaming;

public sealed class ScrcpyStreamingService
{
    private readonly ToolLocator _tools;

    public ScrcpyStreamingService(ToolLocator? tools = null) => _tools = tools ?? new ToolLocator();

    public Task<ScrcpySession> StartAsync(
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
        startInfo.ArgumentList.Add("--no-audio");
        startInfo.ArgumentList.Add("--no-control");
        startInfo.ArgumentList.Add("--window-borderless");
        startInfo.ArgumentList.Add($"--max-size={options.MaxSize}");
        startInfo.ArgumentList.Add($"--max-fps={options.FramesPerSecond}");
        startInfo.ArgumentList.Add($"--video-bit-rate={options.BitrateMbps}M");

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Windows could not start scrcpy.");
            return Task.FromResult(new ScrcpySession(process, title));
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }
}
