namespace SPECTRA.Core.Settings;

public sealed class AppSettings
{
    public bool AutomaticQuality { get; set; } = true;
    public int Resolution { get; set; } = 1920;
    public int FramesPerSecond { get; set; } = 60;
    public int BitrateMbps { get; set; } = 8;
    public string VideoEye { get; set; } = "Left";
    public int RotationDegrees { get; set; }
    public string PairingEndpoint { get; set; } = string.Empty;
    public string DeviceEndpoint { get; set; } = string.Empty;

    public AppSettings Normalize()
    {
        Resolution = AllowedResolutions.Contains(Resolution) ? Resolution : 1920;
        FramesPerSecond = AllowedFrameRates.Contains(FramesPerSecond) ? FramesPerSecond : 60;
        BitrateMbps = AllowedBitrates.Contains(BitrateMbps) ? BitrateMbps : 8;
        VideoEye = string.Equals(VideoEye, "Right", StringComparison.OrdinalIgnoreCase) ? "Right" : "Left";
        RotationDegrees = Math.Clamp(RotationDegrees, -180, 180);
        PairingEndpoint = NormalizeText(PairingEndpoint, 128);
        DeviceEndpoint = NormalizeText(DeviceEndpoint, 128);
        return this;
    }

    public static IReadOnlyList<int> AllowedResolutions { get; } = [1280, 1600, 1920, 2560];
    public static IReadOnlyList<int> AllowedFrameRates { get; } = [30, 60, 72, 90];
    public static IReadOnlyList<int> AllowedBitrates { get; } = [4, 8, 12, 16];

    private static string NormalizeText(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }
}
