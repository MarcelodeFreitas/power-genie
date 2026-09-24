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
}
