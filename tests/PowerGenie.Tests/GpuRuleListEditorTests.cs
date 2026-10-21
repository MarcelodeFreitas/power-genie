using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class GpuRuleListEditorTests
{
    private static readonly Guid Max = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static GpuRule Rule(string exe, string name = "x") =>
        new() { ExeName = exe, DisplayName = name, PresetId = Max };

    [Fact]
    public void FindConflicts_matches_exe_names_case_insensitively_and_ignores_display_names()
    {
        var existing = new List<GpuRule> { Rule("Game.exe", "Game"), Rule("other.exe") };

        var conflicts = GpuRuleListEditor.FindConflicts(existing, new[] { Rule("game.EXE", "Different name") });

        Assert.Single(conflicts);
        Assert.Same(existing[0], conflicts[0]);
    }

    [Fact]
    public void FindConflicts_ignores_the_rule_being_edited()
    {
        var existing = new List<GpuRule> { Rule("a.exe") };

        Assert.Empty(GpuRuleListEditor.FindConflicts(existing, new[] { Rule("a.exe") }, ignore: existing[0]));
    }

    [Fact]
    public void AddOrReplace_replaces_in_place_and_appends_new_exes()
    {
        var rules = new List<GpuRule> { Rule("a.exe", "A"), Rule("b.exe", "B") };

        GpuRuleListEditor.AddOrReplace(rules, new[] { Rule("a.exe", "A2"), Rule("c.exe", "C") });

        Assert.Equal(new[] { "a.exe", "b.exe", "c.exe" }, rules.Select(r => r.ExeName));
        Assert.Equal("A2", rules[0].DisplayName);
    }

    [Fact]
    public void ReplaceEdited_puts_the_rule_at_its_index_and_removes_others_for_the_same_exe()
    {
        var rules = new List<GpuRule> { Rule("a.exe"), Rule("c.exe"), Rule("b.exe") };
        var edited = Rule("c.exe", "C renamed");

        GpuRuleListEditor.ReplaceEdited(rules, 1, edited);

        Assert.Equal(new[] { "a.exe", "c.exe", "b.exe" }, rules.Select(r => r.ExeName));
        Assert.Same(edited, rules[1]);
    }
}
