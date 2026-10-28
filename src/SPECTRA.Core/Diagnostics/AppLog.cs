using System.Diagnostics;
using System.Text;

namespace SPECTRA.Core.Diagnostics;

/// <summary>
/// Lightweight, always-on diagnostics logging for the application and its helper processes.
/// Logs are written outside the installation directory so portable installs remain writable.
/// </summary>
public static class AppLog
{
    private static readonly object Sync = new();
    private static readonly Encoding LogEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static string? _logDirectory;
    private static string? _logFilePath;
    private static StreamWriter? _writer;
    private static bool _initialized;

    public static string LogFilePath
    {
        get
        {
            EnsureInitialized();
            lock (Sync)
                return _logFilePath ?? "(log file unavailable)";
        }
    }

    public static void Initialize()
    {
        lock (Sync)
        {
            if (_initialized)
                return;

            _initialized = true;
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var primaryDirectory = string.IsNullOrWhiteSpace(localAppData)
                ? Path.Combine(Path.GetTempPath(), "SPECTRA", "Logs")
                : Path.Combine(localAppData, "SPECTRA", "Logs");

            try
            {
                _logDirectory = primaryDirectory;
                Directory.CreateDirectory(_logDirectory);
                OpenWriterUnsafe(DateTime.Now);
            }
            catch (Exception primaryException)
            {
                try
                {
                    _logDirectory = Path.Combine(Path.GetTempPath(), "SPECTRA", "Logs");
                    Directory.CreateDirectory(_logDirectory);
                    OpenWriterUnsafe(DateTime.Now);
                    WriteUnsafe("WARN", "Diagnostics",
                        $"Could not open the primary log directory; using the temporary directory. {primaryException.Message}");
                }
                catch
                {
                    _writer = null;
                    _logFilePath = null;
                }
            }

            CleanupOldLogsUnsafe();
            WriteUnsafe("INFO", "Diagnostics", $"Logging initialized. File: {(_logFilePath ?? "(unavailable)")}");
        }
    }

    public static void Debug(string category, string message) => Write("DEBUG", category, message);
    public static void Info(string category, string message) => Write("INFO", category, message);
    public static void Warn(string category, string message) => Write("WARN", category, message);
    public static void Error(string category, string message) => Write("ERROR", category, message);

    public static void Exception(string category, string message, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Write("ERROR", category, $"{message}{Environment.NewLine}{exception}");
    }

    public static void Flush()
    {
        lock (Sync)
        {
            try
            {
                _writer?.Flush();
            }
            catch
            {
                // Logging must never be allowed to crash the application.
            }
        }
    }

    private static void Write(string level, string category, string message)
    {
        EnsureInitialized();

        lock (Sync)
        {
            try
            {
                var now = DateTime.Now;
                var expectedPath = _logDirectory is null
                    ? null
                    : Path.Combine(_logDirectory, $"spectra-{now:yyyy-MM-dd}.log");

                if (_writer is null || !string.Equals(expectedPath, _logFilePath, StringComparison.OrdinalIgnoreCase))
                    OpenWriterUnsafe(now);

                WriteUnsafe(level, category, message);
            }
            catch (Exception loggingException)
            {
                try
                {
                    Console.Error.WriteLine($"{DateTimeOffset.Now:O} [{level}] [{category}] {message}");
                    Console.Error.WriteLine($"Could not write the diagnostics log: {loggingException}");
                }
                catch
                {
                    // No reliable output stream is available; preserve the application operation.
                }
            }
        }
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
            Initialize();
    }

    private static void OpenWriterUnsafe(DateTime now)
    {
        if (string.IsNullOrWhiteSpace(_logDirectory))
            throw new InvalidOperationException("The diagnostics log directory has not been configured.");

        _writer?.Dispose();
        _logFilePath = Path.Combine(_logDirectory, $"spectra-{now:yyyy-MM-dd}.log");
        var stream = new FileStream(
            _logFilePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        _writer = new StreamWriter(stream, LogEncoding) { AutoFlush = true };
    }

    private static void WriteUnsafe(string level, string category, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] [{category}] {message}";
        if (_writer is not null)
            _writer.WriteLine(line);
        else
            Console.Error.WriteLine(line);
    }

    private static void CleanupOldLogsUnsafe()
    {
        if (string.IsNullOrWhiteSpace(_logDirectory) || !Directory.Exists(_logDirectory))
            return;

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-14);
            foreach (var pattern in new[] { "spectra-*.log", "launcher-*.log" })
            {
                foreach (var path in Directory.EnumerateFiles(_logDirectory, pattern))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(path) < cutoff)
                            File.Delete(path);
                    }
                    catch
                    {
                        // A locked or protected old log should not interrupt application startup.
                    }
                }
            }
        }
        catch
        {
            // Cleanup is best-effort.
        }
    }
}

/// <summary>Forwards Avalonia and System.Diagnostics trace messages to the persistent log.</summary>
public sealed class AppLogTraceListener : TraceListener
{
    public override void Write(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            AppLog.Debug("Trace", message.TrimEnd());
    }

    public override void WriteLine(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            AppLog.Debug("Trace", message.TrimEnd());
    }

    public override void Flush() => AppLog.Flush();
}
