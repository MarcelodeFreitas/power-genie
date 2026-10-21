using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class GpuRuleResolver
{
    // Returns the preset to apply, or Guid.Empty to leave the GPU alone. When several running apps
    // have rules, the highest limit wins, so a demanding app is never held back by a quieter one.
    // Rules pointing at a deleted preset are skipped, like power plan rules for deleted plans.
    public static Guid Resolve(
        IReadOnlyList<string> runningExeNames,
        IReadOnlyList<GpuRule> rules,
        IReadOnlyDictionary<Guid, int> presetWatts,
        Guid defaultPresetId)
    {
        var running = new HashSet<string>(runningExeNames, StringComparer.OrdinalIgnoreCase);
        var bestPresetId = Guid.Empty;
        var bestWatts = int.MinValue;

        foreach (var rule in rules)
        {
            if (!rule.IsEnabled || !running.Contains(rule.ExeName))
            {
                continue;
            }

            if (presetWatts.TryGetValue(rule.PresetId, out var watts) && watts > bestWatts)
            {
                bestPresetId = rule.PresetId;
                bestWatts = watts;
            }
        }

        if (bestPresetId != Guid.Empty)
        {
            return bestPresetId;
        }

        return presetWatts.ContainsKey(defaultPresetId) ? defaultPresetId : Guid.Empty;
    }
}
