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

    // Carries the already-fetched plan list along so subscribers (the tray icon) don't need
    // a second powercfg call just to look up the new plan's name.
    public event Action<Guid, IReadOnlyList<PowerPlan>>? ActivePlanChanged;

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
            var runningExeNames = GetRunningExeNames();
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
                ActivePlanChanged?.Invoke(resolvedPlanGuid, availablePlans);
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

    // Process wraps a native OS handle and is IDisposable; Process.GetProcesses() returns one
    // per running process (typically 300-400+) on every tick, forever, so each one is disposed
    // right after its name is read instead of leaking handles for the app's whole lifetime.
    private static List<string> GetRunningExeNames()
    {
        var names = new List<string>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                names.Add(GetExeName(process));
            }
            finally
            {
                process.Dispose();
            }
        }

        return names;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
