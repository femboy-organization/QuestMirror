using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SPECTRA.Launcher;

internal static class Program
{
    private const uint MessageBoxError = 0x00000010;
    private static readonly object LogSync = new();
    private static string? _activeLogPath;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int ShowMessageBox(IntPtr owner, string text, string caption, uint type);

    [STAThread]
    private static int Main(string[] args)
    {
        WriteLauncherLog($"ENTRYPOINT reached. PID={Environment.ProcessId}; OS={Environment.OSVersion}; " +
                         $"architecture={RuntimeInformation.ProcessArchitecture}; argsCount={args.Length}; " +
                         $"baseDirectory='{AppContext.BaseDirectory}'.");

        if (args.Any(argument => string.Equals(argument, "--diagnostic-self-test", StringComparison.OrdinalIgnoreCase)))
        {
            WriteLauncherLog("Diagnostic self-test passed; launcher entry point and writable log destination are available.");
            return 0;
        }

        var installDirectory = AppContext.BaseDirectory;
        var applicationPath = Path.Combine(installDirectory, "bin", "SPECTRA.exe");
        WriteLauncherLog($"Resolving application executable: '{applicationPath}'.");

        if (!File.Exists(applicationPath))
        {
            var message = $"The SPECTRA application files were not found:\n{applicationPath}\n\n" +
                          "Re-extract the portable archive or reinstall SPECTRA.";
            WriteLauncherLog("FATAL: " + message);
            ShowMessageBox(IntPtr.Zero, message, "SPECTRA could not start", MessageBoxError);
            return 2;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = applicationPath,
                WorkingDirectory = Path.GetDirectoryName(applicationPath)!,
                UseShellExecute = false
            };
            foreach (var argument in args)
                startInfo.ArgumentList.Add(argument);

            WriteLauncherLog($"Starting application process; workingDirectory='{startInfo.WorkingDirectory}'.");
            using var application = Process.Start(startInfo);
            if (application is null)
            {
                const string message = "Windows did not start the SPECTRA application process.";
                WriteLauncherLog("FATAL: " + message);
                ShowMessageBox(IntPtr.Zero, message, "SPECTRA could not start", MessageBoxError);
                return 3;
            }

            WriteLauncherLog($"Application process started. PID={application.Id}.");
            application.WaitForExit();
            WriteLauncherLog($"Application process exited. PID={application.Id}; exitCode={application.ExitCode}.");
            return application.ExitCode;
        }
        catch (Exception exception)
        {
            WriteLauncherLog($"FATAL: Could not start the application process: {exception}");
            ShowMessageBox(IntPtr.Zero,
                $"SPECTRA could not start.\n\n{exception.Message}\n\n" +
                "Diagnostic logs are saved in the application's logs folder, or under %LOCALAPPDATA%\\SPECTRA\\Logs / %TEMP%\\SPECTRA\\Logs if necessary.",
                "SPECTRA could not start",
                MessageBoxError);
            return 1;
        }
    }

    private static void WriteLauncherLog(string message)
    {
        lock (LogSync)
        {
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [LAUNCHER] {message}{Environment.NewLine}";
            foreach (var path in GetLogPaths())
            {
                try
                {
                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(directory))
                        Directory.CreateDirectory(directory);

                    using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    writer.Write(line);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                    _activeLogPath = path;
                    return;
                }
                catch { }
            }
            try { Debug.WriteLine(line); } catch { }
        }
    }

    private static IEnumerable<string> GetLogPaths()
    {
        if (!string.IsNullOrWhiteSpace(_activeLogPath))
            yield return _activeLogPath;

        yield return Path.Combine(AppContext.BaseDirectory, "logs", $"launcher-{DateTime.Now:yyyy-MM-dd}.log");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
            yield return Path.Combine(localAppData, "SPECTRA", "Logs", $"launcher-{DateTime.Now:yyyy-MM-dd}.log");

        yield return Path.Combine(Path.GetTempPath(), "SPECTRA", "Logs", $"launcher-{DateTime.Now:yyyy-MM-dd}.log");
    }
}
