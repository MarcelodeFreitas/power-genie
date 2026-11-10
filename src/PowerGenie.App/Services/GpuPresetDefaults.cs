using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class GpuPresetDefaults
{
    // Brings the presets in line with the detected cards. Presets of GPUs that are no longer there are
    // removed, with the rules and defaults that point at them. Each card gets exactly one original
    // preset at its stock watts, recreated or repaired if it was missing or changed. Nothing is applied
    // here. Returns true if the config changed. With no cards detected (nvidia-smi failed), nothing is
    // removed, so a failed detection cannot wipe the user's presets.
    public static bool Reconcile(AppConfig config, IReadOnlyList<GpuCardInfo> cards)
    {
        if (cards.Count == 0)
        {
            return false;
        }

        var changed = false;

        // Presets saved before per-GPU presets have no GPU. With exactly one card they must belong to it.
        if (cards.Count == 1)
        {
            foreach (var preset in config.GpuPresets.Where(p => p.GpuUuid.Length == 0))
            {
                preset.GpuUuid = cards[0].Uuid;
                changed = true;
            }
        }

        var detected = cards.Select(c => c.Uuid).ToHashSet();
        var removedIds = config.GpuPresets.Where(p => !detected.Contains(p.GpuUuid)).Select(p => p.Id).ToHashSet();
        if (removedIds.Count > 0)
        {
            config.GpuPresets.RemoveAll(p => removedIds.Contains(p.Id));
            config.GpuRules.RemoveAll(r => removedIds.Contains(r.PresetId));
            changed = true;
        }

        foreach (var (uuid, presetId) in config.GpuDefaultPresetIds.ToList())
        {
            if (!config.GpuPresets.Any(p => p.Id == presetId && p.GpuUuid == uuid))
            {
                config.GpuDefaultPresetIds.Remove(uuid);
                changed = true;
            }
        }

        foreach (var card in cards)
        {
            changed |= EnsureOriginal(config, card);
        }

        return changed;
    }

    // Name shown in the lists, e.g. "RTX 3090 - 350 W (original)". The model is cut short if needed to fit the name limit.
    internal static string OriginalName(GpuCardInfo card)
    {
        var suffix = $" - {card.DefaultWatts} W (original)";
        var model = card.ShortName[..Math.Min(card.ShortName.Length, GpuPresetValidator.MaxNameLength - suffix.Length)];
        return model + suffix;
    }

    private static bool EnsureOriginal(AppConfig config, GpuCardInfo card)
    {
        var watts = GpuPresetValidator.Clamp(card.DefaultWatts, card);
        var name = OriginalName(card);
        var originals = config.GpuPresets.Where(p => p.IsOriginal && p.GpuUuid == card.Uuid).ToList();
        if (originals.Count == 0)
        {
            config.GpuPresets.Add(new GpuPreset
            {
                Id = Guid.NewGuid(),
                Name = name,
                Watts = watts,
                GpuUuid = card.Uuid,
                IsOriginal = true,
            });
            return true;
        }

        var original = originals[0];
        var changed = original.Name != name || original.Watts != watts;
        original.Name = name;
        original.Watts = watts;

        // Only one original per card; any extra copies are dropped.
        foreach (var extra in originals.Skip(1))
        {
            config.GpuPresets.Remove(extra);
            changed = true;
        }

        return changed;
    }
}
