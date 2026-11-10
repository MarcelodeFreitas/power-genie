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
    private readonly GpuPowerService? _gpuPowerService;

    private AppConfig _config;
    private Guid? _lastAppliedPlanGuid;

    // Per GPU UUID: the preset the rules chose last (starts empty, so the first tick applies the rules'
    // choice), and the preset currently applied, whether the rules or the tray chose it.
    private readonly Dictionary<string, Guid> _lastResolvedGpuPresets = new();
    private readonly Dictionary<string, Guid> _appliedGpuPresets = new();
    private List<PowerPlan> _availablePlans = new();
    private IReadOnlyDictionary<Guid, int> _planScores = new Dictionary<Guid, int>();
    private DateTime _lastPlansRefreshUtc;
    private volatile RuleResolution? _lastResolution;

    // Carries the already-fetched plan list along so subscribers (the tray icon) don't need
    // a second powercfg call just to look up the new plan's name.
    public event Action<Guid, IReadOnlyList<PowerPlan>>? ActivePlanChanged;

    public ProcessMonitorService(
        PowerPlanService powerPlanService,
        FileLogger logger,
        AppConfig initialConfig,
        TimeSpan pollInterval,
        TimeSpan? plansRefreshInterval = null,
        Func<DateTime>? utcNowProvider = null,
        GpuPowerService? gpuPowerService = null)
    {
        _powerPlanService = powerPlanService;
        _logger = logger;
        _config = initialConfig;
        _plansRefreshInterval = plansRefreshInterval ?? TimeSpan.FromDays(1);
        _utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
        _gpuPowerService = gpuPowerService;

        RefreshAvailablePlans();

        _timer = new System.Timers.Timer(pollInterval.TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += (_, _) => Tick();
    }

    // Scores of the plans in the cached plan list; the tray uses them to pick automatic icons.
    public IReadOnlyDictionary<Guid, int> PlanScores => _planScores;

    // Lines for the tray menu naming the rule(s) behind the active plan.
    public IReadOnlyList<string> DescribeActiveRules() =>
        _lastResolution?.Describe(PlanNameFor) ?? new[] { "Detecting..." };

    // The preset currently applied to one GPU, whether the rules or the tray chose it. Guid.Empty if none yet.
    public Guid AppliedGpuPresetIdFor(string gpuUuid) => _appliedGpuPresets.GetValueOrDefault(gpuUuid);

    private string PlanNameFor(Guid planGuid) =>
        _availablePlans.FirstOrDefault(p => p.Guid == planGuid)?.Name ?? "(deleted plan)";

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

    // Same idea for the GPUs: a rule's high limit isn't left in place once nothing is watching the rules.
    // Each GPU returns to its saved default, or to its original preset when it has none. Each GPU is
    // handled separately, so one failing does not stop the others.
    public void RestoreDefaultGpuPresets()
    {
        foreach (var original in _config.GpuPresets.Where(p => p.IsOriginal).ToList())
        {
            try
            {
                var presetId = _config.GpuDefaultPresetIds.TryGetValue(original.GpuUuid, out var chosen)
                    && _config.GpuPresets.Any(p => p.Id == chosen && p.GpuUuid == original.GpuUuid)
                    ? chosen
                    : original.Id;
                ApplyGpuPreset(presetId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Restoring the default GPU preset on exit failed", ex);
            }
        }
    }

    // Applies a preset now (tray menu). It stays until the rules pick a different preset for that GPU.
    public void ApplyGpuPreset(Guid presetId)
    {
        if (_gpuPowerService is null)
        {
            return;
        }

        var preset = _config.GpuPresets.FirstOrDefault(p => p.Id == presetId);
        if (preset is null)
        {
            return;
        }

        _gpuPowerService.Apply(presetId);
        _appliedGpuPresets[preset.GpuUuid] = presetId;
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

            var resolution = RuleResolver.Resolve(
                runningExeNames,
                _config.Rules,
                availableGuids,
                _config.DefaultPlanGuid,
                _planScores);
            _lastResolution = resolution;

            if (resolution.PlanGuid != Guid.Empty && resolution.PlanGuid != _lastAppliedPlanGuid)
            {
                _powerPlanService.SetActivePlan(resolution.PlanGuid);
                _lastAppliedPlanGuid = resolution.PlanGuid;
                ActivePlanChanged?.Invoke(resolution.PlanGuid, availablePlans);
            }

            ApplyGpuRules(runningExeNames);
        }
        catch (Exception ex)
        {
            _logger.LogError("Process monitor tick failed", ex);
        }
    }

    // Per GPU, only acts when the rules' choice changes, so a preset picked from the tray is kept until then.
    private void ApplyGpuRules(IReadOnlyList<string> runningExeNames)
    {
        if (_gpuPowerService is null)
        {
            return;
        }

        var choices = GpuRuleResolver.Resolve(runningExeNames, _config.GpuRules, _config.GpuPresets, _config.GpuDefaultPresetIds);
        foreach (var (gpuUuid, presetId) in choices)
        {
            if (_lastResolvedGpuPresets.GetValueOrDefault(gpuUuid) == presetId)
            {
                continue;
            }

            // Recorded before applying, so a failing task is logged once instead of on every tick.
            _lastResolvedGpuPresets[gpuUuid] = presetId;
            try
            {
                ApplyGpuPreset(presetId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Applying the GPU power limit failed", ex);
            }
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
