using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class InstalledAppRegistryParserTests : IDisposable
{
    private readonly string _tempExePath = Path.Combine(Path.GetTempPath(), $"power-genie-fake-app-{Guid.NewGuid()}.exe");

    public InstalledAppRegistryParserTests()
    {
        File.WriteAllText(_tempExePath, "not a real exe, just needs to exist");
    }

    [Fact]
    public void Resolves_an_exe_path_with_a_trailing_icon_index()
    {
        var displayIcon = $"{_tempExePath},0";

        var resolved = InstalledAppRegistryParser.TryResolveExePath(displayIcon);

        Assert.Equal(_tempExePath, resolved);
    }

    [Fact]
    public void Resolves_an_exe_path_with_no_icon_index_suffix()
    {
        var resolved = InstalledAppRegistryParser.TryResolveExePath(_tempExePath);

        Assert.Equal(_tempExePath, resolved);
    }

    [Fact]
    public void Returns_null_when_displayIcon_is_missing()
    {
        Assert.Null(InstalledAppRegistryParser.TryResolveExePath(null));
        Assert.Null(InstalledAppRegistryParser.TryResolveExePath(""));
    }

    [Fact]
    public void Returns_null_when_the_resolved_path_is_not_an_exe()
    {
        var resolved = InstalledAppRegistryParser.TryResolveExePath(@"C:\some\icon.ico,0");

        Assert.Null(resolved);
    }

    [Fact]
    public void Returns_null_when_the_exe_does_not_actually_exist()
    {
        var resolved = InstalledAppRegistryParser.TryResolveExePath(@"C:\this\does\not\exist.exe,0");

        Assert.Null(resolved);
    }

    public void Dispose()
    {
        if (File.Exists(_tempExePath))
        {
            File.Delete(_tempExePath);
        }
    }
}
