using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class RuleFilterTests
{
    private static readonly Guid DefaultPlan = Guid.Parse("a1841308-3541-4fab-bc81-f71556f20b4a");
    private static readonly Guid HighPerf = Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    private static readonly Guid Balanced = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");

    private static readonly Func<Guid, string> PlanNames = guid =>
        guid == HighPerf ? "High performance" :
        guid == Balanced ? "Balanced" :
        guid == DefaultPlan ? "Power saver" :
        string.Empty;

    private static List<AppRule> SampleRules() => new()
    {
        new() { ExeName = "bambu-studio.exe", DisplayName = "Bambu Studio", PlanGuid = HighPerf, IsEnabled = true },
        new() { ExeName = "notepad.exe", DisplayName = "Notepad", PlanGuid = Balanced, IsEnabled = false },
        new() { ExeName = "blender.exe", DisplayName = "Blender", PlanGuid = HighPerf, IsEnabled = true },
        new() { ExeName = "word.exe", DisplayName = "Word", PlanGuid = DefaultPlan, IsEnabled = false },
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_null_or_whitespace_query_returns_all_rules(string? query)
    {
        var rules = SampleRules();

        var result = RuleFilter.Apply(rules, query, RuleStatusFilter.All, PlanNames);

        Assert.Equal(rules.Count, result.Count);
        Assert.Equal(rules, result);
    }

    [Fact]
    public void Query_matches_display_name_case_insensitively()
    {
        var result = RuleFilter.Apply(SampleRules(), "bAMBU", RuleStatusFilter.All, PlanNames);

        var only = Assert.Single(result);
        Assert.Equal("Bambu Studio", only.DisplayName);
    }

    [Fact]
    public void Query_matches_exe_name_case_insensitively()
    {
        var result = RuleFilter.Apply(SampleRules(), "NOTEPAD.EXE", RuleStatusFilter.All, PlanNames);

        var only = Assert.Single(result);
        Assert.Equal("notepad.exe", only.ExeName);
    }

    [Fact]
    public void Query_matches_the_plan_name_through_the_lookup()
    {
        var result = RuleFilter.Apply(SampleRules(), "high perf", RuleStatusFilter.All, PlanNames);

        Assert.Equal(2, result.Count);
        Assert.All(result, rule => Assert.Equal(HighPerf, rule.PlanGuid));
    }

    [Fact]
    public void Query_is_trimmed_before_matching()
    {
        var result = RuleFilter.Apply(SampleRules(), "  Blender  ", RuleStatusFilter.All, PlanNames);

        var only = Assert.Single(result);
        Assert.Equal("Blender", only.DisplayName);
    }

    [Fact]
    public void Active_filter_keeps_only_enabled_rules()
    {
        var result = RuleFilter.Apply(SampleRules(), null, RuleStatusFilter.Active, PlanNames);

        Assert.Equal(new[] { "Bambu Studio", "Blender" }, result.Select(r => r.DisplayName).ToArray());
        Assert.All(result, rule => Assert.True(rule.IsEnabled));
    }

    [Fact]
    public void Inactive_filter_keeps_only_disabled_rules()
    {
        var result = RuleFilter.Apply(SampleRules(), null, RuleStatusFilter.Inactive, PlanNames);

        Assert.Equal(new[] { "Notepad", "Word" }, result.Select(r => r.DisplayName).ToArray());
        Assert.All(result, rule => Assert.False(rule.IsEnabled));
    }

    [Fact]
    public void All_filter_keeps_both_enabled_and_disabled_rules()
    {
        var result = RuleFilter.Apply(SampleRules(), null, RuleStatusFilter.All, PlanNames);

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void Query_and_status_filter_combine_with_AND()
    {
        // "e" matches Bambu Studio, Notepad, Blender and Word by display name or exe name;
        // Active then removes Notepad and Word.
        var result = RuleFilter.Apply(SampleRules(), "e", RuleStatusFilter.Active, PlanNames);

        Assert.Equal(new[] { "Bambu Studio", "Blender" }, result.Select(r => r.DisplayName).ToArray());
    }

    [Fact]
    public void Query_matching_only_disabled_rules_returns_nothing_under_Active_filter()
    {
        var result = RuleFilter.Apply(SampleRules(), "notepad", RuleStatusFilter.Active, PlanNames);

        Assert.Empty(result);
    }

    [Fact]
    public void Input_order_is_preserved()
    {
        var rules = new List<AppRule>
        {
            new() { ExeName = "c.exe", DisplayName = "Charlie", PlanGuid = HighPerf, IsEnabled = true },
            new() { ExeName = "a.exe", DisplayName = "Alpha", PlanGuid = HighPerf, IsEnabled = true },
            new() { ExeName = "b.exe", DisplayName = "Bravo", PlanGuid = HighPerf, IsEnabled = true },
        };

        var result = RuleFilter.Apply(rules, null, RuleStatusFilter.All, PlanNames);

        Assert.Equal(new[] { "Charlie", "Alpha", "Bravo" }, result.Select(r => r.DisplayName).ToArray());
    }

    [Fact]
    public void No_match_returns_an_empty_list()
    {
        var result = RuleFilter.Apply(SampleRules(), "zzz-no-such-app", RuleStatusFilter.All, PlanNames);

        Assert.Empty(result);
    }

    [Fact]
    public void Returns_a_new_list_and_leaves_the_input_untouched()
    {
        var rules = SampleRules();
        var originalCount = rules.Count;

        var result = RuleFilter.Apply(rules, null, RuleStatusFilter.All, PlanNames);
        result.Clear();

        Assert.NotSame(rules, result);
        Assert.Equal(originalCount, rules.Count);
    }
}
