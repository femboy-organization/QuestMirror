namespace SPECTRA.Core.Devices;

public sealed record AdbDiscoveryResult(
    bool IsAvailable,
    IReadOnlyList<AdbDevice> Devices,
    string Message);