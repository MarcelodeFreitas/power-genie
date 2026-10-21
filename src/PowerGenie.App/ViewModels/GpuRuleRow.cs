using PowerGenie.App.Models;

namespace PowerGenie.App.ViewModels;

public sealed class GpuRuleRow
{
    public required string ExeName { get; init; }
    public required string DisplayName { get; init; }
    public required Guid PresetId { get; init; }
    public required string PresetName { get; init; }

    // The rule behind this row, so filtered views can never act on the wrong rule.
    public required GpuRule Rule { get; init; }

    public bool IsEnabled => Rule.IsEnabled;

    public string Status => Rule.IsEnabled ? "Active" : "Off";
}
