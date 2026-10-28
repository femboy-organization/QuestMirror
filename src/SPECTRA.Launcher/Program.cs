using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SPECTRA.Launcher;

internal static class Program
{
    private const uint MessageBoxError = 0x00000010;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int ShowMessageBox(IntPtr owner, string text, string caption, uint type);

    [STAThread]
    private static int Main(string[] args)
    {
        var installDirectory = AppContext.BaseDirectory;
        var applicationPath = Path.Combine(installDirectory, "bin", "SPECTRA.exe");
        WriteLauncherLog($"Launcher started. PID={Environment.ProcessId}; target={applicationPath}; argsCount={args.Length}.");

        if (!File.Exists(applicationPath))
        {
            var message = $"The SPECTRA application files were not found:\n{applicationPath}\n\n" +
                          "Re-extract the portable archive or reinstall SPECTRA.";
            WriteLauncherLog(message);
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

            using var application = Process.Start(startInfo);
            if (application is null)
            {
                const string message = "Windows did not start the SPECTRA application process.";
                WriteLauncherLog(message);
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
            WriteLauncherLog($"Could not start the application process: {exception}");
            ShowMessageBox(IntPtr.Zero,
                $"SPECTRA could not start.\n\n{exception.Message}\n\n" +
                "Check the diagnostics logs under %LOCALAPPDATA%\\SPECTRA\\Logs.",
                "SPECTRA could not start",
                MessageBoxError);
            return 1;
        }
    }

    private static void WriteLauncherLog(string message)
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = string.IsNullOrWhiteSpace(localAppData) ? Path.GetTempPath() : localAppData;
            var directory = Path.Combine(root, "SPECTRA", "Logs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"launcher-{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(path,
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [LAUNCHER] {message}{Environment.NewLine}",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch
        {
            // The launcher must still try to start the application if diagnostics cannot be written.
        }
    }
}
