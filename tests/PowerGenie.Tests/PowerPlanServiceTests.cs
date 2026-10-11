using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class PowerPlanServiceTests
{
    [Fact]
    public void GetAvailablePlans_returns_the_real_schemes_configured_on_this_machine()
    {
        var service = new PowerPlanService();

        var plans = service.GetAvailablePlans();

        Assert.NotEmpty(plans);
        Assert.Contains(plans, p => p.IsActive);
    }

    [Fact]
    public void SetActivePlan_throws_when_powercfg_reports_a_failure()
    {
        // A random GUID matches no real scheme, so powercfg exits non-zero and this
        // never changes the machine's actual active plan.
        var service = new PowerPlanService();

        var ex = Assert.Throws<InvalidOperationException>(() => service.SetActivePlan(Guid.NewGuid()));
        Assert.Contains("powercfg", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Records every powercfg invocation instead of running it, and can fail chosen
    // sub-commands, so DuplicatePlan's logic is tested without touching real power plans.
    private sealed class RecordingPowerPlanService : PowerPlanService
    {
        public List<string[]> Calls { get; } = new();
        public string? FailingCommand { get; init; }

        protected override string RunPowercfg(params string[] arguments)
        {
            Calls.Add(arguments);
            if (arguments[0] == FailingCommand)
            {
                throw new InvalidOperationException($"powercfg {arguments[0]} failed (exit code 1)");
            }

            return string.Empty;
        }
    }

    [Fact]
    public void DuplicatePlan_duplicates_the_source_into_a_new_guid_then_renames_it()
    {
        var service = new RecordingPowerPlanService();
        var source = Guid.NewGuid();

        var plan = service.DuplicatePlan(source, "  Gaming  ");

        Assert.Equal("Gaming", plan.Name);
        Assert.False(plan.IsActive);
        Assert.NotEqual(source, plan.Guid);
        Assert.Equal(2, service.Calls.Count);
        Assert.Equal(new[] { "/duplicatescheme", source.ToString("D"), plan.Guid.ToString("D") }, service.Calls[0]);
        Assert.Equal(new[] { "/changename", plan.Guid.ToString("D"), "Gaming" }, service.Calls[1]);
    }

    [Fact]
    public void DuplicatePlan_passes_a_hostile_name_as_one_single_argument()
    {
        var service = new RecordingPowerPlanService();
        const string hostile = "x\" /delete 381b4222-f694-41f0-9685-ff5bb260df2e & calc";

        service.DuplicatePlan(Guid.NewGuid(), hostile);

        var rename = service.Calls[1];
        Assert.Equal(3, rename.Length);
        Assert.Equal(hostile, rename[2]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    [InlineData("tab\tname")]
    public void DuplicatePlan_rejects_invalid_names_without_calling_powercfg(string? name)
    {
        var service = new RecordingPowerPlanService();

        Assert.Throws<ArgumentException>(() => service.DuplicatePlan(Guid.NewGuid(), name!));
        Assert.Empty(service.Calls);
    }

    [Fact]
    public void DuplicatePlan_rejects_names_longer_than_the_limit()
    {
        var service = new RecordingPowerPlanService();

        Assert.Throws<ArgumentException>(() =>
            service.DuplicatePlan(Guid.NewGuid(), new string('a', PowerPlanService.MaxPlanNameLength + 1)));
        Assert.Empty(service.Calls);

        service.DuplicatePlan(Guid.NewGuid(), new string('a', PowerPlanService.MaxPlanNameLength));
        Assert.Equal(2, service.Calls.Count);
    }

    [Fact]
    public void DuplicatePlan_propagates_a_duplicate_failure_and_does_not_rename_or_delete()
    {
        var service = new RecordingPowerPlanService { FailingCommand = "/duplicatescheme" };

        Assert.Throws<InvalidOperationException>(() => service.DuplicatePlan(Guid.NewGuid(), "Gaming"));
        Assert.Single(service.Calls);
    }

    [Fact]
    public void DuplicatePlan_deletes_the_unnamed_copy_when_renaming_fails()
    {
        var service = new RecordingPowerPlanService { FailingCommand = "/changename" };

        Assert.Throws<InvalidOperationException>(() => service.DuplicatePlan(Guid.NewGuid(), "Gaming"));

        Assert.Equal(3, service.Calls.Count);
        Assert.Equal("/delete", service.Calls[2][0]);
        Assert.Equal(service.Calls[0][2], service.Calls[2][1]);
    }

    [Fact]
    public void DuplicatePlan_throws_when_the_source_plan_does_not_exist()
    {
        // A random GUID matches no real scheme, so powercfg fails before creating anything.
        var service = new PowerPlanService();

        var ex = Assert.Throws<InvalidOperationException>(() => service.DuplicatePlan(Guid.NewGuid(), "Never created"));
        Assert.Contains("powercfg", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
