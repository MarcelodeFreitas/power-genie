namespace PowerGenie.App.Models;

// A named GPU power limit the user can pick, e.g. "Silent" at 250 W. Each preset belongs to one GPU.
public sealed class GpuPreset
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Watts { get; set; }

    // UUID of the GPU this preset sets. Empty only in configs saved before per-GPU presets existed.
    public string GpuUuid { get; set; } = string.Empty;

    // The unmodifiable preset created for each GPU at its stock watts.
    public bool IsOriginal { get; set; }
}
