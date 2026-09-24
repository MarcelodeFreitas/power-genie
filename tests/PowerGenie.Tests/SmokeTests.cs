namespace PowerGenie.Tests;

public class SmokeTests
{
    [Fact]
    public void Test_project_can_see_the_app_project()
    {
        var manager = new PowerGenie.App.TrayIconManager(() => { });
        manager.Dispose();
    }
}
