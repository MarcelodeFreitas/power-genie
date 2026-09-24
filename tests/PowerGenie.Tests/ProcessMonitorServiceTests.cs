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
}
