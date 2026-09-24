using Microsoft.Win32;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class AutoStartManagerTests : IDisposable
{
    private readonly string _testKeyPath = $@"Software\PowerGenieTests\{Guid.NewGuid()}";

    [Fact]
    public void SetEnabled_true_writes_a_registry_value_pointing_at_the_executable()
    {
        var manager = new AutoStartManager(_testKeyPath);

        manager.SetEnabled(true, @"C:\fake\PowerGenie.exe");

        Assert.True(manager.IsEnabled());
    }

    [Fact]
    public void SetEnabled_false_removes_the_registry_value()
    {
        var manager = new AutoStartManager(_testKeyPath);
        manager.SetEnabled(true, @"C:\fake\PowerGenie.exe");

        manager.SetEnabled(false, @"C:\fake\PowerGenie.exe");

        Assert.False(manager.IsEnabled());
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_testKeyPath, throwOnMissingSubKey: false);
    }
}
