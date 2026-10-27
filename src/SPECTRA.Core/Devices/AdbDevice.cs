namespace SPECTRA.Core.Devices;

public sealed record AdbDevice(string Serial, string State, string? Details = null);