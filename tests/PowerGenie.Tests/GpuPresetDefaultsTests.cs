using System.Text.Json;
using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class GpuPresetDefaultsTests
{
    [Fact]
    public void Seed_adds_silent_and_balanced_and_makes_balanced_the_default()
    {
        var config = new AppConfig();

        var seeded = GpuPresetDefaults.Seed(config, new GpuCardInfo("RTX 3090", 290, 420, 100, 450));

        Assert.True(seeded);
        Assert.Equal(new[] { "Silent", "Balanced" }, config.GpuPresets.Select(p => p.Name));
        Assert.Equal(new[] { 250, 330 }, config.GpuPresets.Select(p => p.Watts));
        Assert.Equal(config.GpuPresets[1].Id, config.GpuDefaultPresetId);
        Assert.True(config.GpuPresetsSeeded);
    }

    [Fact]
    public void Seed_does_nothing_once_it_has_run()
    {
        var config = new AppConfig();
        var card = new GpuCardInfo("RTX 3090", 290, 420, 100, 450);
        GpuPresetDefaults.Seed(config, card);
        config.GpuPresets.Clear();

        Assert.False(GpuPresetDefaults.Seed(config, card));
        Assert.Empty(config.GpuPresets);
    }

    [Fact]
    public void Seed_clamps_watts_to_a_card_with_a_lower_maximum()
    {
        var config = new AppConfig();

        GpuPresetDefaults.Seed(config, new GpuCardInfo("Small card", 150, 200, 100, 200));

        Assert.Equal(new[] { 200, 200 }, config.GpuPresets.Select(p => p.Watts));
    }

    [Fact]
    public void Config_saved_before_gpu_settings_existed_still_loads()
    {
        const string oldJson = """{ "DefaultPlanGuid": "a1841308-3541-4fab-bc81-f71556f20b4a", "Rules": [], "StartWithWindows": true }""";

        var config = JsonSerializer.Deserialize<AppConfig>(oldJson)!;

        Assert.Empty(config.GpuPresets);
        Assert.Empty(config.GpuRules);
        Assert.False(config.GpuPresetsSeeded);
    }
}
