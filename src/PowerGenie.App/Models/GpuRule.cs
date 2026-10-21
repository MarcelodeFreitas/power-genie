namespace PowerGenie.App.Models;

// Applies a GPU preset while the named app is running, the GPU counterpart of AppRule.
public sealed class GpuRule
{
    public required string ExeName { get; set; }
    public required string DisplayName { get; set; }
    public required Guid PresetId { get; set; }

    // Defaults to true so a rule written without this property still loads enabled.
    public bool IsEnabled { get; set; } = true;
}
