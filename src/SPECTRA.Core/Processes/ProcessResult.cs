namespace SPECTRA.Core.Processes;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut = false)
{
    public string CombinedOutput =>
        string.Join(Environment.NewLine, new[] { StandardOutput.Trim(), StandardError.Trim() }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
}
