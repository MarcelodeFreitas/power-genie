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

    // Every app the user has ticked in the list, in pick order. Kept separately from the
    // ListBox's own selection because re-filtering swaps ItemsSource (which clears the ListBox
    // selection): ticked apps that the search hides must stay selected and still be added, with
    // the status line saying how many are hidden, rather than being silently lost.
    private readonly List<SearchResultItem> _picked = new();
    private bool _suppressListSelection;

    // The display name most recently filled in automatically. Lets a later pick replace it
    // (instead of leaving the first app's name on the second one) without ever overwriting a
    // name the user typed themselves.
    private string? _lastAutoDisplayName;
    private readonly IReadOnlyList<AppRule> _allRules;
    private readonly AppRule? _editedRule;

    public IReadOnlyList<AppRule> CreatedRules { get; private set; } = Array.Empty<AppRule>();

    // existingRule: when set, pre-fills the dialog so an existing rule's exe, display name,
    // and plan can be edited instead of only ever creating a brand new rule.
    // allRules: the rules already configured, used to warn before a new rule duplicates one.
    public AddRuleDialog(List<PowerPlan> availablePlans, AppRule? existingRule = null, IReadOnlyList<AppRule>? allRules = null)
    {
        InitializeComponent();
        _availablePlans = availablePlans;
        _allRules = allRules ?? Array.Empty<AppRule>();
        _editedRule = existingRule;
        _isEditMode = existingRule is not null;
        PlanComboBox.ItemsSource = _availablePlans;

        // Editing is always exactly one rule, so it keeps a plain single-select list. Adding gets
        // checkboxes + multi-select so it's visible, without any modifier key, that several apps
        // can be picked at once.
        if (_isEditMode)
        {
            ResultsListBox.SelectionMode = System.Windows.Controls.SelectionMode.Single;
            ResultsListBox.ItemTemplate = (DataTemplate)FindResource("PlainRowTemplate");
            SelectionStatusPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            ResultsListBox.ItemTemplate = (DataTemplate)FindResource("CheckRowTemplate");
        }

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
        }

        UpdateOkButtonState();
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

        var items = filtered.ToList();

        // Swapping ItemsSource clears the ListBox selection; re-tick whatever is still picked
        // and visible. Suppressed so this bookkeeping isn't mistaken for the user un-ticking.
        _suppressListSelection = true;
        try
        {
            ResultsListBox.ItemsSource = items;
            foreach (var item in items.Where(_picked.Contains))
            {
                ResultsListBox.SelectedItems.Add(item);
            }
        }
        finally
        {
            _suppressListSelection = false;
        }

        UpdateSelectionStatus();
    }

    private void SearchSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshResultsList();

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshResultsList();

    private void ResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressListSelection)
        {
            return;
        }

        // Editing is always exactly one rule (the list is single-select there).
        if (_isEditMode)
        {
            if (ResultsListBox.SelectedItem is SearchResultItem editPick)
            {
                SetSelectedExe(editPick.ExeNameOrPath);
            }

            return;
        }

        foreach (var removed in e.RemovedItems.Cast<SearchResultItem>())
        {
            _picked.Remove(removed);
        }

        foreach (var added in e.AddedItems.Cast<SearchResultItem>())
        {
            if (!_picked.Contains(added))
            {
                _picked.Add(added);
            }
        }

        ApplyPicks();
    }

    // Turns the current set of ticked apps into the dialog's selection state: 2+ distinct apps
    // -> multi-select, exactly one -> the ordinary single-app path, none -> back to empty.
    private void ApplyPicks()
    {
        var sources = AddRuleSelectionText.DistinctByExeName(
            _picked.Select(item => ToRuleSource(item.ExeNameOrPath)));

        if (sources.Count >= 2)
        {
            SetMultiSelection(sources);
        }
        else if (sources.Count == 1)
        {
            ClearMultiSelection();
            SetSelectedExe(_picked[0].ExeNameOrPath);
        }
        else
        {
            ClearMultiSelection();
            ClearSingleSelection();
        }
    }

    // Drops all list ticks without treating it as a user selection change. Used when the choice
    // moves elsewhere (Browse, typing a path) so the ticks don't contradict what will be added.
    private void ClearListPicks()
    {
        _picked.Clear();

        _suppressListSelection = true;
        try
        {
            ResultsListBox.UnselectAll();
        }
        finally
        {
            _suppressListSelection = false;
        }
    }

    private void SelectAllShownLink_Click(object sender, RoutedEventArgs e)
    {
        if (!_isEditMode)
        {
            ResultsListBox.SelectAll();
        }
    }

    private void ClearSelectionLink_Click(object sender, RoutedEventArgs e)
    {
        if (_isEditMode)
        {
            return;
        }

        ClearListPicks();
        ApplyPicks();
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

        // Browsing replaces the list choice, so un-tick the list to keep it from contradicting it.
        if (!_isEditMode)
        {
            ClearListPicks();
        }

        if (!_isEditMode && dialog.FileNames.Length >= 2)
        {
            SetMultiSelection(AddRuleSelectionText.DistinctByExeName(dialog.FileNames.Select(ToRuleSource)));
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
        if (!_isEditMode)
        {
            ClearListPicks();
        }

        var text = ManualPathTextBox.Text;
        _selectedExeName = string.IsNullOrWhiteSpace(text) ? null : Path.GetFileName(text);

        if (!string.IsNullOrWhiteSpace(_selectedExeName))
        {
            ApplyAutoDisplayName(Path.GetFileNameWithoutExtension(_selectedExeName));
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

        ApplyAutoDisplayName(Path.GetFileNameWithoutExtension(normalized));

        UpdateOkButtonState();
    }

    // Fills the name from the chosen exe only while the field is empty or still holds a name we
    // filled in ourselves, so a name the user typed is never overwritten.
    private void ApplyAutoDisplayName(string name)
    {
        var current = DisplayNameTextBox.Text;
        if (string.IsNullOrWhiteSpace(current) || current == _lastAutoDisplayName)
        {
            _lastAutoDisplayName = name;
            DisplayNameTextBox.Text = name;
        }
    }

    // Nothing is chosen any more (the last tick was removed): reset the single-app state so the
    // confirm button disables instead of still pointing at the previously ticked app.
    private void ClearSingleSelection()
    {
        _selectedExeName = null;

        _suppressManualPathTextChanged = true;
        ManualPathTextBox.Text = string.Empty;
        _suppressManualPathTextChanged = false;

        if (DisplayNameTextBox.Text == _lastAutoDisplayName)
        {
            DisplayNameTextBox.Text = string.Empty;
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
        MultiSelectionNoticeText.Text = "Each rule will use its app's own name.";

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
        OkButton.Content = AddRuleSelectionText.ConfirmLabel(EffectiveRuleCount, _isEditMode);

        if (_multiSelection.Count > 0)
        {
            OkButton.IsEnabled = PlanComboBox.SelectedItem is not null;
        }
        else
        {
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedExeName)
                && !string.IsNullOrWhiteSpace(DisplayNameTextBox.Text)
                && PlanComboBox.SelectedItem is not null;
        }

        UpdateSelectionStatus();
    }

    // How many rules OK would create right now.
    private int EffectiveRuleCount => _multiSelection.Count > 0
        ? _multiSelection.Count
        : string.IsNullOrWhiteSpace(_selectedExeName) ? 0 : 1;

    private void UpdateSelectionStatus()
    {
        if (_isEditMode)
        {
            return;
        }

        // Picks the current search/source hides are still selected (and still added); report
        // them so the count never disagrees with what the button will do.
        var hidden = Math.Max(0, _picked.Count - ResultsListBox.SelectedItems.Count);
        var rules = EffectiveRuleCount;
        var selected = _picked.Count > 0 ? _picked.Count : rules;
        SelectionStatusText.Text = AddRuleSelectionText.Summary(selected, hidden, rules);
        ClearSelectionLink.IsEnabled = selected > 0;
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

            if (!ConfirmReplacingExistingRules())
            {
                return;
            }

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

        if (!ConfirmReplacingExistingRules())
        {
            return;
        }

        DialogResult = true;
    }

    // A rule for the same exe already exists: block silently adding a contradictory second one.
    // Replace swaps the old rule for the new one; Cancel keeps the dialog open so nothing is lost.
    private bool ConfirmReplacingExistingRules()
    {
        var conflicts = RuleListEditor.FindConflicts(_allRules, CreatedRules, _editedRule);
        if (conflicts.Count == 0)
        {
            return true;
        }

        var list = string.Join("\n", conflicts.Select(rule =>
        {
            var plan = _availablePlans.FirstOrDefault(p => p.Guid == rule.PlanGuid)?.Name ?? "(deleted plan)";
            return $"  \u2022 {rule.ExeName} \u2192 {plan}";
        }));

        var message = conflicts.Count == 1
            ? $"A rule for this app already exists:\n\n{list}\n\nReplace it with the new rule?"
            : $"Rules for these apps already exist:\n\n{list}\n\nReplace them with the new rules?";

        // Yes = Replace, No = Cancel: the standard MessageBox has no custom button text.
        var answer = System.Windows.MessageBox.Show(
            this,
            message + "\n\nYes = Replace    No = Cancel",
            "Rule already exists",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return answer == MessageBoxResult.Yes;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
