using System.Text.RegularExpressions;

namespace LayerZero.Testcontainers.Cleanup;

internal static partial class CleanupDurationParser
{
    [GeneratedRegex("(\\d+)([smhd])", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SegmentRegex();

    public static bool TryParse(string text, out TimeSpan duration)
    {
        duration = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (TimeSpan.TryParse(text, out duration))
        {
            return duration >= TimeSpan.Zero;
        }

        var matches = SegmentRegex().Matches(text);
        if (matches.Count == 0)
        {
            return false;
        }

        var consumed = 0;
        long totalTicks = 0;

        foreach (Match match in matches)
        {
            if (!match.Success || match.Index != consumed)
            {
                return false;
            }

            consumed += match.Length;

            if (!long.TryParse(match.Groups[1].Value, out var value))
            {
                return false;
            }

            var segment = char.ToLowerInvariant(match.Groups[2].Value[0]) switch
            {
                's' => TimeSpan.FromSeconds(value),
                'm' => TimeSpan.FromMinutes(value),
                'h' => TimeSpan.FromHours(value),
                'd' => TimeSpan.FromDays(value),
                _ => TimeSpan.MinValue,
            };

            if (segment == TimeSpan.MinValue)
            {
                return false;
            }

            totalTicks += segment.Ticks;
        }

        if (consumed != text.Length || totalTicks < 0)
        {
            return false;
        }

        duration = TimeSpan.FromTicks(totalTicks);
        return true;
    }
}
