using System.Globalization;
using System.Windows;
using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.App.Views;

public partial class GpuPresetDialog : Window
{
    private readonly GpuCardInfo _card;
    private readonly Guid _presetId;

    // The saved result. Keeps the id of the preset being edited, so rules pointing at it still match.
    public GpuPreset? Preset { get; private set; }

    public GpuPresetDialog(GpuCardInfo card, GpuPreset? existing)
    {
        InitializeComponent();
        _card = card;
        _presetId = existing?.Id ?? Guid.NewGuid();

        Title = existing is null ? "Add GPU preset" : "Edit GPU preset";
        RangeText.Text = $"Allowed: {card.MinWatts}-{card.MaxWatts} W";
        NameTextBox.Text = existing?.Name ?? string.Empty;
        WattsTextBox.Text = (existing?.Watts ?? card.DefaultWatts).ToString(CultureInfo.InvariantCulture);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        if (!GpuPresetValidator.IsValidName(name))
        {
            ErrorText.Text = $"Enter a name of up to {GpuPresetValidator.MaxNameLength} characters.";
            return;
        }

        if (!int.TryParse(WattsTextBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var watts))
        {
            ErrorText.Text = "Enter the power limit as a whole number of watts.";
            return;
        }

        Preset = new GpuPreset
        {
            Id = _presetId,
            Name = name,
            Watts = GpuPresetValidator.Clamp(watts, _card)
        };
        DialogResult = true;
    }
}
