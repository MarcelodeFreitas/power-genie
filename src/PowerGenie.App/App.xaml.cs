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
    private FileLogger? _logger;
    private Models.AppConfig? _currentConfig;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _logger = new FileLogger(FileLogger.GetDefaultFilePath());
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        _configStore = new ConfigStore(ConfigStore.GetDefaultFilePath());
        _powerPlanService = new PowerPlanService();
        _autoStartManager = new AutoStartManager();
        var logger = _logger;

        _currentConfig = _configStore.Load();

        _monitor = new ProcessMonitorService(_powerPlanService, logger, _currentConfig, TimeSpan.FromSeconds(3));
        _monitor.ActivePlanChanged += OnActivePlanChanged;
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
        _settingsWindow.ConfigSaved += config =>
        {
            _currentConfig = config;
            _monitor!.UpdateConfig(config);
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    // Fires on the monitor's timer thread, not the UI thread — NotifyIcon must only be
    // touched from the thread that created it, so this hops back via the Dispatcher.
    private void OnActivePlanChanged(Guid planGuid, IReadOnlyList<Models.PowerPlan> availablePlans)
    {
        var plan = availablePlans.FirstOrDefault(p => p.Guid == planGuid);
        var planName = plan?.Name ?? "Unknown plan";
        var planColors = _currentConfig?.PlanColors ?? new Dictionary<Guid, string>();
        var colorHex = PlanColorPalette.GetColorForPlan(
            planGuid, planColors, availablePlans.Select(p => p.Guid).ToList());

        Dispatcher.Invoke(() => _trayIconManager?.UpdateActivePlan(planName, colorHex));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Stop();
        _monitor?.Dispose();
        _trayIconManager?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError("Unhandled UI exception", e.Exception);
        // Fully qualified to avoid ambiguity with System.Windows.Forms.MessageBox,
        // which is implicitly in scope because this project also enables UseWindowsForms.
        System.Windows.MessageBox.Show(
            $"Power Genie hit an unexpected error and this action couldn't complete:\n\n{e.Exception.Message}\n\nDetails were written to the log file.",
            "Power Genie",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger?.LogError("Unhandled non-UI exception", e.ExceptionObject as Exception);
    }
}
