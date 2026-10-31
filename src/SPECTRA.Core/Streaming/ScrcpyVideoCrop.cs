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

    public static string CreateHalfDisplayCrop(ScrcpyDisplaySize displaySize, string eye)
    {
        if (displaySize.Width < 4 || displaySize.Height < 2)
            throw new ArgumentOutOfRangeException(nameof(displaySize), "The display dimensions are too small to crop.");

        var cropWidth = (displaySize.Width / 2) & ~1;
        if (cropWidth < 2)
            throw new ArgumentOutOfRangeException(nameof(displaySize), "The display width is too small to split into two eyes.");

        var normalizedEye = string.Equals(eye, "Right", StringComparison.OrdinalIgnoreCase)
            ? "Right"
            : string.Equals(eye, "Left", StringComparison.OrdinalIgnoreCase)
                ? "Left"
                : throw new ArgumentException("Eye must be either Left or Right.", nameof(eye));

        var x = normalizedEye == "Right" ? cropWidth : 0;
        var height = displaySize.Height & ~1;
        return $"{cropWidth}:{height}:{x}:0";
    }
}

public readonly record struct ScrcpyDisplaySize(int Width, int Height);
