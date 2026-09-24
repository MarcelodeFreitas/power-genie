using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class FileLoggerTests : IDisposable
{
    private readonly string _tempFilePath =
        Path.Combine(Path.GetTempPath(), $"power-genie-log-{Guid.NewGuid()}.txt");

    [Fact]
    public void LogError_appends_a_line_containing_the_message()
    {
        var logger = new FileLogger(_tempFilePath);

        logger.LogError("something failed");

        var contents = File.ReadAllText(_tempFilePath);
        Assert.Contains("something failed", contents);
        Assert.Contains("[ERROR]", contents);
    }

    [Fact]
    public void LogError_appends_multiple_entries_without_overwriting()
    {
        var logger = new FileLogger(_tempFilePath);

        logger.LogError("first");
        logger.LogError("second");

        var lines = File.ReadAllLines(_tempFilePath);
        Assert.Equal(2, lines.Length);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }
}
