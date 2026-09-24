using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class InstalledAppsReaderTests
{
    [Fact]
    public void GetInstalledApps_returns_real_apps_installed_on_this_machine_with_resolvable_exe_paths()
    {
        var apps = InstalledAppsReader.GetInstalledApps();

        Assert.NotEmpty(apps);
        Assert.All(apps, app => Assert.True(File.Exists(app.ExePath)));
    }
}
