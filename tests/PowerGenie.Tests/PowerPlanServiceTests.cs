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
}
