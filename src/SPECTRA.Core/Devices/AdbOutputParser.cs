namespace SPECTRA.Core.Devices;

public static class AdbOutputParser
{
    public static IReadOnlyList<AdbDevice> ParseDevices(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var devices = new List<AdbDevice>();

        foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 ||
                line.StartsWith("List of devices attached", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("* daemon", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || parts[0] == "*")
                continue;

            var details = parts.Length > 2 ? string.Join(' ', parts.Skip(2)) : null;
            devices.Add(new AdbDevice(parts[0], parts[1], details));
        }

        return devices;
    }

    public static bool IsSuccessfulConnectOutput(string output)
    {
        var line = output.Trim();
        return line.StartsWith("connected to ", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("already connected to ", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSuccessfulPairOutput(string output) =>
        output.Contains("successfully paired", StringComparison.OrdinalIgnoreCase);
}
