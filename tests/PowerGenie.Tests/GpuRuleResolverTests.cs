using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class GpuRuleResolverTests
{
    private const string Gpu0 = "GPU-0";
    private const string Gpu1 = "GPU-1";

    private static readonly GpuPreset Silent0 = new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Silent", Watts = 250, GpuUuid = Gpu0 };
    private static readonly GpuPreset Max0 = new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Max", Watts = 450, GpuUuid = Gpu0 };
    private static readonly GpuPreset Max1 = new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Max", Watts = 350, GpuUuid = Gpu1 };
    private static readonly List<GpuPreset> Presets = new() { Silent0, Max0, Max1 };
    private static readonly Dictionary<string, Guid> NoDefaults = new();

    private static GpuRule Rule(string exe, GpuPreset preset, bool enabled = true) =>
        new() { ExeName = exe, DisplayName = exe, PresetId = preset.Id, IsEnabled = enabled };

    private static Dictionary<string, Guid> Defaults(params (string Gpu, GpuPreset Preset)[] pairs) =>
        pairs.ToDictionary(pair => pair.Gpu, pair => pair.Preset.Id);

    [Fact]
    public void Default_applies_when_no_rule_matches()
    {
        var result = GpuRuleResolver.Resolve(new[] { "notepad.exe" }, new[] { Rule("game.exe", Max0) }, Presets, Defaults((Gpu0, Silent0)));

        Assert.Equal(new Dictionary<string, Guid> { [Gpu0] = Silent0.Id }, result);
    }

    [Fact]
    public void Highest_limit_wins_when_several_rules_match_the_same_gpu()
    {
        var rules = new[] { Rule("notepad.exe", Silent0), Rule("game.exe", Max0) };

        var result = GpuRuleResolver.Resolve(new[] { "notepad.exe", "game.exe" }, rules, Presets, NoDefaults);

        Assert.Equal(Max0.Id, result[Gpu0]);
    }

    [Fact]
    public void Each_gpu_resolves_its_own_rule()
    {
        var rules = new[] { Rule("game.exe", Max0), Rule("render.exe", Max1) };

        var result = GpuRuleResolver.Resolve(new[] { "game.exe", "render.exe" }, rules, Presets, NoDefaults);

        Assert.Equal(new Dictionary<string, Guid> { [Gpu0] = Max0.Id, [Gpu1] = Max1.Id }, result);
    }

    [Fact]
    public void A_rule_on_one_gpu_does_not_replace_the_default_of_the_other()
    {
        var rules = new[] { Rule("render.exe", Max1) };

        var result = GpuRuleResolver.Resolve(new[] { "render.exe" }, rules, Presets, Defaults((Gpu0, Silent0)));

        Assert.Equal(new Dictionary<string, Guid> { [Gpu0] = Silent0.Id, [Gpu1] = Max1.Id }, result);
    }

    [Fact]
    public void Gpu_without_a_rule_or_default_is_left_alone()
    {
        var result = GpuRuleResolver.Resolve(Array.Empty<string>(), Array.Empty<GpuRule>(), Presets, NoDefaults);

        Assert.Empty(result);
    }

    [Fact]
    public void Disabled_rules_are_ignored()
    {
        var rules = new[] { Rule("game.exe", Max0, enabled: false) };

        var result = GpuRuleResolver.Resolve(new[] { "game.exe" }, rules, Presets, Defaults((Gpu0, Silent0)));

        Assert.Equal(Silent0.Id, result[Gpu0]);
    }

    [Fact]
    public void Rules_for_a_deleted_preset_are_ignored()
    {
        var deleted = new GpuPreset { Id = Guid.NewGuid(), Name = "Gone", Watts = 300, GpuUuid = Gpu0 };
        var rules = new[] { Rule("game.exe", deleted) };

        var result = GpuRuleResolver.Resolve(new[] { "game.exe" }, rules, Presets, NoDefaults);

        Assert.Empty(result);
    }

    [Fact]
    public void Default_pointing_at_another_gpus_preset_is_not_used()
    {
        var result = GpuRuleResolver.Resolve(Array.Empty<string>(), Array.Empty<GpuRule>(), Presets, Defaults((Gpu1, Silent0)));

        Assert.Empty(result);
    }

    [Fact]
    public void Process_names_match_regardless_of_case()
    {
        var result = GpuRuleResolver.Resolve(new[] { "GAME.EXE" }, new[] { Rule("game.exe", Max0) }, Presets, NoDefaults);

        Assert.Equal(Max0.Id, result[Gpu0]);
    }
}
