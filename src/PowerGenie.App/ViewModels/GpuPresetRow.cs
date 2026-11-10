using PowerGenie.App.Models;

namespace PowerGenie.App.ViewModels;

// One line of the GPU presets grid: the preset with its GPU's label and whether it is that GPU's default.
public sealed class GpuPresetRow
{
    public required GpuPreset Preset { get; init; }
    public required string GpuLabel { get; init; }
    public required bool IsDefault { get; init; }

    public string Name => Preset.Name;
    public int Watts => Preset.Watts;
    public string DefaultText => IsDefault ? "Default" : string.Empty;
}
