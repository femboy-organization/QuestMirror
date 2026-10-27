namespace SPECTRA.Core.Tools;

public sealed class ToolLocator
{
    private readonly string _baseDirectory;

    public ToolLocator(string? baseDirectory = null)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory ?? AppContext.BaseDirectory);
    }

    public string AdbPath => Resolve("adb");
    public string ScrcpyPath => Resolve("scrcpy");

    public string Resolve(string toolName)
    {
        var executableName = OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;
        var candidates = new[]
        {
            Path.Combine(_baseDirectory, "tools", executableName),
            Path.Combine(_baseDirectory, executableName),
            Path.Combine(_baseDirectory, "tools", toolName, executableName)
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(directory.Trim(), executableName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        // Returning the command name allows ProcessStartInfo to resolve PATH and gives
        // ProcessRunner a normal, user-readable error when the tool is unavailable.
        return executableName;
    }
}
