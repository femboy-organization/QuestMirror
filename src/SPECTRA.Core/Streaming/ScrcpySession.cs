using System.Collections.Concurrent;
using System.Diagnostics;

namespace SPECTRA.Core.Streaming;

public sealed class ScrcpySession : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> _recentOutput = new();
    private bool _disposed;

    internal ScrcpySession(Process process, string windowTitle)
    {
        Process = process;
        WindowTitle = windowTitle;
        Process.OutputDataReceived += (_, e) => RecordOutput(e.Data);
        Process.ErrorDataReceived += (_, e) => RecordOutput(e.Data);
        Process.BeginOutputReadLine();
        Process.BeginErrorReadLine();
    }

    internal Process Process { get; }
    public string WindowTitle { get; }
    public int ProcessId => Process.Id;
    public bool HasExited
    {
        get
        {
            try { return Process.HasExited; }
            catch (InvalidOperationException) { return true; }
        }
    }

    public string RecentOutput => string.Join(Environment.NewLine, _recentOutput.ToArray());

    private void RecordOutput(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        _recentOutput.Enqueue(line);
        while (_recentOutput.Count > 30)
            _recentOutput.TryDequeue(out _);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

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

        _disposed = true;
        Process.Dispose();
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
