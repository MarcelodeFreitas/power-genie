namespace PowerGenie.App.Models;

// One NVIDIA card as reported by nvidia-smi, in watts. Uuid identifies it across reboots,
// since the index can change; PciBusId names the slot, which tells two identical models apart.
public sealed record GpuCardInfo(
    int Index, string Uuid, string PciBusId, string Name, int CurrentWatts, int DefaultWatts, int MinWatts, int MaxWatts)
{
    // "NVIDIA GeForce RTX 3090" -> "RTX 3090".
    public string ShortName => Name.Replace("NVIDIA ", string.Empty).Replace("GeForce ", string.Empty).Trim();

    // "00000000:04:00.0" -> "04:00".
    public string BusLabel
    {
        get
        {
            var parts = PciBusId.Split(':');
            return parts.Length >= 3 ? $"{parts[^2]}:{parts[^1].Split('.')[0]}" : PciBusId;
        }
    }

    public string Label => $"{ShortName} (PCI {BusLabel})";
}
