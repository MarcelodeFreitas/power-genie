using System.Diagnostics;
using System.IO;
using System.Windows;
using PowerGenie.App.Models;

namespace PowerGenie.App.Views;

public partial class AddRuleDialog : Window
{
    private sealed record RunningProcessOption(string DisplayName, string ExeName);

    private readonly List<PowerPlan> _availablePlans;
    private string? _selectedExeName;

    public AppRule? CreatedRule { get; private set; }

    public AddRuleDialog(List<PowerPlan> availablePlans)
    {
        InitializeComponent();
        _availablePlans = availablePlans;
        PlanComboBox.ItemsSource = _availablePlans;

        RunningProcessComboBox.ItemsSource = Process.GetProcesses()
            .Select(TryDescribe)
            .Where(option => option is not null)
            .Select(option => option!)
            .DistinctBy(option => option.ExeName)
            .OrderBy(option => option.DisplayName)
            .ToList();
    }

    private static RunningProcessOption? TryDescribe(Process process)
    {
        try
        {
            var exeName = process.MainModule?.ModuleName;
            return exeName is null ? null : new RunningProcessOption($"{process.ProcessName} ({exeName})", exeName);
        }
        catch
        {
            return null;
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        // Fully qualified to avoid ambiguity with System.Windows.Forms.OpenFileDialog,
        // which is implicitly in scope because this project also enables UseWindowsForms.
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe",
            Title = "Select application"
        };

        if (dialog.ShowDialog() == true)
        {
            SetSelectedExe(Path.GetFileName(dialog.FileName));
        }
    }

    private void RunningProcessComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (RunningProcessComboBox.SelectedItem is RunningProcessOption option)
        {
            SetSelectedExe(option.ExeName);
        }
    }

    private void SetSelectedExe(string exeName)
    {
        _selectedExeName = exeName;
        SelectedExeText.Text = $"Selected: {exeName}";
        if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
        {
            DisplayNameTextBox.Text = Path.GetFileNameWithoutExtension(exeName);
        }

        UpdateOkButtonState();
    }

    private void PlanComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => UpdateOkButtonState();

    private void DisplayNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => UpdateOkButtonState();

    private void UpdateOkButtonState()
    {
        OkButton.IsEnabled = _selectedExeName is not null
            && !string.IsNullOrWhiteSpace(DisplayNameTextBox.Text)
            && PlanComboBox.SelectedItem is not null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedExeName is null || PlanComboBox.SelectedItem is not PowerPlan selectedPlan)
        {
            return;
        }

        CreatedRule = new AppRule
        {
            ExeName = _selectedExeName,
            DisplayName = DisplayNameTextBox.Text,
            PlanGuid = selectedPlan.Guid
        };

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
