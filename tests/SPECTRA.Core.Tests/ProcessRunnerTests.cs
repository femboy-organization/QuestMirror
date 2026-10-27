using System.Diagnostics;
using SPECTRA.Core.Processes;

namespace SPECTRA.Core.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_CapturesStandardOutputAndExitCode()
    {
        var runner = new ProcessRunner();
        var executable = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows()
            ? new[] { "/d", "/c", "echo spectra-process-runner-test" }
            : new[] { "-c", "printf spectra-process-runner-test" };

        var result = await runner.RunAsync(executable, arguments, TimeSpan.FromSeconds(5));

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("spectra-process-runner-test", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_KillsTimedOutProcessTree()
    {
        var runner = new ProcessRunner();
        var executable = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows()
            ? new[] { "/d", "/c", "ping -n 30 127.0.0.1 > NUL" }
            : new[] { "-c", "sleep 30" };
        var stopwatch = Stopwatch.StartNew();

        var result = await runner.RunAsync(executable, arguments, TimeSpan.FromMilliseconds(350));

        stopwatch.Stop();
        Assert.True(result.TimedOut);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Timed-out process tree took too long to stop: {stopwatch.Elapsed}.");
    }
}
