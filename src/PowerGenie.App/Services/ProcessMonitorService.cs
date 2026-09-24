using System.Diagnostics;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public sealed class ProcessMonitorService : IDisposable
{
    private readonly PowerPlanService _powerPlanService;
    private readonly FileLogger _logger;
    private readonly System.Timers.Timer _timer;

    private AppConfig _config;
    private readonly List<PowerPlan> _availablePlans;
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
        _availablePlans = _powerPlanService.GetAvailablePlans();

        _timer = new System.Timers.Timer(pollInterval.TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += (_, _) => Tick();
    }

    public void UpdateConfig(AppConfig config) => _config = config;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private void Tick()
    {
        try
        {
            var runningExeNames = Process.GetProcesses()
                .Select(TryGetExeName)
                .Where(name => name is not null)
                .Select(name => name!)
                .ToList();

            var availableGuids = new HashSet<Guid>(_availablePlans.Select(p => p.Guid));

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

    private static string? TryGetExeName(Process process)
    {
        try
        {
            return process.MainModule?.ModuleName;
        }
        catch
        {
            // Access denied on elevated/system processes is expected — skip them.
            return null;
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
