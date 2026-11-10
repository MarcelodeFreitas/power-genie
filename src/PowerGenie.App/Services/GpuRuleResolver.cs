using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class GpuRuleResolver
{
    // Returns the preset to apply to each GPU, keyed by GPU UUID. A GPU missing from the result is left
    // alone. Per GPU, when several running apps have rules, the highest limit wins, so a demanding app is
    // never held back by a quieter one. Without a matching rule, a GPU gets its saved default preset
    // if it has one. Rules pointing at a deleted preset are skipped.
    public static Dictionary<string, Guid> Resolve(
        IReadOnlyList<string> runningExeNames,
        IReadOnlyList<GpuRule> rules,
        IReadOnlyList<GpuPreset> presets,
        IReadOnlyDictionary<string, Guid> defaultPresetIds)
    {
        var running = new HashSet<string>(runningExeNames, StringComparer.OrdinalIgnoreCase);
        var presetById = presets.ToDictionary(p => p.Id);
        var winners = new Dictionary<string, GpuPreset>();

        foreach (var rule in rules)
        {
            if (!rule.IsEnabled || !running.Contains(rule.ExeName) || !presetById.TryGetValue(rule.PresetId, out var preset))
            {
                continue;
            }

            if (!winners.TryGetValue(preset.GpuUuid, out var best) || preset.Watts > best.Watts)
            {
                winners[preset.GpuUuid] = preset;
            }
        }

        var result = winners.ToDictionary(pair => pair.Key, pair => pair.Value.Id);
        foreach (var (uuid, presetId) in defaultPresetIds)
        {
            if (!result.ContainsKey(uuid) && presetById.TryGetValue(presetId, out var preset) && preset.GpuUuid == uuid)
            {
                result[uuid] = presetId;
            }
        }

        return result;
    }
}
