using PowerGenie.App.Models;

namespace PowerGenie.App.ViewModels;

public sealed class RuleRow
{
    public required string ExeName { get; init; }
    public required string DisplayName { get; init; }
    public required Guid PlanGuid { get; init; }
    public required string PlanName { get; init; }

    // The rule behind this row. Lookups for edit/remove/toggle go through this reference, so a
    // filtered view can never act on the wrong rule.
    public required AppRule Rule { get; init; }

    public bool IsEnabled => Rule.IsEnabled;

    public string Status => Rule.IsEnabled ? "Active" : "Off";
}
