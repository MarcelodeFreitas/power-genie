using System.Text.Json;
using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class GpuPresetDefaultsTests
{
    private static readonly GpuCardInfo Card0 = new(0, "GPU-0", "00000000:04:00.0", "NVIDIA GeForce RTX 3090", 290, 350, 100, 450);
    private static readonly GpuCardInfo Card1 = new(1, "GPU-1", "00000000:2B:00.0", "NVIDIA GeForce RTX 3090", 290, 350, 100, 450);

    private static GpuPreset Original(GpuCardInfo card) => new()
    {
        Id = Guid.NewGuid(),
        Name = GpuPresetDefaults.OriginalName(card),
        Watts = card.DefaultWatts,
        GpuUuid = card.Uuid,
        IsOriginal = true,
    };

    [Fact]
    public void Reconcile_adds_one_original_per_card_at_its_stock_watts()
    {
        var config = new AppConfig();

        var changed = GpuPresetDefaults.Reconcile(config, new[] { Card0, Card1 });

        Assert.True(changed);
        Assert.Equal(2, config.GpuPresets.Count);
        Assert.All(config.GpuPresets, p => Assert.True(p.IsOriginal));
        Assert.Equal(new[] { "GPU-0", "GPU-1" }, config.GpuPresets.Select(p => p.GpuUuid));
        Assert.All(config.GpuPresets, p => Assert.Equal(350, p.Watts));
        Assert.Equal("RTX 3090 - 350 W (original)", config.GpuPresets[0].Name);
    }

    [Fact]
    public void Reconcile_changes_nothing_on_the_next_run()
    {
        var config = new AppConfig();
        GpuPresetDefaults.Reconcile(config, new[] { Card0 });

        Assert.False(GpuPresetDefaults.Reconcile(config, new[] { Card0 }));
        Assert.Single(config.GpuPresets);
    }

    [Fact]
    public void Reconcile_leaves_everything_alone_when_no_gpu_was_detected()
    {
        var preset = new GpuPreset { Id = Guid.NewGuid(), Name = "Mine", Watts = 300, GpuUuid = "GPU-0" };
        var config = new AppConfig();
        config.GpuPresets.Add(preset);

        Assert.False(GpuPresetDefaults.Reconcile(config, Array.Empty<GpuCardInfo>()));
        Assert.Equal(new[] { preset.Id }, config.GpuPresets.Select(p => p.Id));
    }

    [Fact]
    public void Reconcile_removes_presets_rules_and_defaults_of_gpus_that_are_gone()
    {
        var gone = new GpuPreset { Id = Guid.NewGuid(), Name = "Gone", Watts = 300, GpuUuid = "GPU-gone" };
        var kept = new GpuPreset { Id = Guid.NewGuid(), Name = "Kept", Watts = 300, GpuUuid = "GPU-0" };
        var config = new AppConfig();
        config.GpuPresets.AddRange(new[] { gone, kept });
        config.GpuRules.Add(new GpuRule { ExeName = "a.exe", DisplayName = "A", PresetId = gone.Id });
        config.GpuRules.Add(new GpuRule { ExeName = "b.exe", DisplayName = "B", PresetId = kept.Id });
        config.GpuDefaultPresetIds["GPU-gone"] = gone.Id;
        config.GpuDefaultPresetIds["GPU-0"] = kept.Id;

        GpuPresetDefaults.Reconcile(config, new[] { Card0 });

        Assert.DoesNotContain(config.GpuPresets, p => p.Id == gone.Id);
        Assert.Equal(new[] { "b.exe" }, config.GpuRules.Select(r => r.ExeName));
        Assert.False(config.GpuDefaultPresetIds.ContainsKey("GPU-gone"));
        Assert.Equal(kept.Id, config.GpuDefaultPresetIds["GPU-0"]);
    }

    [Fact]
    public void Reconcile_recreates_a_missing_original()
    {
        var config = new AppConfig();
        GpuPresetDefaults.Reconcile(config, new[] { Card0 });
        config.GpuPresets.Clear();

        Assert.True(GpuPresetDefaults.Reconcile(config, new[] { Card0 }));

        var original = Assert.Single(config.GpuPresets);
        Assert.True(original.IsOriginal);
        Assert.Equal(350, original.Watts);
    }

    [Fact]
    public void Reconcile_repairs_an_original_whose_watts_were_changed()
    {
        var config = new AppConfig();
        GpuPresetDefaults.Reconcile(config, new[] { Card0 });
        config.GpuPresets[0].Watts = 999;

        Assert.True(GpuPresetDefaults.Reconcile(config, new[] { Card0 }));
        Assert.Equal(350, config.GpuPresets[0].Watts);
    }

    [Fact]
    public void Reconcile_keeps_only_one_original_per_card()
    {
        var config = new AppConfig();
        config.GpuPresets.Add(Original(Card0));
        config.GpuPresets.Add(Original(Card0));

        GpuPresetDefaults.Reconcile(config, new[] { Card0 });

        Assert.Single(config.GpuPresets);
    }

    [Fact]
    public void Preset_saved_without_a_gpu_belongs_to_the_only_card()
    {
        var old = new GpuPreset { Id = Guid.NewGuid(), Name = "Silent", Watts = 250 };
        var config = new AppConfig();
        config.GpuPresets.Add(old);

        GpuPresetDefaults.Reconcile(config, new[] { Card0 });

        Assert.Equal("GPU-0", config.GpuPresets.Single(p => p.Id == old.Id).GpuUuid);
    }

    [Fact]
    public void Preset_saved_without_a_gpu_is_removed_when_there_are_several_cards()
    {
        var old = new GpuPreset { Id = Guid.NewGuid(), Name = "Silent", Watts = 250 };
        var config = new AppConfig();
        config.GpuPresets.Add(old);

        GpuPresetDefaults.Reconcile(config, new[] { Card0, Card1 });

        Assert.DoesNotContain(config.GpuPresets, p => p.Id == old.Id);
    }

    [Fact]
    public void Original_name_fits_the_name_limit_for_a_long_model()
    {
        var wideCard = new GpuCardInfo(0, "GPU-x", "00000000:01:00.0", "NVIDIA GeForce RTX 4090 Laptop GPU Super Extra", 150, 1000, 100, 1000);

        var name = GpuPresetDefaults.OriginalName(wideCard);

        Assert.True(name.Length <= GpuPresetValidator.MaxNameLength);
        Assert.EndsWith(" - 1000 W (original)", name);
    }

    [Fact]
    public void Config_saved_before_gpu_presets_per_card_still_loads()
    {
        const string oldJson = """
            { "DefaultPlanGuid": "a1841308-3541-4fab-bc81-f71556f20b4a", "Rules": [], "StartWithWindows": true,
              "GpuPresets": [ { "Id": "11111111-1111-1111-1111-111111111111", "Name": "Silent", "Watts": 250 } ],
              "GpuDefaultPresetId": "11111111-1111-1111-1111-111111111111" }
            """;

        var config = JsonSerializer.Deserialize<AppConfig>(oldJson)!;

        var preset = Assert.Single(config.GpuPresets);
        Assert.Equal(string.Empty, preset.GpuUuid);
        Assert.False(preset.IsOriginal);
        Assert.Empty(config.GpuDefaultPresetIds);
    }
}
