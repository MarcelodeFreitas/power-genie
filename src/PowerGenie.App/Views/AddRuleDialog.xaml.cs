using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.App.Views;

public partial class AddRuleDialog : Window
{
    private sealed record SearchResultItem(string DisplayName, string ExeNameOrPath);

    private readonly List<PowerPlan> _availablePlans;
    private readonly List<SearchResultItem> _installedProgramItems;
    private readonly List<SearchResultItem> _runningProcessItems;

    private string? _selectedExeName;
    private bool _suppressManualPathTextChanged;

    public AppRule? CreatedRule { get; private set; }

    public AddRuleDialog(List<PowerPlan> availablePlans)
    {
        InitializeComponent();
        _availablePlans = availablePlans;
        PlanComboBox.ItemsSource = _availablePlans;

        // Populated before SearchSourceComboBox.SelectedIndex is set below — that assignment
        // fires SelectionChanged synchronously, and its handler (RefreshResultsList) reads
        // these two fields.
        _installedProgramItems = InstalledAppsReader.GetInstalledApps()
            .Select(app => new SearchResultItem(app.DisplayName, app.ExePath))
            .ToList();

        _runningProcessItems = Process.GetProcesses()
            .Select(TryDescribe)
            .Where(item => item is not null)
            .Select(item => item!)
            .DistinctBy(item => item.ExeNameOrPath)
            .OrderBy(item => item.DisplayName)
            .ToList();

        SearchSourceComboBox.ItemsSource = new[] { "Installed Programs", "Running Processes" };
        SearchSourceComboBox.SelectedIndex = 0;
    }

    private static SearchResultItem? TryDescribe(Process process)
    {
        // MainModule is only used for a nicer label here; ProcessName-based naming (matching
        // ProcessMonitorService.GetExeName) is the fallback so elevated/protected processes
        // still show up and can be picked, instead of silently disappearing from the list.
        string exeName;
        try
        {
            exeName = process.MainModule?.ModuleName ?? process.ProcessName + ".exe";
        }
        catch
        {
            exeName = process.ProcessName + ".exe";
        }

        return new SearchResultItem($"{process.ProcessName} ({exeName})", exeName);
    }

    private void RefreshResultsList()
    {
        var source = SearchSourceComboBox.SelectedIndex == 1 ? _runningProcessItems : _installedProgramItems;
        var query = SearchTextBox.Text;

        IEnumerable<SearchResultItem> filtered = source;
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = source.Where(item => item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        ResultsListBox.ItemsSource = filtered.ToList();
    }

    private void SearchSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshResultsList();

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshResultsList();

    private void ResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsListBox.SelectedItem is SearchResultItem item)
        {
            SetSelectedExe(item.ExeNameOrPath);
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
            SetSelectedExe(dialog.FileName);
        }
    }

    private void ManualPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressManualPathTextChanged)
        {
            return;
        }

        var text = ManualPathTextBox.Text;
        _selectedExeName = string.IsNullOrWhiteSpace(text) ? null : Path.GetFileName(text);

        if (!string.IsNullOrWhiteSpace(_selectedExeName) && string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
        {
            DisplayNameTextBox.Text = Path.GetFileNameWithoutExtension(_selectedExeName);
        }

        UpdateOkButtonState();
    }

    // Accepts either a bare exe name (from the running-process list) or a full path
    // (from Installed Programs or Browse). Only the bare filename is ever used to build the
    // rule — RuleResolver matches against ProcessMonitorService.GetExeName's bare exe names —
    // but the full path is still shown in the text box for the user's own confirmation.
    private void SetSelectedExe(string exeNameOrPath)
    {
        var normalized = Path.GetFileName(exeNameOrPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        _selectedExeName = normalized;

        if (ManualPathTextBox.Text != exeNameOrPath)
        {
            _suppressManualPathTextChanged = true;
            ManualPathTextBox.Text = exeNameOrPath;
            _suppressManualPathTextChanged = false;
        }

        if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
        {
            DisplayNameTextBox.Text = Path.GetFileNameWithoutExtension(normalized);
        }

        UpdateOkButtonState();
    }

    private void PlanComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateOkButtonState();

    private void DisplayNameTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateOkButtonState();

    private void UpdateOkButtonState()
    {
        OkButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedExeName)
            && !string.IsNullOrWhiteSpace(DisplayNameTextBox.Text)
            && PlanComboBox.SelectedItem is not null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedExeName) || PlanComboBox.SelectedItem is not PowerPlan selectedPlan)
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
