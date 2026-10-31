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
    public void CreateSixteenByNineEyeCrop_CropsTheLeftEyeInsideTheCircularViewport()
    {
        var crop = ScrcpyVideoCrop.CreateSixteenByNineEyeCrop(new ScrcpyDisplaySize(3664, 1920), "Left");

        Assert.Equal("1308:736:262:592", crop);
    }

    [Fact]
    public void CreateSixteenByNineEyeCrop_CropsTheRightEyeInsideTheCircularViewport()
    {
        var crop = ScrcpyVideoCrop.CreateSixteenByNineEyeCrop(new ScrcpyDisplaySize(3664, 1920), "Right");

        Assert.Equal("1308:736:2094:592", crop);
    }

    [Fact]
    public void CreateSixteenByNineEyeCrop_RejectsUnknownEye()
    {
        Assert.Throws<ArgumentException>(() =>
            ScrcpyVideoCrop.CreateSixteenByNineEyeCrop(new ScrcpyDisplaySize(3664, 1920), "Both"));
    }
}
