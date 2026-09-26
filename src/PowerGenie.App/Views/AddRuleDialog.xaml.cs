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
    private readonly bool _isEditMode;

    private string? _selectedExeName;
    private bool _suppressManualPathTextChanged;

    // Populated instead of _selectedExeName when 2+ apps are picked at once (via list
    // multi-select or browsing multiple files). Each entry becomes its own rule on OK, all
    // pointing at the one chosen plan. Never used in edit mode - editing is always one rule.
    private List<(string ExeName, string DisplayName)> _multiSelection = new();

    public IReadOnlyList<AppRule> CreatedRules { get; private set; } = Array.Empty<AppRule>();

    // existingRule: when set, pre-fills the dialog so an existing rule's exe, display name,
    // and plan can be edited instead of only ever creating a brand new rule.
    public AddRuleDialog(List<PowerPlan> availablePlans, AppRule? existingRule = null)
    {
        InitializeComponent();
        _availablePlans = availablePlans;
        _isEditMode = existingRule is not null;
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

        if (existingRule is not null)
        {
            Title = "Edit rule";
            _selectedExeName = existingRule.ExeName;

            _suppressManualPathTextChanged = true;
            ManualPathTextBox.Text = existingRule.ExeName;
            _suppressManualPathTextChanged = false;

            DisplayNameTextBox.Text = existingRule.DisplayName;
            PlanComboBox.SelectedItem = _availablePlans.FirstOrDefault(p => p.Guid == existingRule.PlanGuid);
            UpdateOkButtonState();
        }
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
        // Editing is always exactly one rule, regardless of how many items get selected.
        var selected = ResultsListBox.SelectedItems.Cast<SearchResultItem>().ToList();
        if (_isEditMode)
        {
            if (selected.Count > 0)
            {
                SetSelectedExe(selected[0].ExeNameOrPath);
            }

            return;
        }

        if (selected.Count >= 2)
        {
            SetMultiSelection(selected.Select(item => ToRuleSource(item.ExeNameOrPath)).ToList());
        }
        else if (selected.Count == 1)
        {
            ClearMultiSelection();
            SetSelectedExe(selected[0].ExeNameOrPath);
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        // Fully qualified to avoid ambiguity with System.Windows.Forms.OpenFileDialog,
        // which is implicitly in scope because this project also enables UseWindowsForms.
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe",
            Title = "Select application",
            Multiselect = !_isEditMode
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!_isEditMode && dialog.FileNames.Length >= 2)
        {
            SetMultiSelection(dialog.FileNames.Select(ToRuleSource).ToList());
        }
        else
        {
            ClearMultiSelection();
            SetSelectedExe(dialog.FileName);
        }
    }

    private void ManualPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressManualPathTextChanged)
        {
            return;
        }

        ClearMultiSelection();

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

    private static (string ExeName, string DisplayName) ToRuleSource(string exeNameOrPath)
    {
        var exeName = Path.GetFileName(exeNameOrPath);
        return (exeName, Path.GetFileNameWithoutExtension(exeName));
    }

    // A single shared Display Name field doesn't make sense across several different apps, so
    // multi-selection replaces it with a notice and derives each rule's name from its own exe,
    // and disables the single-item path field entirely while active.
    private void SetMultiSelection(List<(string ExeName, string DisplayName)> items)
    {
        _multiSelection = items;
        _selectedExeName = null;

        DisplayNameTextBox.Visibility = Visibility.Collapsed;
        MultiSelectionNoticeText.Visibility = Visibility.Visible;
        MultiSelectionNoticeText.Text = $"{items.Count} apps selected. Each will use its own name.";

        ManualPathTextBox.IsEnabled = false;

        UpdateOkButtonState();
    }

    private void ClearMultiSelection()
    {
        if (_multiSelection.Count == 0)
        {
            return;
        }

        _multiSelection = new List<(string, string)>();

        DisplayNameTextBox.Visibility = Visibility.Visible;
        MultiSelectionNoticeText.Visibility = Visibility.Collapsed;
        ManualPathTextBox.IsEnabled = true;
    }

    private void PlanComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateOkButtonState();

    private void DisplayNameTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateOkButtonState();

    private void UpdateOkButtonState()
    {
        if (_multiSelection.Count > 0)
        {
            OkButton.Content = $"Add {_multiSelection.Count} rules";
            OkButton.IsEnabled = PlanComboBox.SelectedItem is not null;
            return;
        }

        OkButton.Content = _isEditMode ? "Save" : "OK";
        OkButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedExeName)
            && !string.IsNullOrWhiteSpace(DisplayNameTextBox.Text)
            && PlanComboBox.SelectedItem is not null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (PlanComboBox.SelectedItem is not PowerPlan selectedPlan)
        {
            return;
        }

        if (_multiSelection.Count > 0)
        {
            CreatedRules = _multiSelection
                .Select(item => new AppRule
                {
                    ExeName = item.ExeName,
                    DisplayName = item.DisplayName,
                    PlanGuid = selectedPlan.Guid
                })
                .ToList();

            DialogResult = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedExeName))
        {
            return;
        }

        CreatedRules = new List<AppRule>
        {
            new()
            {
                ExeName = _selectedExeName,
                DisplayName = DisplayNameTextBox.Text,
                PlanGuid = selectedPlan.Guid
            }
        };

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
