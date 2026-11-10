using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.App.Views;

public partial class GpuPresetDialog : Window
{
    private readonly bool _isEditing;
    private readonly Guid _presetId;

    // The saved result. Keeps the id of the preset being edited, so rules pointing at it still match.
    public GpuPreset? Preset { get; private set; }

    public GpuPresetDialog(IReadOnlyList<GpuCardInfo> cards, GpuPreset? existing)
    {
        InitializeComponent();
        _isEditing = existing is not null;
        _presetId = existing?.Id ?? Guid.NewGuid();

        Title = existing is null ? "Add GPU preset" : "Edit GPU preset";
        GpuComboBox.ItemsSource = cards;
        GpuComboBox.SelectedItem = cards.FirstOrDefault(c => c.Uuid == existing?.GpuUuid) ?? cards.FirstOrDefault();
        GpuComboBox.IsEnabled = !_isEditing;
        NameTextBox.Text = existing?.Name ?? string.Empty;
        WattsTextBox.Text = (existing?.Watts ?? (GpuComboBox.SelectedItem as GpuCardInfo)?.DefaultWatts ?? 0)
            .ToString(CultureInfo.InvariantCulture);
        UpdateRange();
    }

    // Adding a preset starts it at the chosen GPU's default watts.
    private void GpuComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isEditing && GpuComboBox.SelectedItem is GpuCardInfo card)
        {
            WattsTextBox.Text = card.DefaultWatts.ToString(CultureInfo.InvariantCulture);
        }

        UpdateRange();
    }

    private void UpdateRange()
    {
        RangeText.Text = GpuComboBox.SelectedItem is GpuCardInfo card
            ? $"Allowed: {card.MinWatts}-{card.MaxWatts} W"
            : string.Empty;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (GpuComboBox.SelectedItem is not GpuCardInfo card)
        {
            ErrorText.Text = "Choose the GPU this preset sets.";
            return;
        }

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
            Watts = GpuPresetValidator.Clamp(watts, card),
            GpuUuid = card.Uuid,
        };
        DialogResult = true;
    }
}
