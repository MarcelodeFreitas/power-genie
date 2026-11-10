using System.IO;
using System.Text;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

// The Windows scheduled tasks that set the GPU limit. Each preset has its own task that sets one GPU
// to that preset's watts, so applying a preset never needs administrator rights. Every run appends
// one line to LogFilePath with the GPU, the watts and the result.
public static class GpuTasks
{
    public const string Prefix = "PowerGenie GPU ";

    // Bump when the task action changes, so the app re-registers the existing tasks once.
    public const int Version = 2;

    public static string TaskName(Guid presetId) => Prefix + presetId.ToString("D");

    public static string LogFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerGenie", "gpu-tasks.log");

    private static string PowerShellPath =>
        Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    // conhost --headless runs PowerShell without a console window. Running powershell.exe directly
    // shows a window for a moment before -WindowStyle Hidden takes effect.
    private static string HeadlessHostPath => Path.Combine(Environment.SystemDirectory, "conhost.exe");

    // The script the elevated PowerShell runs. Only values built here go into it (GUIDs, clamped
    // integers, paths, GPU labels, the user name and preset names), each single-quoted by Quote.
    // A preset whose GPU is not among the cards gets no task.
    internal static string BuildRegisterScript(
        IReadOnlyList<GpuPreset> presets, IReadOnlyList<GpuCardInfo> cards, string nvidiaSmiPath, string userName, string logPath)
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
            var card = cards.FirstOrDefault(c => c.Uuid == preset.GpuUuid);
            if (card is null)
            {
                continue;
            }

            var taskName = TaskName(preset.Id);
            var command = Convert.ToBase64String(Encoding.Unicode.GetBytes(BuildTaskCommand(preset, card, nvidiaSmiPath, logPath)));
            lines.Add($"$keep += {Quote(taskName)}");
            lines.Add($"$action = New-ScheduledTaskAction -Execute {Quote(HeadlessHostPath)} -Argument {Quote($"--headless {PowerShellPath} -NoProfile -EncodedCommand {command}")}");
            lines.Add($"Register-ScheduledTask -TaskName {Quote(taskName)} -Action $action -Principal $principal -Settings $settings -Force | Out-Null");
        }

        // Tasks of deleted presets are removed, so an old limit can never be applied later.
        lines.Add($"Get-ScheduledTask | Where-Object {{ $_.TaskName -like '{Prefix}*' -and $keep -notcontains $_.TaskName }} | Unregister-ScheduledTask -Confirm:$false");
        return string.Join(Environment.NewLine, lines);
    }

    // The PowerShell one task runs: sets one GPU with nvidia-smi, then logs the result. The exit code is
    // nvidia-smi's, so Task Scheduler's history shows whether this GPU was set.
    internal static string BuildTaskCommand(GpuPreset preset, GpuCardInfo card, string nvidiaSmiPath, string logPath)
    {
        var watts = GpuPresetValidator.Clamp(preset.Watts, card);
        var logDirectory = Path.GetDirectoryName(logPath) ?? string.Empty;
        var logLine = string.Concat(
            "(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ",
            $"' | GPU ' + {Quote(card.Label)} + ' | preset ' + {Quote(preset.Name)} + ",
            $"' | requested {preset.Watts} W | applied {watts} W | exit ' + $code + ' | ' + ",
            @"($out -replace '\s+', ' ').Trim()");

        return string.Join("; ",
            $"$out = (& {Quote(nvidiaSmiPath)} -i {Quote(card.Uuid)} -pl {watts} 2>&1 | Out-String)",
            "$code = $LASTEXITCODE",
            $"New-Item -ItemType Directory -Force -Path {Quote(logDirectory)} | Out-Null",
            $"Add-Content -LiteralPath {Quote(logPath)} -Value ({logLine})",
            "exit $code");
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
}
