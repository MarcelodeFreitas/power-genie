using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

// Reads and sets the NVIDIA GPU power limit. Reading calls nvidia-smi directly. Setting runs the
// scheduled tasks registered once with administrator rights (see GpuTasks), so the app never runs elevated.
public class GpuPowerService
{
    // Resolved once, not searched on PATH, so a same-named exe earlier on PATH can't run instead.
    private static readonly string SchtasksPath = Path.Combine(Environment.SystemDirectory, "schtasks.exe");
    private static readonly string PowerShellPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    private const string QueryArgument = "--query-gpu=name,power.limit,power.default_limit,power.min_limit,power.max_limit";
    private const string FormatArgument = "--format=csv,noheader,nounits";

    // Returned by Win32 when the user clicks "No" on the UAC prompt.
    private const int ErrorCancelled = 1223;

    private string? _nvidiaSmiPath;

    // Null when there is no NVIDIA GPU, so the whole GPU part of the app stays hidden.
    public virtual GpuCardInfo? Detect()
    {
        _nvidiaSmiPath = FindNvidiaSmi();
        if (_nvidiaSmiPath is null)
        {
            return null;
        }

        try
        {
            return NvidiaSmiParser.Parse(RunTool(_nvidiaSmiPath, QueryArgument, FormatArgument));
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }

    // Starts the preset's task. Returns once the task has started; the task runs nvidia-smi.
    public virtual void Apply(Guid presetId) => RunTool(SchtasksPath, "/run", "/tn", GpuTasks.TaskName(presetId));

    // True when every preset has its task. Needs no administrator rights.
    public virtual bool AreRegistered(IEnumerable<GpuPreset> presets)
    {
        try
        {
            foreach (var preset in presets)
            {
                RunTool(SchtasksPath, "/query", "/tn", GpuTasks.TaskName(preset.Id));
            }

            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    // Creates or updates one task per preset and removes the tasks of deleted presets. Shows one UAC
    // prompt. Returns false if the user declines it; throws if the elevated script fails.
    public virtual bool Register(IReadOnlyList<GpuPreset> presets, GpuCardInfo card)
    {
        if (_nvidiaSmiPath is null)
        {
            throw new InvalidOperationException("nvidia-smi was not found.");
        }

        var script = GpuTasks.BuildRegisterScript(presets, card, _nvidiaSmiPath, WindowsIdentity.GetCurrent().Name);
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var startInfo = new ProcessStartInfo(PowerShellPath)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encoded);

        try
        {
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start PowerShell.");
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Setting up the GPU presets failed (exit code {process.ExitCode}).");
            }

            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
    }

    private static string? FindNvidiaSmi() => new[]
    {
        Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe"),
    }.FirstOrDefault(File.Exists);

    // Each argument is passed separately (ArgumentList), so nothing can be read as extra arguments.
    protected virtual string RunTool(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {Path.GetFileName(fileName)}");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(fileName)} {string.Join(' ', arguments)} failed (exit code {process.ExitCode}): {error}{output}".Trim());
        }

        return output;
    }
}
