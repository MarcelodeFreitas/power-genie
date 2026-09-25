using PowerGenie.App.Services;

namespace PowerGenie.App.ViewModels;

public sealed class PlanColorRow
{
    public required Guid PlanGuid { get; init; }
    public required string PlanName { get; init; }
    public List<PaletteColor> ColorOptions { get; } = PlanColorPalette.Colors.ToList();
    public required PaletteColor SelectedColor { get; set; }
}
