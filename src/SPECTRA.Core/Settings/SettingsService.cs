using System.Text.Json;
using System.Text.Json.Serialization;
using SPECTRA.Core.Diagnostics;

namespace SPECTRA.Core.Settings;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    private readonly string _settingsPath;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public SettingsService(string? rootDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SPECTRA");
        _settingsPath = Path.Combine(root, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                AppLog.Debug("Settings", $"Settings file does not exist yet; defaults will be used. Path='{_settingsPath}'.");
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = (JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings()).Normalize();
            AppLog.Debug("Settings", $"Settings loaded from '{_settingsPath}'.");
            return settings;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            AppLog.Exception("Settings", $"Could not load settings from '{_settingsPath}'; defaults will be used.", exception);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)
                ?? throw new InvalidOperationException("The settings path has no parent directory.");
            Directory.CreateDirectory(directory);
            var temporaryPath = _settingsPath + ".tmp";
            var normalized = new AppSettings
            {
                AutomaticQuality = settings.AutomaticQuality,
                Resolution = settings.Resolution,
                FramesPerSecond = settings.FramesPerSecond,
                BitrateMbps = settings.BitrateMbps,
                PairingEndpoint = settings.PairingEndpoint,
                DeviceEndpoint = settings.DeviceEndpoint
            }.Normalize();

            await using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _settingsPath, overwrite: true);
            AppLog.Debug("Settings", $"Settings saved to '{_settingsPath}'.");
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
