using System.Globalization;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class NvidiaSmiParser
{
    // Expects the output of: nvidia-smi --query-gpu=index,uuid,pci.bus_id,name,power.limit,power.default_limit,power.min_limit,power.max_limit --format=csv,noheader,nounits
    // One card per line. Lines with a missing value ("[N/A]", e.g. an integrated GPU) are skipped.
    public static List<GpuCardInfo> Parse(string output)
    {
        var cards = new List<GpuCardInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = line.Split(',', StringSplitOptions.TrimEntries);
            if (fields.Length != 8
                || !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                || !TryWatts(fields[4], out var current)
                || !TryWatts(fields[5], out var defaults)
                || !TryWatts(fields[6], out var min)
                || !TryWatts(fields[7], out var max))
            {
                continue;
            }

            cards.Add(new GpuCardInfo(index, fields[1], fields[2], fields[3], current, defaults, min, max));
        }

        return cards;
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
