using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class GpuRuleFilterTests
{
    private static readonly Guid Silent = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Max = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static string PresetName(Guid id) => id == Max ? "Max" : "Silent";

    private static List<GpuRule> SampleRules() => new()
    {
        new() { ExeName = "game.exe", DisplayName = "Game", PresetId = Max },
        new() { ExeName = "notepad.exe", DisplayName = "Notepad", PresetId = Silent, IsEnabled = false },
    };

    [Fact]
    public void Empty_query_and_all_filter_keep_every_rule()
    {
        var result = RuleFilter.Apply(SampleRules(), null, RuleStatusFilter.All, PresetName);

        Assert.Equal(2, result.Count);
    }

    [Theory]
    [InlineData("GAME")]
    [InlineData("notepad.exe")]
    [InlineData("  max  ")]
    public void Query_matches_display_name_exe_name_or_preset_name_ignoring_case(string query)
    {
        var result = RuleFilter.Apply(SampleRules(), query, RuleStatusFilter.All, PresetName);

        Assert.NotEmpty(result);
    }

    [Fact]
    public void Query_matching_nothing_returns_no_rules()
    {
        Assert.Empty(RuleFilter.Apply(SampleRules(), "zzz-no-such-app", RuleStatusFilter.All, PresetName));
    }

    [Fact]
    public void Active_filter_hides_disabled_rules()
    {
        var result = RuleFilter.Apply(SampleRules(), null, RuleStatusFilter.Active, PresetName);

        Assert.Equal(new[] { "game.exe" }, result.Select(r => r.ExeName));
    }

    [Fact]
    public void Inactive_filter_shows_only_disabled_rules()
    {
        var result = RuleFilter.Apply(SampleRules(), null, RuleStatusFilter.Inactive, PresetName);

        Assert.Equal(new[] { "notepad.exe" }, result.Select(r => r.ExeName));
    }
}
