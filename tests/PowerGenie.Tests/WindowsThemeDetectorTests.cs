using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class WindowsThemeDetectorTests
{
    [Fact]
    public void IsDarkModeEnabled_reads_the_real_registry_without_throwing()
    {
        var exception = Record.Exception(() => WindowsThemeDetector.IsDarkModeEnabled());

        Assert.Null(exception);
    }
}
