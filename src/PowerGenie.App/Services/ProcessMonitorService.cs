using System.Diagnostics;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public sealed class ProcessMonitorService : IDisposable
{
    private readonly PowerPlanService _powerPlanService;
    private readonly FileLogger _logger;
    private readonly System.Timers.Timer _timer;
    private readonly TimeSpan _plansRefreshInterval;
    private readonly Func<DateTime> _utcNowProvider;

    private AppConfig _config;
    private Guid? _lastAppliedPlanGuid;
    private List<PowerPlan> _availablePlans = new();
    private IReadOnlyDictionary<Guid, int> _planScores = new Dictionary<Guid, int>();
    private DateTime _lastPlansRefreshUtc;

    // Carries the already-fetched plan list along so subscribers (the tray icon) don't need
    // a second powercfg call just to look up the new plan's name.
    public event Action<Guid, IReadOnlyList<PowerPlan>>? ActivePlanChanged;

    public ProcessMonitorService(
        PowerPlanService powerPlanService,
        FileLogger logger,
        AppConfig initialConfig,
        TimeSpan pollInterval,
        TimeSpan? plansRefreshInterval = null,
        Func<DateTime>? utcNowProvider = null)
    {
        _powerPlanService = powerPlanService;
        _logger = logger;
        _config = initialConfig;
        _plansRefreshInterval = plansRefreshInterval ?? TimeSpan.FromDays(1);
        _utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);

        RefreshAvailablePlans();

        _timer = new System.Timers.Timer(pollInterval.TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += (_, _) => Tick();
    }

    public void UpdateConfig(AppConfig config) => _config = config;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    // Shelling out to powercfg to list plans is rarely useful (the user almost never creates
    // or deletes a Windows power plan while the app is running), so the list is cached and
    // only re-fetched once a day by default, or immediately via this method - wired to a
    // "Sync power plans" button in Settings for the rare case someone wants it sooner.
    public void RefreshAvailablePlans()
    {
        _availablePlans = _powerPlanService.GetAvailablePlans();
        _lastPlansRefreshUtc = _utcNowProvider();

        // Scored here, with the plan list, so the per-tick resolve never shells out to powercfg.
        _planScores = _availablePlans.ToDictionary(p => p.Guid, p => _powerPlanService.GetPerformanceScore(p.Guid));
    }

    // Called when Power Genie exits so a rule's high-performance plan isn't left active (and
    // draining battery / making noise) once nothing is watching the rules any more.
    public void RestoreDefaultPlan()
    {
        try
        {
            if (_availablePlans.Any(p => p.Guid == _config.DefaultPlanGuid))
            {
                _powerPlanService.SetActivePlan(_config.DefaultPlanGuid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Restoring the default plan on exit failed", ex);
        }
    }

    internal void Tick()
    {
        try
        {
            if (_utcNowProvider() - _lastPlansRefreshUtc >= _plansRefreshInterval)
            {
                RefreshAvailablePlans();
            }

            var availablePlans = _availablePlans;
            var runningExeNames = GetRunningExeNames();
            var availableGuids = new HashSet<Guid>(availablePlans.Select(p => p.Guid));

            var resolvedPlanGuid = RuleResolver.ResolveActivePlan(
                runningExeNames,
                _config.Rules,
                availableGuids,
                _config.DefaultPlanGuid,
                _planScores);

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
