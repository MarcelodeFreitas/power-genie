using System.Collections.ObjectModel;
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
        _availablePlans = _powerPlanService.GetAvailablePlans();
        _config = _configStore.Load();

        DefaultPlanComboBox.ItemsSource = _availablePlans;
        DefaultPlanComboBox.SelectedItem = _availablePlans.FirstOrDefault(p => p.Guid == _config.DefaultPlanGuid)
            ?? _availablePlans.FirstOrDefault();

        StartWithWindowsCheckBox.IsChecked = _config.StartWithWindows;

        RefreshRuleRows();
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
            RefreshRuleRows();
        }
    }

    private void RemoveRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is RuleRow selected)
        {
            _config.Rules.RemoveAll(r => r.ExeName == selected.ExeName && r.PlanGuid == selected.PlanGuid);
            RefreshRuleRows();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DefaultPlanComboBox.SelectedItem is PowerPlan selectedDefault)
        {
            _config.DefaultPlanGuid = selectedDefault.Guid;
        }

        _config.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _configStore.Save(_config);

        var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        _autoStartManager.SetEnabled(_config.StartWithWindows, exePath);

        ConfigSaved?.Invoke(_config);
        Close();
    }
}
