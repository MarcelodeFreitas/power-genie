namespace PowerGenie.App.Models;

// Power limits of the first NVIDIA card, in watts, as reported by nvidia-smi.
public sealed record GpuCardInfo(string Name, int CurrentWatts, int DefaultWatts, int MinWatts, int MaxWatts);
