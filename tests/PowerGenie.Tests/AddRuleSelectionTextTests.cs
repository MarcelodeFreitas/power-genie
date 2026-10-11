using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class AddRuleSelectionTextTests
{
    [Fact]
    public void Summary_shows_the_multi_select_hint_when_nothing_is_selected()
    {
        Assert.Equal(AddRuleSelectionText.Hint, AddRuleSelectionText.Summary(0, 0));
        Assert.Contains("several apps", AddRuleSelectionText.Hint);
    }

    [Theory]
    [InlineData(1, "1 app selected")]
    [InlineData(2, "2 apps selected")]
    [InlineData(12, "12 apps selected")]
    public void Summary_pluralizes_the_selected_count(int count, string expected)
    {
        Assert.Equal(expected, AddRuleSelectionText.Summary(count, 0));
    }

    [Fact]
    public void Summary_mentions_selected_apps_hidden_by_the_search_filter()
    {
        Assert.Equal("3 apps selected (1 hidden by filter)", AddRuleSelectionText.Summary(3, 1));
    }

    [Fact]
    public void Summary_says_when_duplicate_exe_names_collapse_into_fewer_rules()
    {
        Assert.Equal("5 apps selected, 3 unique exe names", AddRuleSelectionText.Summary(5, 0, 3));
        Assert.Equal("3 apps selected (1 hidden by filter), 1 unique exe name", AddRuleSelectionText.Summary(3, 1, 1));
    }

    [Fact]
    public void Summary_does_not_mention_unique_names_when_every_pick_becomes_a_rule()
    {
        Assert.Equal("3 apps selected", AddRuleSelectionText.Summary(3, 0, 3));
    }

    [Theory]
    [InlineData(0, false, "Add rule")]
    [InlineData(1, false, "Add rule")]
    [InlineData(2, false, "Add 2 rules")]
    [InlineData(5, false, "Add 5 rules")]
    [InlineData(0, true, "Save")]
    [InlineData(1, true, "Save")]
    public void ConfirmLabel_reflects_how_many_rules_will_be_added(int count, bool isEdit, string expected)
    {
        Assert.Equal(expected, AddRuleSelectionText.ConfirmLabel(count, isEdit));
    }

    [Fact]
    public void DistinctByExeName_drops_case_insensitive_duplicates_keeping_the_first()
    {
        var items = new List<(string ExeName, string DisplayName)>
        {
            ("chrome.exe", "Chrome"),
            ("Code.exe", "Code"),
            ("CHROME.EXE", "chrome (CHROME.EXE)"),
        };

        var result = AddRuleSelectionText.DistinctByExeName(items);

        Assert.Equal(new[] { "chrome.exe", "Code.exe" }, result.Select(r => r.ExeName));
        Assert.Equal("Chrome", result[0].DisplayName);
    }
}
