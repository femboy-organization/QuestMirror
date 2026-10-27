using SPECTRA.Core.Devices;

namespace SPECTRA.Core.Tests;

public sealed class AdbOutputParserTests
{
    [Fact]
    public void ParseDevices_ReturnsReadyUnauthorizedAndOfflineDevices()
    {
        const string output =
            "List of devices attached\r\n" +
            "1WMHH000000001 device product:hollywood model:Quest_3 transport_id:4\r\n" +
            "192.168.1.23:5555 unauthorized transport_id:5\r\n" +
            "emulator-5554 offline\r\n";

        var devices = AdbOutputParser.ParseDevices(output);

        Assert.Equal(3, devices.Count);
        Assert.Equal("device", devices[0].State);
        Assert.Equal("1WMHH000000001", devices[0].Serial);
        Assert.Equal("unauthorized", devices[1].State);
        Assert.Equal("offline", devices[2].State);
    }

    [Theory]
    [InlineData("connected to 192.168.1.23:5555", true)]
    [InlineData("already connected to 192.168.1.23:5555", true)]
    [InlineData("failed to connect to 192.168.1.23:5555", false)]
    [InlineData("cannot connect", false)]
    public void ConnectOutput_IsAcceptedOnlyWhenAdbConfirmsSuccess(string output, bool expected)
    {
        Assert.Equal(expected, AdbOutputParser.IsSuccessfulConnectOutput(output));
    }

    [Fact]
    public void PairOutput_RequiresAdbSuccessMessage()
    {
        Assert.True(AdbOutputParser.IsSuccessfulPairOutput("Successfully paired to 192.168.1.23:37123"));
        Assert.False(AdbOutputParser.IsSuccessfulPairOutput("Failed: pairing code expired"));
    }

    [Theory]
    [InlineData("192.168.1.23:5555", true)]
    [InlineData("quest-headset.local:5555", true)]
    [InlineData("[fe80::1234]:5555", true)]
    [InlineData("192.168.1.23:0", false)]
    [InlineData("192.168.1.23:65536", false)]
    [InlineData("192.168.1.23:5555 & whoami", false)]
    [InlineData("", false)]
    public void WirelessEndpoint_IsValidatedBeforeExecution(string endpoint, bool expected)
    {
        Assert.Equal(expected, AdbService.IsValidEndpoint(endpoint));
    }
}
