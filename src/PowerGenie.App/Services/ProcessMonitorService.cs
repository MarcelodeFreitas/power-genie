using System.Diagnostics;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public sealed class ProcessMonitorService : IDisposable
{
    private readonly PowerPlanService _powerPlanService;
    private readonly FileLogger _logger;
    private readonly System.Timers.Timer _timer;

    private AppConfig _config;
    private Guid? _lastAppliedPlanGuid;

    public ProcessMonitorService(
        PowerPlanService powerPlanService,
        FileLogger logger,
        AppConfig initialConfig,
        TimeSpan pollInterval)
    {
        _powerPlanService = powerPlanService;
        _logger = logger;
        _config = initialConfig;

        _timer = new System.Timers.Timer(pollInterval.TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += (_, _) => Tick();
    }

    public void UpdateConfig(AppConfig config) => _config = config;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    // Fetched fresh on every tick (not cached at construction) so a power plan created or
    // deleted while the app is running is picked up without needing a restart.
    internal void Tick()
    {
        try
        {
            var availablePlans = _powerPlanService.GetAvailablePlans();

            var runningExeNames = Process.GetProcesses()
                .Select(GetExeName)
                .ToList();

            var availableGuids = new HashSet<Guid>(availablePlans.Select(p => p.Guid));

            var resolvedPlanGuid = RuleResolver.ResolveActivePlan(
                runningExeNames,
                _config.Rules,
                availableGuids,
                _config.DefaultPlanGuid);

            if (resolvedPlanGuid != Guid.Empty && resolvedPlanGuid != _lastAppliedPlanGuid)
            {
                _powerPlanService.SetActivePlan(resolvedPlanGuid);
                _lastAppliedPlanGuid = resolvedPlanGuid;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Process monitor tick failed", ex);
        }
    }

    // ProcessName is always readable for any live process, unlike MainModule (which throws
    // for elevated/system processes the current user can't inspect) — so rules can match
    // apps running as administrator instead of silently never firing for them.
    internal static string GetExeName(Process process) => process.ProcessName + ".exe";

    public void Dispose()
    {
        _timer.Dispose();
    }
}
