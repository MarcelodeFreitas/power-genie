namespace PowerGenie.App.Models;

public sealed class AppRule
{
    public required string ExeName { get; set; }
    public required string DisplayName { get; set; }
    public required Guid PlanGuid { get; set; }
}
