using System.Text.RegularExpressions;

namespace SPECTRA.Core.Streaming;

/// <summary>Calculates the scrcpy crop rectangle for one half of a side-by-side display.</summary>
public static class ScrcpyVideoCrop
{
    private static readonly Regex DisplaySizePattern = new(
        @"(?<width>\d{2,5})\s*x\s*(?<height>\d{2,5})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool TryParseDisplaySize(string? output, out ScrcpyDisplaySize displaySize)
    {
        displaySize = default;
        if (string.IsNullOrWhiteSpace(output))
            return false;

        var matches = DisplaySizePattern.Matches(output);
        for (var index = matches.Count - 1; index >= 0; index--)
        {
            if (!int.TryParse(matches[index].Groups["width"].Value, out var width) ||
                !int.TryParse(matches[index].Groups["height"].Value, out var height))
                continue;

            if (width is < 2 or > 16384 || height is < 2 or > 16384)
                continue;

            displaySize = new ScrcpyDisplaySize(width, height);
            return true;
        }

        return false;
    }

    public static string CreateSixteenByNineEyeCrop(ScrcpyDisplaySize displaySize, string eye)
    {
        if (displaySize.Width < 4 || displaySize.Height < 2)
            throw new ArgumentOutOfRangeException(nameof(displaySize), "The display dimensions are too small to crop.");

        var eyeWidth = (displaySize.Width / 2) & ~1;
        var eyeHeight = displaySize.Height & ~1;
        if (eyeWidth < 2 || eyeHeight < 2)
            throw new ArgumentOutOfRangeException(nameof(displaySize), "The eye viewport is too small to crop.");

        var normalizedEye = string.Equals(eye, "Right", StringComparison.OrdinalIgnoreCase)
            ? "Right"
            : string.Equals(eye, "Left", StringComparison.OrdinalIgnoreCase)
                ? "Left"
                : throw new ArgumentException("Eye must be either Left or Right.", nameof(eye));

        // Fit a 16:9 rectangle inside the circular lens area. The safety factor
        // avoids exposing the circular edge, including after display rotation.
        const double visibleCircleDiameterRatio = 0.82d;
        var visibleDiameter = Math.Min(eyeWidth, eyeHeight) * visibleCircleDiameterRatio;
        var cropHeight = ((int)Math.Floor(visibleDiameter * 9d / Math.Sqrt(337d))) & ~1;
        var cropWidth = ((int)Math.Floor(cropHeight * 16d / 9d)) & ~1;
        if (cropWidth < 2 || cropHeight < 2)
            throw new ArgumentOutOfRangeException(nameof(displaySize), "The eye viewport is too small for a 16:9 crop.");

        var localX = ((eyeWidth - cropWidth) / 2) & ~1;
        var y = ((eyeHeight - cropHeight) / 2) & ~1;
        var eyeOffset = normalizedEye == "Right" ? eyeWidth : 0;
        var x = eyeOffset + localX;

        return $"{cropWidth}:{cropHeight}:{x}:{y}";
    }
}

public readonly record struct ScrcpyDisplaySize(int Width, int Height);
