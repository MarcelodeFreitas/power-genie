namespace PowerGenie.App.Models;

public sealed class AppRule
{
    public required string ExeName { get; set; }
    public required string DisplayName { get; set; }
    public required Guid PlanGuid { get; set; }

    // Defaults to true so config.json files written before this property existed still load enabled.
    public bool IsEnabled { get; set; } = true;
}
