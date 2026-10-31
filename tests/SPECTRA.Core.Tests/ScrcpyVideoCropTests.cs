using SPECTRA.Core.Streaming;

namespace SPECTRA.Core.Tests;

public sealed class ScrcpyVideoCropTests
{
    [Fact]
    public void TryParseDisplaySize_PrefersTheActiveOverrideOverPhysicalSize()
    {
        const string output = "Physical size: 3664x1920\\r\\nOverride size: 3200x1600";

        Assert.True(ScrcpyVideoCrop.TryParseDisplaySize(output, out var size));
        Assert.Equal(new ScrcpyDisplaySize(3200, 1600), size);
    }

    [Fact]
    public void CreateHalfDisplayCrop_CropsLeftEye()
    {
        var crop = ScrcpyVideoCrop.CreateHalfDisplayCrop(new ScrcpyDisplaySize(3664, 1920), "Left");

        Assert.Equal("1832:1920:0:0", crop);
    }

    [Fact]
    public void CreateHalfDisplayCrop_CropsRightEye()
    {
        var crop = ScrcpyVideoCrop.CreateHalfDisplayCrop(new ScrcpyDisplaySize(3664, 1920), "Right");

        Assert.Equal("1832:1920:1832:0", crop);
    }

    [Fact]
    public void CreateHalfDisplayCrop_RejectsUnknownEye()
    {
        Assert.Throws<ArgumentException>(() =>
            ScrcpyVideoCrop.CreateHalfDisplayCrop(new ScrcpyDisplaySize(3664, 1920), "Both"));
    }
}
