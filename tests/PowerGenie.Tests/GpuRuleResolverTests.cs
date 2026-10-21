using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class GpuRuleResolverTests
{
    private static readonly Guid Silent = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Balanced = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Max = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly Dictionary<Guid, int> Watts = new()
    {
        [Silent] = 250,
        [Balanced] = 330,
        [Max] = 450,
    };

    private static GpuRule Rule(string exe, Guid preset, bool enabled = true) =>
        new() { ExeName = exe, DisplayName = exe, PresetId = preset, IsEnabled = enabled };

    [Fact]
    public void Returns_default_when_no_rule_matches()
    {
        var result = GpuRuleResolver.Resolve(new[] { "notepad.exe" }, new[] { Rule("game.exe", Max) }, Watts, Balanced);

        Assert.Equal(Balanced, result);
    }

    [Fact]
    public void Highest_limit_wins_when_several_rules_match()
    {
        var rules = new[] { Rule("notepad.exe", Silent), Rule("game.exe", Max) };

        var result = GpuRuleResolver.Resolve(new[] { "notepad.exe", "game.exe" }, rules, Watts, Balanced);

        Assert.Equal(Max, result);
    }

    [Fact]
    public void Disabled_rules_are_ignored()
    {
        var rules = new[] { Rule("game.exe", Max, enabled: false) };

        var result = GpuRuleResolver.Resolve(new[] { "game.exe" }, rules, Watts, Balanced);

        Assert.Equal(Balanced, result);
    }

    [Fact]
    public void Rules_for_a_deleted_preset_are_ignored()
    {
        var deleted = Guid.NewGuid();
        var rules = new[] { Rule("game.exe", deleted), Rule("notepad.exe", Silent) };

        var result = GpuRuleResolver.Resolve(new[] { "game.exe", "notepad.exe" }, rules, Watts, Balanced);

        Assert.Equal(Silent, result);
    }

    [Fact]
    public void Process_names_match_regardless_of_case()
    {
        var result = GpuRuleResolver.Resolve(new[] { "GAME.EXE" }, new[] { Rule("game.exe", Max) }, Watts, Balanced);

        Assert.Equal(Max, result);
    }

    [Fact]
    public void Returns_empty_when_default_preset_no_longer_exists()
    {
        var result = GpuRuleResolver.Resolve(Array.Empty<string>(), Array.Empty<GpuRule>(), Watts, Guid.NewGuid());

        Assert.Equal(Guid.Empty, result);
    }
}
