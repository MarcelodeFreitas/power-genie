using PowerGenie.App.Models;
using PowerGenie.App.ViewModels;

namespace PowerGenie.Tests;

public class GpuRuleRowTests
{
    private static GpuRuleRow Row(bool enabled) => new()
    {
        ExeName = "game.exe",
        DisplayName = "Game",
        PresetId = Guid.NewGuid(),
        PresetName = "Max",
        Rule = new GpuRule { ExeName = "game.exe", DisplayName = "Game", PresetId = Guid.NewGuid(), IsEnabled = enabled }
    };

    [Fact]
    public void Enabled_rule_shows_active()
    {
        var row = Row(enabled: true);

        Assert.True(row.IsEnabled);
        Assert.Equal("Active", row.Status);
    }

    [Fact]
    public void Disabled_rule_shows_off()
    {
        var row = Row(enabled: false);

        Assert.False(row.IsEnabled);
        Assert.Equal("Off", row.Status);
    }
}
