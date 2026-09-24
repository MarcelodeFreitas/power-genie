using System.Windows;
using Application = System.Windows.Application;
using PowerGenie.App.Services;
using PowerGenie.App.Views;

namespace PowerGenie.App;

public partial class App : Application
{
    private TrayIconManager? _trayIconManager;
    private ProcessMonitorService? _monitor;
    private ConfigStore? _configStore;
    private PowerPlanService? _powerPlanService;
    private AutoStartManager? _autoStartManager;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _configStore = new ConfigStore(ConfigStore.GetDefaultFilePath());
        _powerPlanService = new PowerPlanService();
        _autoStartManager = new AutoStartManager();
        var logger = new FileLogger(FileLogger.GetDefaultFilePath());

        var config = _configStore.Load();

        _monitor = new ProcessMonitorService(_powerPlanService, logger, config, TimeSpan.FromSeconds(3));
        _monitor.Start();

        _trayIconManager = new TrayIconManager(OpenSettings);
        _trayIconManager.Show();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_configStore!, _powerPlanService!, _autoStartManager!);
        _settingsWindow.ConfigSaved += config => _monitor!.UpdateConfig(config);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Stop();
        _monitor?.Dispose();
        _trayIconManager?.Dispose();
        base.OnExit(e);
    }
}
