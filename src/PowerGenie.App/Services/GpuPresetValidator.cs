using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class GpuPresetValidator
{
    public const int MaxNameLength = 32;

    // Keeps a preset inside the range the card accepts, so nvidia-smi never gets a value it rejects.
    public static int Clamp(int watts, GpuCardInfo card) => Math.Clamp(watts, card.MinWatts, card.MaxWatts);

    public static bool IsValidName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return trimmed.Length is > 0 and <= MaxNameLength;
    }
}
