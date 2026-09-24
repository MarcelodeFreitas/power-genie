namespace PowerGenie.App.ViewModels;

public sealed class RuleRow
{
    public required string ExeName { get; init; }
    public required string DisplayName { get; init; }
    public required Guid PlanGuid { get; init; }
    public required string PlanName { get; init; }
}
