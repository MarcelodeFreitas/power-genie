using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

// The Windows scheduled tasks that set the GPU limit. Each preset has its own task with that
// preset's watts fixed in its command, so applying a preset never needs administrator rights.
public static class GpuTasks
{
    public const string Prefix = "PowerGenie GPU ";

    public static string TaskName(Guid presetId) => Prefix + presetId.ToString("D");

    // The script the elevated PowerShell runs. Only values built here go into it (GUIDs, clamped
    // integers, the nvidia-smi path and the user name), each single-quoted. Preset names never reach it.
    internal static string BuildRegisterScript(
        IReadOnlyList<GpuPreset> presets, GpuCardInfo card, string nvidiaSmiPath, string userName)
    {
        var lines = new List<string>
        {
            "$ErrorActionPreference = 'Stop'",
            $"$principal = New-ScheduledTaskPrincipal -UserId {Quote(userName)} -LogonType Interactive -RunLevel Highest",
            "$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries",
            "$keep = @()",
        };

        foreach (var preset in presets)
        {
            var taskName = TaskName(preset.Id);
            var watts = GpuPresetValidator.Clamp(preset.Watts, card);
            lines.Add($"$keep += {Quote(taskName)}");
            lines.Add($"$action = New-ScheduledTaskAction -Execute {Quote(nvidiaSmiPath)} -Argument {Quote($"-pl {watts}")}");
            lines.Add($"Register-ScheduledTask -TaskName {Quote(taskName)} -Action $action -Principal $principal -Settings $settings -Force | Out-Null");
        }

        // Tasks of deleted presets are removed, so an old limit can never be applied later.
        lines.Add($"Get-ScheduledTask | Where-Object {{ $_.TaskName -like '{Prefix}*' -and $keep -notcontains $_.TaskName }} | Unregister-ScheduledTask -Confirm:$false");
        return string.Join(Environment.NewLine, lines);
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
}
