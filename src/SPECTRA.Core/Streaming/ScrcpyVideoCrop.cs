using System.Text.RegularExpressions;

namespace SPECTRA.Core.Streaming;

/// <summary>
/// Calculates a square crop contained inside one circular eye viewport from a side-by-side display.
/// </summary>
public static class ScrcpyVideoCrop
{
    private const double VisibleCircleDiameterRatio = 0.82d;
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

    public static string CreateSquareEyeCrop(ScrcpyDisplaySize displaySize, string eye)
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

        // Keep the complete square inside the circular visible area, including its corners.
        var estimatedCircleDiameter = Math.Min(eyeWidth, eyeHeight) * VisibleCircleDiameterRatio;
        var squareSide = ((int)Math.Floor(estimatedCircleDiameter / Math.Sqrt(2d))) & ~1;
        if (squareSide < 2)
            throw new ArgumentOutOfRangeException(nameof(displaySize), "The eye viewport is too small for a square crop.");

        var localX = ((eyeWidth - squareSide) / 2) & ~1;
        var y = ((eyeHeight - squareSide) / 2) & ~1;
        var eyeOffset = normalizedEye == "Right" ? eyeWidth : 0;
        var x = eyeOffset + localX;

        return $"{squareSide}:{squareSide}:{x}:{y}";
    }
}

public readonly record struct ScrcpyDisplaySize(int Width, int Height);
