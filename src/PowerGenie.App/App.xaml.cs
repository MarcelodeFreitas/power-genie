using System.Windows;
using Application = System.Windows.Application;
using PowerGenie.App.Models;
using PowerGenie.App.Services;
using PowerGenie.App.Views;

namespace PowerGenie.App;

public partial class App : Application
{
    private TrayIconManager? _trayIconManager;
    private ProcessMonitorService? _monitor;
    private ConfigStore? _configStore;
    private PowerPlanService? _powerPlanService;
    private GpuPowerService? _gpuPowerService;
    private IReadOnlyList<GpuCardInfo> _gpuCards = Array.Empty<GpuCardInfo>();
    private AutoStartManager? _autoStartManager;
    private SettingsWindow? _settingsWindow;
    private FileLogger? _logger;
    private Models.AppConfig? _currentConfig;
    private bool _sessionEnding;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _logger = new FileLogger(FileLogger.GetDefaultFilePath());
        SessionEnding += (_, _) =>
        {
            _sessionEnding = true;
            _monitor?.Stop();
        };
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        _configStore = new ConfigStore(ConfigStore.GetDefaultFilePath());
        _powerPlanService = new PowerPlanService();
        _autoStartManager = new AutoStartManager();
        var logger = _logger;

        _currentConfig = _configStore.Load();

        var gpuService = new GpuPowerService();
        _gpuCards = gpuService.Detect();
        if (_gpuCards.Count > 0)
        {
            _gpuPowerService = gpuService;
            PrepareGpuPresets();
        }

        _monitor = new ProcessMonitorService(_powerPlanService, logger, _currentConfig, TimeSpan.FromSeconds(3),
            gpuPowerService: _gpuPowerService);
        _monitor.ActivePlanChanged += OnActivePlanChanged;
        _monitor.Start();

        var gpuTrayMenu = _gpuCards.Count == 0
            ? null
            : new GpuTrayMenu(
                () => _gpuCards,
                () => _currentConfig!.GpuPresets,
                gpuUuid => _monitor!.AppliedGpuPresetIdFor(gpuUuid),
                ApplyGpuPreset);
        _trayIconManager = new TrayIconManager(OpenSettings, () => _monitor!.DescribeActiveRules(), gpuTrayMenu);
        _trayIconManager.Show();
    }

    // The presets' scheduled tasks must exist before the first tick applies one. Missing tasks are
    // registered now, which shows one UAC prompt. If the user declines, the app still starts and the
    // reason is logged; Settings > GPU asks again when presets are saved.
    private void PrepareGpuPresets()
    {
        if (GpuPresetDefaults.Reconcile(_currentConfig!, _gpuCards))
        {
            _configStore!.Save(_currentConfig!);
        }

        try
        {
            var config = _currentConfig!;
            var service = _gpuPowerService!;
            var outdated = config.GpuTaskVersion != GpuTasks.Version;
            if (outdated || !service.AreRegistered(config.GpuPresets))
            {
                if (service.Register(config.GpuPresets, _gpuCards))
                {
                    config.GpuTaskVersion = GpuTasks.Version;
                    _configStore!.Save(config);
                }
                else
                {
                    _logger?.LogError("GPU presets are not set up because administrator approval was declined");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Setting up the GPU presets failed", ex);
        }
    }

    // Tray menu pick. Failures are shown, since the user is looking at the menu right now.
    private void ApplyGpuPreset(Guid presetId)
    {
        try
        {
            _monitor!.ApplyGpuPreset(presetId);
        }
        catch (Exception ex)
        {
            _logger?.LogError("Applying a GPU preset from the tray failed", ex);
            System.Windows.MessageBox.Show(
                $"Couldn't set the GPU power limit.\n\n{ex.Message}\n\nOpen Settings > GPU and save the presets to set them up again.",
                "Power Genie",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_configStore!, _powerPlanService!, _autoStartManager!, _gpuPowerService, _gpuCards);
        _settingsWindow.ConfigSaved += config =>
        {
            _currentConfig = config;
            _monitor!.UpdateConfig(config);

            // Re-color assignments for the plan that's already active don't trigger
            // ActivePlanChanged (nothing about the active plan itself changed), so the icon
            // would otherwise only pick up a new color the next time the plan actually
            // switches. Refresh it immediately against whatever the OS reports as active now.
            RefreshTrayIconForCurrentPlan();
        };
        _settingsWindow.PlansSyncRequested += () => _monitor!.RefreshAvailablePlans();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void RefreshTrayIconForCurrentPlan()
    {
        var availablePlans = _powerPlanService!.GetAvailablePlans();
        var activePlan = availablePlans.FirstOrDefault(p => p.IsActive);
        if (activePlan is null)
        {
            return;
        }

        _trayIconManager?.UpdateActivePlan(activePlan.Name, ResolveIcon(activePlan.Guid, availablePlans));
    }

    // Fires on the monitor's timer thread, not the UI thread — NotifyIcon must only be
    // touched from the thread that created it, so this hops back via the Dispatcher.
    private void OnActivePlanChanged(Guid planGuid, IReadOnlyList<Models.PowerPlan> availablePlans)
    {
        var plan = availablePlans.FirstOrDefault(p => p.Guid == planGuid);
        var planName = plan?.Name ?? "Unknown plan";
        var style = ResolveIcon(planGuid, availablePlans);

        Dispatcher.Invoke(() => _trayIconManager?.UpdateActivePlan(planName, style));
    }

    // The user's saved icon for the plan, or the automatic one chosen from its performance rank.
    private Models.PlanIconStyle ResolveIcon(Guid planGuid, IReadOnlyList<Models.PowerPlan> availablePlans)
    {
        var automatic = PlanIconDefaults.Compute(availablePlans, _monitor!.PlanScores);
        var customized = _currentConfig?.PlanIcons ?? new Dictionary<Guid, Models.PlanIconStyle>();
        return PlanIconDefaults.Resolve(planGuid, customized, automatic);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Stop();
        // Launching powercfg while Windows is shutting down fails with 0xc0000142 and shows an
        // error popup, so the default plan is only restored on a normal exit.
        if (!_sessionEnding)
        {
            _monitor?.RestoreDefaultPlan();
            _monitor?.RestoreDefaultGpuPresets();
        }

        _monitor?.Dispose();
        _trayIconManager?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError("Unhandled UI exception", e.Exception);
        e.Handled = true;

        // A dialog during Windows shutdown only blocks the shutdown and can't be read, so the
        // error is logged and the shutdown continues.
        if (_sessionEnding)
        {
            return;
        }

        // Fully qualified to avoid ambiguity with System.Windows.Forms.MessageBox,
        // which is implicitly in scope because this project also enables UseWindowsForms.
        System.Windows.MessageBox.Show(
            $"Power Genie hit an unexpected error and this action couldn't complete:\n\n{e.Exception.Message}\n\nDetails were written to the log file.",
            "Power Genie",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger?.LogError("Unhandled non-UI exception", e.ExceptionObject as Exception);
    }
}
