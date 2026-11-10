using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class GpuPresetText
{
    // The preset's name, with its GPU added when there is more than one card, so presets with the same
    // name on different GPUs can be told apart. With one card the name is shown as it is.
    public static string Name(GpuPreset preset, IReadOnlyList<GpuCardInfo> cards)
    {
        if (cards.Count < 2)
        {
            return preset.Name;
        }

        var card = cards.FirstOrDefault(c => c.Uuid == preset.GpuUuid);
        return card is null ? $"{preset.Name} (missing GPU)" : $"{preset.Name} - {card.Label}";
    }
}
