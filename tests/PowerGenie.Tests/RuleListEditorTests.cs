using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class RuleListEditorTests
{
    private static AppRule Rule(string exe, string name = "x") =>
        new() { ExeName = exe, DisplayName = name, PlanGuid = Guid.NewGuid() };

    [Fact]
    public void FindConflicts_matches_exe_names_case_insensitively_and_ignores_display_names()
    {
        var existing = new List<AppRule> { Rule("Code.exe", "VS Code"), Rule("other.exe") };

        var conflicts = RuleListEditor.FindConflicts(existing, new[] { Rule("code.EXE", "Different name") });

        Assert.Single(conflicts);
        Assert.Same(existing[0], conflicts[0]);
    }

    [Fact]
    public void FindConflicts_allows_the_same_display_name_for_a_different_exe()
    {
        var existing = new List<AppRule> { Rule("a.exe", "Game") };

        Assert.Empty(RuleListEditor.FindConflicts(existing, new[] { Rule("b.exe", "Game") }));
    }

    [Fact]
    public void FindConflicts_ignores_the_rule_being_edited()
    {
        var existing = new List<AppRule> { Rule("a.exe") };

        Assert.Empty(RuleListEditor.FindConflicts(existing, new[] { Rule("a.exe") }, ignore: existing[0]));
    }

    [Fact]
    public void AddOrReplace_replaces_in_place_and_appends_new_exes()
    {
        var rules = new List<AppRule> { Rule("a.exe"), Rule("b.exe") };
        var replacement = Rule("A.exe");
        var added = Rule("c.exe");

        RuleListEditor.AddOrReplace(rules, new[] { replacement, added });

        Assert.Equal(3, rules.Count);
        Assert.Same(replacement, rules[0]);
        Assert.Same(added, rules[2]);
    }

    [Fact]
    public void ReplaceEdited_removes_the_other_rule_for_the_new_exe()
    {
        var rules = new List<AppRule> { Rule("a.exe"), Rule("b.exe"), Rule("c.exe") };
        var edited = Rule("c.exe");

        RuleListEditor.ReplaceEdited(rules, 0, edited);

        Assert.Equal(new[] { "c.exe", "b.exe" }, rules.Select(r => r.ExeName));
        Assert.Same(edited, rules[0]);
    }
}
