using System.Text.RegularExpressions;
using SPECTRA.Core.Processes;
using SPECTRA.Core.Tools;

namespace SPECTRA.Core.Devices;

public sealed class AdbService
{
    private static readonly Regex EndpointPattern = new(
        @"^(?:[A-Za-z0-9.-]+|\[[0-9A-Fa-f:]+\]):[0-9]{1,5}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ProcessRunner _runner;
    private readonly ToolLocator _tools;

    public AdbService(ProcessRunner? runner = null, ToolLocator? tools = null)
    {
        _runner = runner ?? new ProcessRunner();
        _tools = tools ?? new ToolLocator();
    }

    public async Task<AdbDiscoveryResult> ListDevicesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(_tools.AdbPath, ["devices", "-l"],
            TimeSpan.FromSeconds(8), cancellationToken).ConfigureAwait(false);
        if (result.TimedOut)
            return new(false, Array.Empty<AdbDevice>(), "ADB did not respond within 8 seconds.");
        if (result.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(result.CombinedOutput)
                ? $"ADB exited with code {result.ExitCode}."
                : result.CombinedOutput;
            return new(false, Array.Empty<AdbDevice>(), message);
        }

        var devices = AdbOutputParser.ParseDevices(result.StandardOutput);
        return new(true, devices, devices.Count == 0
            ? "ADB is available, but no device is connected. Connect USB or use Wireless debugging."
            : $"ADB found {devices.Count} device(s).");
    }

    public async Task<AdbCommandResult> PairAsync(
        string pairingEndpoint,
        string pairingCode,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidEndpoint(pairingEndpoint))
            return new(false, "Enter a valid pairing address in host:port format.");
        if (string.IsNullOrWhiteSpace(pairingCode) ||
            pairingCode.Length is < 4 or > 16 ||
            pairingCode.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
            return new(false, "Enter the pairing code shown in Quest Wireless debugging.");

        var result = await _runner.RunAsync(_tools.AdbPath,
            ["pair", pairingEndpoint.Trim(), pairingCode.Trim()],
            TimeSpan.FromSeconds(25), cancellationToken).ConfigureAwait(false);

        if (result.TimedOut)
            return new(false, "ADB pairing timed out. Check that the pairing dialog is still active.");
        if (result.ExitCode == 0 && AdbOutputParser.IsSuccessfulPairOutput(result.CombinedOutput))
            return new(true, result.CombinedOutput);

        return new(false, string.IsNullOrWhiteSpace(result.CombinedOutput)
            ? $"ADB pairing failed (exit code {result.ExitCode})."
            : result.CombinedOutput);
    }

    public async Task<AdbCommandResult> ConnectAsync(
        string deviceEndpoint,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidEndpoint(deviceEndpoint))
            return new(false, "Enter the connection address shown under Wireless debugging, in host:port format.");

        var result = await _runner.RunAsync(_tools.AdbPath,
            ["connect", deviceEndpoint.Trim()],
            TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);

        if (result.TimedOut)
            return new(false, "Wireless connection timed out.");
        if (result.ExitCode == 0 && AdbOutputParser.IsSuccessfulConnectOutput(result.StandardOutput))
            return new(true, result.StandardOutput.Trim());

        return new(false, string.IsNullOrWhiteSpace(result.CombinedOutput)
            ? $"ADB could not connect (exit code {result.ExitCode})."
            : result.CombinedOutput);
    }

    public static bool IsValidEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return false;

        var value = endpoint.Trim();
        if (value.Length > 128 || value.Any(char.IsWhiteSpace) || !EndpointPattern.IsMatch(value))
            return false;

        var colon = value.LastIndexOf(':');
        if (!int.TryParse(value[(colon + 1)..], out var port) || port is < 1 or > 65535)
            return false;

        var host = value[..colon].Trim('[', ']');
        return host.Length > 0;
    }
}

public sealed record AdbCommandResult(bool IsSuccess, string Message);
