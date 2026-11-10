using System.Text;
using System.Text.RegularExpressions;
using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class GpuTasksTests
{
    private static readonly GpuCardInfo Card = new(0, "GPU-aaa", "00000000:04:00.0", "NVIDIA GeForce RTX 3090", 290, 420, 100, 450);
    private static readonly List<GpuCardInfo> Cards = new() { Card };
    private const string SmiPath = @"C:\Windows\System32\nvidia-smi.exe";
    private const string LogPath = @"C:\Users\pc\AppData\Local\PowerGenie\gpu-tasks.log";

    // The task's PowerShell is passed as a base64 -EncodedCommand; this returns the decoded command.
    private static string DecodedCommand(string script)
    {
        var encoded = Regex.Match(script, "-EncodedCommand ([A-Za-z0-9+/=]+)").Groups[1].Value;
        return Encoding.Unicode.GetString(Convert.FromBase64String(encoded));
    }

    [Fact]
    public void Task_name_is_built_only_from_the_preset_id()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        Assert.Equal("PowerGenie GPU 11111111-2222-3333-4444-555555555555", GpuTasks.TaskName(id));
    }

    [Fact]
    public void Task_sets_only_its_gpu_to_the_clamped_watts()
    {
        var preset = new GpuPreset { Id = Guid.NewGuid(), Name = "Max", Watts = 999, GpuUuid = Card.Uuid };

        var script = GpuTasks.BuildRegisterScript(new[] { preset }, Cards, SmiPath, @"PC\pc", LogPath);

        Assert.Contains($"-TaskName '{GpuTasks.TaskName(preset.Id)}'", script);
        Assert.Contains("conhost.exe' -Argument '--headless ", script);
        Assert.Contains("-i 'GPU-aaa' -pl 450", DecodedCommand(script));
    }

    [Fact]
    public void Task_logs_the_gpu_its_requested_watts_and_the_applied_watts()
    {
        var preset = new GpuPreset { Id = Guid.NewGuid(), Name = "Max", Watts = 999, GpuUuid = Card.Uuid };

        var command = DecodedCommand(GpuTasks.BuildRegisterScript(new[] { preset }, Cards, SmiPath, "pc", LogPath));

        Assert.Contains("'RTX 3090 (PCI 04:00)'", command);
        Assert.Contains("requested 999 W", command);
        Assert.Contains("applied 450 W", command);
        Assert.Contains($"-LiteralPath '{LogPath}'", command);
    }

    [Fact]
    public void Preset_name_with_an_apostrophe_is_escaped_in_the_log_line()
    {
        var preset = new GpuPreset { Id = Guid.NewGuid(), Name = "Joe's", Watts = 300, GpuUuid = Card.Uuid };

        var command = DecodedCommand(GpuTasks.BuildRegisterScript(new[] { preset }, Cards, SmiPath, "pc", LogPath));

        Assert.Contains("'Joe''s'", command);
    }

    [Fact]
    public void Preset_whose_gpu_is_not_detected_gets_no_task()
    {
        var preset = new GpuPreset { Id = Guid.NewGuid(), Name = "Gone", Watts = 300, GpuUuid = "GPU-missing" };

        var script = GpuTasks.BuildRegisterScript(new[] { preset }, Cards, SmiPath, "pc", LogPath);

        Assert.DoesNotContain("Register-ScheduledTask -TaskName", script);
    }

    [Fact]
    public void Script_quotes_the_user_name_so_it_cannot_end_the_string()
    {
        var script = GpuTasks.BuildRegisterScript(Array.Empty<GpuPreset>(), Cards, SmiPath, "O'Brien", LogPath);

        Assert.Contains("-UserId 'O''Brien'", script);
    }

    [Fact]
    public void Script_removes_tasks_of_presets_that_no_longer_exist()
    {
        var script = GpuTasks.BuildRegisterScript(Array.Empty<GpuPreset>(), Cards, SmiPath, "pc", LogPath);

        Assert.Contains("Unregister-ScheduledTask", script);
        Assert.DoesNotContain("Register-ScheduledTask -TaskName", script);
    }
}
