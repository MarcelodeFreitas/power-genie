using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private bool _isLoading;
    private bool _hasUnsavedChanges;

    public event Action<AppConfig>? ConfigSaved;

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
        }
        finally
        {
            _isLoading = false;
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

    private void RefreshRuleRows()
    {
        _ruleRows.Clear();
        foreach (var rule in _config.Rules)
        {
            var planName = _availablePlans.FirstOrDefault(p => p.Guid == rule.PlanGuid)?.Name ?? "(deleted plan)";
            _ruleRows.Add(new RuleRow
            {
                ExeName = rule.ExeName,
                DisplayName = rule.DisplayName,
                PlanGuid = rule.PlanGuid,
                PlanName = planName
            });
        }
    }

    private void AddRuleButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddRuleDialog(_availablePlans) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.CreatedRule is not null)
        {
            _config.Rules.Add(dialog.CreatedRule);
            _hasUnsavedChanges = true;
            RefreshRuleRows();
        }
    }

    private void RemoveRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is RuleRow selected)
        {
            _config.Rules.RemoveAll(r => r.ExeName == selected.ExeName && r.PlanGuid == selected.PlanGuid);
            _hasUnsavedChanges = true;
            RefreshRuleRows();
        }
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
        _configStore.Save(_config);

        _autoStartManager.SetEnabled(_config.StartWithWindows, Environment.ProcessPath!);

        ConfigSaved?.Invoke(_config);
        _hasUnsavedChanges = false;
    }

    // Closing via the window's X button (or Alt+F4) must not silently discard rules/settings
    // the user added but never explicitly saved — that gap is exactly what caused a rule to
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
