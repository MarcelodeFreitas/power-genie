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

        public override void SetActivePlan(Guid planGuid)
        {
            // No-op: never touch the real machine's power plan from a test.
        }
    }

    [Fact]
    public void Tick_refetches_available_plans_on_every_run_instead_of_caching_them_once()
    {
        var fakeService = new FakePowerPlanService(new List<PowerPlan>(), new List<PowerPlan>());
        var logger = new FileLogger(Path.Combine(Path.GetTempPath(), $"power-genie-monitor-test-{Guid.NewGuid()}.txt"));
        var config = new AppConfig { DefaultPlanGuid = Guid.Empty };

        using var monitor = new ProcessMonitorService(fakeService, logger, config, TimeSpan.FromMinutes(10));

        monitor.Tick();
        monitor.Tick();

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
}
