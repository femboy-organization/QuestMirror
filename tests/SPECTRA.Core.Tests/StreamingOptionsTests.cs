using SPECTRA.Core.Settings;
using SPECTRA.Core.Streaming;

namespace SPECTRA.Core.Tests;

public sealed class StreamingOptionsTests
{
    [Fact]
    public void AutomaticQuality_UsesTheDocumentedDefaultPreset()
    {
        var options = StreamingOptions.FromSettings(new AppSettings { AutomaticQuality = true });

        Assert.True(options.IsAutomatic);
        Assert.Equal(1920, options.MaxSize);
        Assert.Equal(60, options.FramesPerSecond);
        Assert.Equal(8, options.BitrateMbps);
    }

    [Fact]
    public void ManualQuality_UsesValidatedUserValues()
    {
        var options = StreamingOptions.FromSettings(new AppSettings
        {
            AutomaticQuality = false,
            Resolution = 1600,
            FramesPerSecond = 72,
            BitrateMbps = 12
        });

        Assert.False(options.IsAutomatic);
        Assert.Equal(1600, options.MaxSize);
        Assert.Equal(72, options.FramesPerSecond);
        Assert.Equal(12, options.BitrateMbps);
    }
}
