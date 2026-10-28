using System.Diagnostics;
using System.ComponentModel;
using SPECTRA.Core.Diagnostics;

namespace SPECTRA.Core.Processes;

public sealed class ProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var argumentList = arguments.ToArray();
        var command = argumentList.FirstOrDefault() ?? "(no arguments)";
        var processName = Path.GetFileName(executable);
        var stopwatch = Stopwatch.StartNew();

        // Argument values are deliberately omitted: the wireless-pair command includes a secret pairing code.
        AppLog.Debug("ProcessRunner",
            $"Starting process '{processName}' command='{command}', timeoutSeconds={timeout.TotalSeconds:0.##}.");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in argumentList)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                AppLog.Error("ProcessRunner", $"Process '{processName}' could not be started.");
                return new(-1, string.Empty, $"Could not start '{processName}'.");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) { }
                catch (Win32Exception) { }

                try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (InvalidOperationException) { }

                if (cancellationToken.IsCancellationRequested)
                {
                    AppLog.Debug("ProcessRunner",
                        $"Process '{processName}' command='{command}' was cancelled after {stopwatch.ElapsedMilliseconds} ms.");
                    throw;
                }

                var timedOutOutput = await outputTask.ConfigureAwait(false);
                var timedOutError = await errorTask.ConfigureAwait(false);
                AppLog.Warn("ProcessRunner",
                    $"Process '{processName}' command='{command}' timed out after {stopwatch.ElapsedMilliseconds} ms. " +
                    $"stdout='{Preview(timedOutOutput)}'; stderr='{Preview(timedOutError)}'.");
                return new(-1, timedOutOutput, timedOutError, TimedOut: true);
            }

            var standardOutput = await outputTask.ConfigureAwait(false);
            var standardError = await errorTask.ConfigureAwait(false);

            if (process.ExitCode == 0)
            {
                AppLog.Debug("Performance",
                    $"Process '{processName}' command='{command}' completed in {stopwatch.ElapsedMilliseconds} ms.");
            }
            else
            {
                AppLog.Warn("ProcessRunner",
                    $"Process '{processName}' command='{command}' exited with code {process.ExitCode} " +
                    $"after {stopwatch.ElapsedMilliseconds} ms. stdout='{Preview(standardOutput)}'; " +
                    $"stderr='{Preview(standardError)}'.");
            }

            return new(process.ExitCode, standardOutput, standardError);
        }
        catch (Win32Exception exception)
        {
            AppLog.Exception("ProcessRunner", $"Windows could not start process '{processName}'.", exception);
            return new(-1, string.Empty, exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AppLog.Exception("ProcessRunner", $"Unexpected error while running '{processName}' command='{command}'.", exception);
            throw;
        }
    }

    private static string Preview(string value)
    {
        var normalized = value.Trim();
        const int maxCharacters = 2000;
        return normalized.Length <= maxCharacters
            ? normalized
            : normalized[..maxCharacters] + "…(truncated)";
    }
}
