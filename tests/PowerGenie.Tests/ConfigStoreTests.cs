using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _tempFilePath =
        Path.Combine(Path.GetTempPath(), $"power-genie-test-{Guid.NewGuid()}.json");

    [Fact]
    public void Load_returns_default_config_when_file_does_not_exist()
    {
        var store = new ConfigStore(_tempFilePath);

        var config = store.Load();

        Assert.Equal(Guid.Empty, config.DefaultPlanGuid);
        Assert.Empty(config.Rules);
        Assert.True(config.StartWithWindows);
    }

    [Fact]
    public void Save_then_load_round_trips_all_fields()
    {
        var store = new ConfigStore(_tempFilePath);
        var original = new AppConfig
        {
            DefaultPlanGuid = Guid.Parse("a1841308-3541-4fab-bc81-f71556f20b4a"),
            StartWithWindows = false,
            Rules = new List<AppRule>
            {
                new()
                {
                    ExeName = "bambu-studio.exe",
                    DisplayName = "Bambu Studio",
                    PlanGuid = Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c")
                }
            },
            PlanColors = new Dictionary<Guid, string>
            {
                [Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c")] = "#C62828"
            }
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(original.DefaultPlanGuid, loaded.DefaultPlanGuid);
        Assert.False(loaded.StartWithWindows);
        Assert.Single(loaded.Rules);
        Assert.Equal("bambu-studio.exe", loaded.Rules[0].ExeName);
        Assert.Equal("#C62828", loaded.PlanColors[Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c")]);
    }

    [Fact]
    public void Load_returns_default_config_and_does_not_throw_when_the_file_is_malformed()
    {
        File.WriteAllText(_tempFilePath, "{ this is not valid json ][");
        var store = new ConfigStore(_tempFilePath);

        var config = store.Load();

        Assert.Equal(Guid.Empty, config.DefaultPlanGuid);
        Assert.Empty(config.Rules);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }

        var badFilePath = _tempFilePath + ".bad";
        if (File.Exists(badFilePath))
        {
            File.Delete(badFilePath);
        }
    }
}
