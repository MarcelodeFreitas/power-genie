using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class GpuPresetDefaults
{
    // Adds the shipped Silent and Balanced presets once. Returns false if they were already added,
    // so a user who deleted them does not get them back on the next start.
    public static bool Seed(AppConfig config, GpuCardInfo card)
    {
        if (config.GpuPresetsSeeded)
        {
            return false;
        }

        var silent = new GpuPreset { Id = Guid.NewGuid(), Name = "Silent", Watts = GpuPresetValidator.Clamp(250, card) };
        var balanced = new GpuPreset { Id = Guid.NewGuid(), Name = "Balanced", Watts = GpuPresetValidator.Clamp(330, card) };

        config.GpuPresets.Add(silent);
        config.GpuPresets.Add(balanced);
        config.GpuDefaultPresetId = balanced.Id;
        config.GpuPresetsSeeded = true;
        return true;
    }
}
