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
    public void CreateSquareEyeCrop_CropsTheLeftEyeToAnInscribedSquare()
    {
        var crop = ScrcpyVideoCrop.CreateSquareEyeCrop(new ScrcpyDisplaySize(3664, 1920), "Left");

        Assert.Equal("1062:1062:384:428", crop);
    }

    [Fact]
    public void CreateSquareEyeCrop_CropsTheRightEyeToAnInscribedSquare()
    {
        var crop = ScrcpyVideoCrop.CreateSquareEyeCrop(new ScrcpyDisplaySize(3664, 1920), "Right");

        Assert.Equal("1062:1062:2216:428", crop);
    }

    [Fact]
    public void CreateSquareEyeCrop_RejectsUnknownEye()
    {
        Assert.Throws<ArgumentException>(() =>
            ScrcpyVideoCrop.CreateSquareEyeCrop(new ScrcpyDisplaySize(3664, 1920), "Both"));
    }
}
