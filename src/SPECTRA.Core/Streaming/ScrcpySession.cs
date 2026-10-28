using System.Collections.Concurrent;
using System.Diagnostics;
using SPECTRA.Core.Diagnostics;

namespace SPECTRA.Core.Streaming;

public sealed class ScrcpySession : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> _recentOutput = new();
    private bool _disposed;

    internal ScrcpySession(Process process, string windowTitle)
    {
        Process = process;
        WindowTitle = windowTitle;
        Process.OutputDataReceived += (_, eventArgs) => RecordOutput("stdout", eventArgs.Data);
        Process.ErrorDataReceived += (_, eventArgs) => RecordOutput("stderr", eventArgs.Data);
        Process.BeginOutputReadLine();
        Process.BeginErrorReadLine();

        AppLog.Info("scrcpy", $"Started scrcpy process. PID={Process.Id}; windowTitle='{WindowTitle}'.");
    }

    internal Process Process { get; }
    public string WindowTitle { get; }
    public int ProcessId => Process.Id;

    public int? ExitCode
    {
        get
        {
            try { return Process.HasExited ? Process.ExitCode : null; }
            catch (InvalidOperationException) { return null; }
        }
    }

    public bool HasExited
    {
        get
        {
            try { return Process.HasExited; }
            catch (InvalidOperationException) { return true; }
        }
    }

    public string RecentOutput => string.Join(Environment.NewLine, _recentOutput.ToArray());

    private void RecordOutput(string stream, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        _recentOutput.Enqueue(line);
        while (_recentOutput.Count > 30)
            _recentOutput.TryDequeue(out _);

        const int maxCharacters = 2000;
        var preview = line.Length <= maxCharacters ? line : line[..maxCharacters] + "…(truncated)";
        AppLog.Debug("scrcpy", $"PID={SafeProcessId}; {stream}: {preview}");
    }

    private string SafeProcessId
    {
        get
        {
            try { return Process.Id.ToString(); }
            catch (InvalidOperationException) { return "unknown"; }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        var processId = SafeProcessId;
        AppLog.Debug("scrcpy", $"Stopping scrcpy process PID={processId}.");

        try
        {
            if (!Process.HasExited)
                Process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await Process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException) { }

        var exitCode = ExitCode;
        _disposed = true;
        Process.Dispose();
        AppLog.Info("scrcpy", $"scrcpy process PID={processId} stopped. ExitCode={exitCode?.ToString() ?? "unknown"}.");
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
