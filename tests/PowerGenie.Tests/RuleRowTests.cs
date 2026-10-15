using PowerGenie.App.Models;
using PowerGenie.App.ViewModels;

namespace PowerGenie.Tests;

public class RuleRowTests
{
    [Fact]
    public void EnabledRule_ShowsActiveStatus()
    {
        var row = new RuleRow
        {
            ExeName = "game.exe",
            DisplayName = "Game",
            PlanGuid = Guid.NewGuid(),
            PlanName = "High performance",
            Rule = new AppRule
            {
                ExeName = "game.exe",
                DisplayName = "Game",
                PlanGuid = Guid.NewGuid(),
                IsEnabled = true
            }
        };

        Assert.True(row.IsEnabled);
        Assert.Equal("Active", row.Status);
    }

    [Fact]
    public void DisabledRule_ShowsOffStatus()
    {
        var row = new RuleRow
        {
            ExeName = "game.exe",
            DisplayName = "Game",
            PlanGuid = Guid.NewGuid(),
            PlanName = "High performance",
            Rule = new AppRule
            {
                ExeName = "game.exe",
                DisplayName = "Game",
                PlanGuid = Guid.NewGuid(),
                IsEnabled = false
            }
        };

        Assert.False(row.IsEnabled);
        Assert.Equal("Off", row.Status);
    }
}
