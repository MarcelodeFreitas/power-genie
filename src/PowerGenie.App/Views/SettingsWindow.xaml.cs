using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using PowerGenie.App.Models;
using PowerGenie.App.Services;
using PowerGenie.App.ViewModels;

namespace PowerGenie.App.Views;

public partial class SettingsWindow : Window
{
    private readonly ConfigStore _configStore;
    private readonly PowerPlanService _powerPlanService;
    private readonly AutoStartManager _autoStartManager;
    private readonly ObservableCollection<RuleRow> _ruleRows = new();

    private List<PowerPlan> _availablePlans = new();
    private AppConfig _config = new();
    private List<PlanIconRow> _planColorRows = new();
    private bool _isLoading;
    private bool _hasUnsavedChanges;
    private bool _reloadPlansOnActivate;

    public event Action<AppConfig>? ConfigSaved;

    // Lets App.xaml.cs tell the running ProcessMonitorService to refresh its own cached plan
    // list too, so a manual sync here takes effect immediately instead of waiting up to a day.
    public event Action? PlansSyncRequested;

    public SettingsWindow(ConfigStore configStore, PowerPlanService powerPlanService, AutoStartManager autoStartManager)
    {
        InitializeComponent();
        _configStore = configStore;
        _powerPlanService = powerPlanService;
        _autoStartManager = autoStartManager;

        RulesGrid.ItemsSource = _ruleRows;
        LoadState();
    }

    private void LoadState()
    {
        _isLoading = true;
        try
        {
            _availablePlans = _powerPlanService.GetAvailablePlans();
            _config = _configStore.Load();

            DefaultPlanComboBox.ItemsSource = _availablePlans;
            DefaultPlanComboBox.SelectedItem = _availablePlans.FirstOrDefault(p => p.Guid == _config.DefaultPlanGuid)
                ?? _availablePlans.FirstOrDefault();

            StartWithWindowsCheckBox.IsChecked = _config.StartWithWindows;

            RefreshRuleRows();
            RefreshPlanColorRows();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RefreshPlanColorRows()
    {
        // The automatic icons follow the plans' performance, so they are recomputed whenever the
        // plan list is (re)loaded, which also picks up plans created in Power Options.
        var automatic = PlanIconDefaults.Compute(_availablePlans, _powerPlanService.GetPerformanceScores(_availablePlans));

        // Rebuilding the rows must not throw away edits made but not yet saved: a row the user
        // already changed on screen keeps its on-screen style over the saved or automatic one.
        var onScreen = _planColorRows.Where(r => r.IsCustomized).ToDictionary(r => r.PlanGuid, r => r.ToStyle());

        _planColorRows = _availablePlans.Select(plan =>
        {
            var isCustomized = onScreen.ContainsKey(plan.Guid) || _config.PlanIcons.ContainsKey(plan.Guid);
            var style = onScreen.TryGetValue(plan.Guid, out var pending)
                ? pending
                : PlanIconDefaults.Resolve(plan.Guid, _config.PlanIcons, automatic);

            return new PlanIconRow(plan.Guid, plan.Name, style, isCustomized);
        }).ToList();

        PlanColorsItemsControl.ItemsSource = _planColorRows;
    }

    private void PlanIconComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_isLoading)
        {
            _hasUnsavedChanges = true;
        }
    }

    private void DefaultPlanComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_isLoading)
        {
            _hasUnsavedChanges = true;
        }
    }

    private void StartWithWindowsCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (!_isLoading)
        {
            _hasUnsavedChanges = true;
        }
    }

    private RuleStatusFilter CurrentStatusFilter =>
        (RuleStatusFilter)Math.Max(0, RuleStatusFilterComboBox.SelectedIndex);

    private string PlanNameFor(Guid planGuid) =>
        _availablePlans.FirstOrDefault(p => p.Guid == planGuid)?.Name ?? "(deleted plan)";

    // Rebuilds the visible rows from the search box and status filter. "reselect" keeps the
    // selection on a rule after an edit or toggle, if the current filter still shows it.
    private void RefreshRuleRows(AppRule? reselect = null)
    {
        _ruleRows.Clear();
        var visible = RuleFilter.Apply(_config.Rules, RuleSearchTextBox.Text, CurrentStatusFilter, PlanNameFor);
        foreach (var rule in visible)
        {
            _ruleRows.Add(new RuleRow
            {
                ExeName = rule.ExeName,
                DisplayName = rule.DisplayName,
                PlanGuid = rule.PlanGuid,
                PlanName = PlanNameFor(rule.PlanGuid),
                Rule = rule
            });
        }

        if (reselect != null)
        {
            RulesGrid.SelectedItem = _ruleRows.FirstOrDefault(r => ReferenceEquals(r.Rule, reselect));
        }

        if (_ruleRows.Count == 0)
        {
            RulesEmptyText.Text = _config.Rules.Count == 0 ? "No rules yet. Click Add rule." : "No rules match.";
            RulesEmptyText.Visibility = Visibility.Visible;
        }
        else
        {
            RulesEmptyText.Visibility = Visibility.Collapsed;
        }

        UpdateToggleButton();
    }

    private void UpdateToggleButton()
    {
        if (RulesGrid.SelectedItem is RuleRow row)
        {
            ToggleRuleButton.IsEnabled = true;
            ToggleRuleButton.Content = row.Rule.IsEnabled ? "Disable" : "Enable";
        }
        else
        {
            ToggleRuleButton.IsEnabled = false;
            ToggleRuleButton.Content = "Disable";
        }
    }

    private void RuleSearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        RefreshRuleRows();
    }

    private void RuleStatusFilterComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        RefreshRuleRows();
    }

    private void RulesGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateToggleButton();
    }

    private void AddRuleButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddRuleDialog(_availablePlans, allRules: _config.Rules) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.CreatedRules.Count > 0)
        {
            RuleListEditor.AddOrReplace(_config.Rules, dialog.CreatedRules);
            RefreshRuleRows();
            PerformSave();
        }
    }

    private void EditRuleButton_Click(object sender, RoutedEventArgs e)
    {
        // Lookup is by reference to the rule behind the selected row, so filtering can never
        // target the wrong rule.
        if (RulesGrid.SelectedItem is not RuleRow row)
        {
            return;
        }

        var index = _config.Rules.IndexOf(row.Rule);
        if (index < 0)
        {
            return;
        }

        var dialog = new AddRuleDialog(_availablePlans, _config.Rules[index], _config.Rules) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.CreatedRules.Count > 0)
        {
            // The dialog does not know about the enabled flag, so carry the current state over.
            var edited = dialog.CreatedRules[0];
            edited.IsEnabled = row.Rule.IsEnabled;
            RuleListEditor.ReplaceEdited(_config.Rules, index, edited);
            RefreshRuleRows(edited);
            PerformSave();
        }
    }

    // Re-reads the plan list without touching _config or reloading it from disk, so any
    // unsaved edits (a rule just added, a color not yet saved) survive a sync click.
    private void SyncPlansButton_Click(object sender, RoutedEventArgs e) => ReloadPlans();

    // Opens the classic Power Options control panel, where plans can be created, renamed,
    // deleted or tuned. Deliberately not the "rundll32 ... powercfg.cpl,,3" shortcut: that jumps
    // straight into the advanced settings of whichever plan is currently active.
    private void EditPlansButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                Path.Combine(Environment.SystemDirectory, "control.exe"), "powercfg.cpl")
            {
                UseShellExecute = true
            });
            _reloadPlansOnActivate = true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            System.Windows.MessageBox.Show(
                this, $"Couldn't open Windows Power Options.\n\n{ex.Message}", "Power Genie",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // Plans may have been created, renamed or deleted in Power Options, so when the user comes
    // back to this window the list is re-read (bypassing the once-a-day cache).
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (_reloadPlansOnActivate)
        {
            _reloadPlansOnActivate = false;
            ReloadPlans();
        }
    }

    private void ReloadPlans()
    {
        var previousDefaultGuid = (DefaultPlanComboBox.SelectedItem as PowerPlan)?.Guid ?? _config.DefaultPlanGuid;

        _availablePlans = _powerPlanService.GetAvailablePlans();

        _isLoading = true;
        try
        {
            DefaultPlanComboBox.ItemsSource = _availablePlans;
            DefaultPlanComboBox.SelectedItem = _availablePlans.FirstOrDefault(p => p.Guid == previousDefaultGuid)
                ?? _availablePlans.FirstOrDefault();

            RefreshRuleRows();
            RefreshPlanColorRows();
        }
        finally
        {
            _isLoading = false;
        }

        PlansSyncRequested?.Invoke();
    }

    private void RemoveRuleButton_Click(object sender, RoutedEventArgs e)
    {
        // Lookup is by reference to the rule behind the selected row, so filtering can never
        // target the wrong rule.
        if (RulesGrid.SelectedItem is not RuleRow row)
        {
            return;
        }

        var index = _config.Rules.IndexOf(row.Rule);
        if (index < 0)
        {
            return;
        }

        _config.Rules.RemoveAt(index);
        RefreshRuleRows();
        PerformSave();
    }

    private void ToggleRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not RuleRow row)
        {
            return;
        }

        // If the active filter now hides the toggled rule, the selection is simply cleared.
        row.Rule.IsEnabled = !row.Rule.IsEnabled;
        RefreshRuleRows(row.Rule);
        PerformSave();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        PerformSave();
        Close();
    }

    private void PerformSave()
    {
        if (DefaultPlanComboBox.SelectedItem is PowerPlan selectedDefault)
        {
            _config.DefaultPlanGuid = selectedDefault.Guid;
        }

        _config.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;

        // Only customized plans are stored; the rest stay automatic.
        foreach (var row in _planColorRows)
        {
            if (row.IsCustomized)
            {
                _config.PlanIcons[row.PlanGuid] = row.ToStyle();
            }
        }

        _configStore.Save(_config);

        _autoStartManager.SetEnabled(_config.StartWithWindows, Environment.ProcessPath!);

        ConfigSaved?.Invoke(_config);
        _hasUnsavedChanges = false;
    }

    // Closing via the window's X button (or Alt+F4) must not silently discard rules/settings
    // the user added but never explicitly saved. That gap is exactly what caused a rule to
    // vanish without the user realizing it never took effect.
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_hasUnsavedChanges)
        {
            var result = System.Windows.MessageBox.Show(
                "You have unsaved changes. Save before closing?",
                "Power Genie",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                PerformSave();
            }
        }

        base.OnClosing(e);
    }
}
