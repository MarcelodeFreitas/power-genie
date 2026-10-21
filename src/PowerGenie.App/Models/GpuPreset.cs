namespace PowerGenie.App.Models;

// A named GPU power limit the user can pick, e.g. "Silent" at 250 W.
public sealed class GpuPreset
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Watts { get; set; }
}
