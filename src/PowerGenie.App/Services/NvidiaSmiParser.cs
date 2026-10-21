using System.Globalization;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class NvidiaSmiParser
{
    // Expects the output of: nvidia-smi --query-gpu=name,power.limit,power.default_limit,power.min_limit,power.max_limit --format=csv,noheader,nounits
    // Only the first GPU is read. Returns null when there is no line or a value is missing ("[N/A]").
    public static GpuCardInfo? Parse(string output)
    {
        var line = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (line is null)
        {
            return null;
        }

        var fields = line.Split(',', StringSplitOptions.TrimEntries);
        if (fields.Length != 5
            || !TryWatts(fields[1], out var current)
            || !TryWatts(fields[2], out var defaults)
            || !TryWatts(fields[3], out var min)
            || !TryWatts(fields[4], out var max))
        {
            return null;
        }

        return new GpuCardInfo(fields[0], current, defaults, min, max);
    }

    private static bool TryWatts(string text, out int watts)
    {
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            watts = (int)Math.Round(value);
            return true;
        }

        watts = 0;
        return false;
    }
}
