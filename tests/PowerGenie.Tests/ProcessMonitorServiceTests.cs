using System.Diagnostics;
using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class ProcessMonitorServiceTests
{
    [Fact]
    public void GetExeName_uses_process_name_so_elevated_or_protected_processes_are_not_skipped()
    {
        // MainModule access throws for processes the current user can't inspect (elevated
        // or system processes). ProcessName never throws for any live process, so basing
        // exe-name matching on it means those processes are never silently skipped.
        var currentProcess = Process.GetCurrentProcess();

        var exeName = ProcessMonitorService.GetExeName(currentProcess);

        Assert.Equal(currentProcess.ProcessName + ".exe", exeName);
    }

    private sealed class FakePowerPlanService : PowerPlanService
    {
        private readonly Queue<List<PowerPlan>> _responses;

        public FakePowerPlanService(params List<PowerPlan>[] responses)
        {
            _responses = new Queue<List<PowerPlan>>(responses);
        }

        public int CallCount { get; private set; }

        public override List<PowerPlan> GetAvailablePlans()
        {
            CallCount++;
            return _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
        }

        public List<Guid> ActivatedPlans { get; } = new();

        public override void SetActivePlan(Guid planGuid)
        {
            // Never touch the real machine's power plan from a test; just record the request.
            ActivatedPlans.Add(planGuid);
        }

        public override int GetPerformanceScore(Guid planGuid) => 0;
    }

    // Refreshing the plan list means shelling out to powercfg, which is rarely needed (the
    // user almost never creates/deletes a Windows power plan while the app is running) — so
    // it's cached and only re-fetched once a day, or on demand via RefreshAvailablePlans
    // (wired to a "Sync power plans" button in Settings). A fake clock lets tests control
    // elapsed time without waiting a real day.
    [Fact]
    public void Tick_does_not_refetch_available_plans_before_the_refresh_interval_elapses()
    {
        var fakeService = new FakePowerPlanService(new List<PowerPlan>());
        var logger = new FileLogger(Path.Combine(Path.GetTempPath(), $"power-genie-monitor-test-{Guid.NewGuid()}.txt"));
        var config = new AppConfig { DefaultPlanGuid = Guid.Empty };
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using var monitor = new ProcessMonitorService(
            fakeService, logger, config, TimeSpan.FromMinutes(10),
            plansRefreshInterval: TimeSpan.FromDays(1),
            utcNowProvider: () => now);

        monitor.Tick();
        now = now.AddHours(23);
        monitor.Tick();

        // One call from the constructor's initial fetch; the refresh interval hasn't elapsed.
        Assert.Equal(1, fakeService.CallCount);
    }

    [Fact]
    public void Tick_refetches_available_plans_once_the_refresh_interval_elapses()
    {
        var fakeService = new FakePowerPlanService(new List<PowerPlan>());
        var logger = new FileLogger(Path.Combine(Path.GetTempPath(), $"power-genie-monitor-test-{Guid.NewGuid()}.txt"));
        var config = new AppConfig { DefaultPlanGuid = Guid.Empty };
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using var monitor = new ProcessMonitorService(
            fakeService, logger, config, TimeSpan.FromMinutes(10),
            plansRefreshInterval: TimeSpan.FromDays(1),
            utcNowProvider: () => now);

        monitor.Tick();
        now = now.AddDays(1).AddSeconds(1);
        monitor.Tick();

        Assert.Equal(2, fakeService.CallCount);
    }

    [Fact]
    public void RefreshAvailablePlans_forces_an_immediate_refetch_regardless_of_the_interval()
    {
        var fakeService = new FakePowerPlanService(new List<PowerPlan>());
        var logger = new FileLogger(Path.Combine(Path.GetTempPath(), $"power-genie-monitor-test-{Guid.NewGuid()}.txt"));
        var config = new AppConfig { DefaultPlanGuid = Guid.Empty };

        using var monitor = new ProcessMonitorService(
            fakeService, logger, config, TimeSpan.FromMinutes(10),
            plansRefreshInterval: TimeSpan.FromDays(1));

        monitor.RefreshAvailablePlans();

        Assert.Equal(2, fakeService.CallCount);
    }

    [Fact]
    public void Tick_raises_ActivePlanChanged_when_it_switches_to_a_new_plan()
    {
        var targetPlan = Guid.Parse("a1841308-3541-4fab-bc81-f71556f20b4a");
        var availablePlans = new List<PowerPlan> { new(targetPlan, "Power saver", false) };
        var fakeService = new FakePowerPlanService(availablePlans);
        var logger = new FileLogger(Path.Combine(Path.GetTempPath(), $"power-genie-monitor-test-{Guid.NewGuid()}.txt"));
        var config = new AppConfig { DefaultPlanGuid = targetPlan };

        using var monitor = new ProcessMonitorService(fakeService, logger, config, TimeSpan.FromMinutes(10));
        Guid? raisedPlanGuid = null;
        IReadOnlyList<PowerPlan>? raisedPlans = null;
        monitor.ActivePlanChanged += (guid, plans) =>
        {
            raisedPlanGuid = guid;
            raisedPlans = plans;
        };

        monitor.Tick();

        Assert.Equal(targetPlan, raisedPlanGuid);
        Assert.Equal(availablePlans, raisedPlans);
    }

    [Fact]
    public void Tick_does_not_raise_ActivePlanChanged_when_the_plan_does_not_change()
    {
        var targetPlan = Guid.Parse("a1841308-3541-4fab-bc81-f71556f20b4a");
        var availablePlans = new List<PowerPlan> { new(targetPlan, "Power saver", false) };
        var fakeService = new FakePowerPlanService(availablePlans, availablePlans);
        var logger = new FileLogger(Path.Combine(Path.GetTempPath(), $"power-genie-monitor-test-{Guid.NewGuid()}.txt"));
        var config = new AppConfig { DefaultPlanGuid = targetPlan };

        using var monitor = new ProcessMonitorService(fakeService, logger, config, TimeSpan.FromMinutes(10));
        monitor.Tick();

        var raiseCount = 0;
        monitor.ActivePlanChanged += (_, _) => raiseCount++;
        monitor.Tick();

        Assert.Equal(0, raiseCount);
    }

    [Fact]
    public void RestoreDefaultPlan_activates_the_default_plan()
    {
        var defaultGuid = Guid.NewGuid();
        var fake = new FakePowerPlanService(new List<PowerPlan> { new(defaultGuid, "Balanced", false) });
        var logger = new FileLogger(Path.Combine(Path.GetTempPath(), $"power-genie-monitor-test-{Guid.NewGuid()}.txt"));
        using var monitor = new ProcessMonitorService(
            fake, logger, new AppConfig { DefaultPlanGuid = defaultGuid }, TimeSpan.FromMinutes(10));

        monitor.RestoreDefaultPlan();

        Assert.Equal(new[] { defaultGuid }, fake.ActivatedPlans);
    }

    [Fact]
    public void RestoreDefaultPlan_does_nothing_when_the_default_plan_no_longer_exists()
    {
        var fake = new FakePowerPlanService(new List<PowerPlan> { new(Guid.NewGuid(), "Balanced", false) });
        var logger = new FileLogger(Path.Combine(Path.GetTempPath(), $"power-genie-monitor-test-{Guid.NewGuid()}.txt"));
        using var monitor = new ProcessMonitorService(
            fake, logger, new AppConfig { DefaultPlanGuid = Guid.NewGuid() }, TimeSpan.FromMinutes(10));

        monitor.RestoreDefaultPlan();

        Assert.Empty(fake.ActivatedPlans);
    }
}
