using PowerGenie.App.Services;

namespace PowerGenie.App.ViewModels;

public sealed class PlanColorRow
{
    public required Guid PlanGuid { get; init; }
    public required string PlanName { get; init; }
    public List<(string Name, string Hex)> ColorOptions { get; } = PlanColorPalette.Colors.ToList();
    public required (string Name, string Hex) SelectedColor { get; set; }
}
