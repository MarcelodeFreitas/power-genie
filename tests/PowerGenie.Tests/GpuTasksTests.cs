using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class GpuTasksTests
{
    private static readonly GpuCardInfo Card = new("RTX 3090", 290, 420, 100, 450);
    private const string SmiPath = @"C:\Windows\System32\nvidia-smi.exe";

    [Fact]
    public void Task_name_is_built_only_from_the_preset_id()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        Assert.Equal("PowerGenie GPU 11111111-2222-3333-4444-555555555555", GpuTasks.TaskName(id));
    }

    [Fact]
    public void Script_fixes_the_clamped_watts_into_each_task()
    {
        var preset = new GpuPreset { Id = Guid.NewGuid(), Name = "Max", Watts = 999 };

        var script = GpuTasks.BuildRegisterScript(new[] { preset }, Card, SmiPath, @"PC\pc");

        Assert.Contains("-Argument '-pl 450'", script);
        Assert.Contains($"-TaskName '{GpuTasks.TaskName(preset.Id)}'", script);
        Assert.Contains($"-Execute '{SmiPath}'", script);
    }

    [Fact]
    public void Script_quotes_the_user_name_so_it_cannot_end_the_string()
    {
        var script = GpuTasks.BuildRegisterScript(Array.Empty<GpuPreset>(), Card, SmiPath, "O'Brien");

        Assert.Contains("-UserId 'O''Brien'", script);
    }

    [Fact]
    public void Script_removes_tasks_of_presets_that_no_longer_exist()
    {
        var script = GpuTasks.BuildRegisterScript(Array.Empty<GpuPreset>(), Card, SmiPath, "pc");

        Assert.Contains("Unregister-ScheduledTask", script);
        Assert.DoesNotContain("Register-ScheduledTask -TaskName", script);
    }
}
