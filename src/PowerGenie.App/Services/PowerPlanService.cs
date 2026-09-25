using System.Diagnostics;
using System.IO;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public class PowerPlanService
{
    // Resolved once, not relying on PATH search order at call time: a same-named
    // "powercfg.exe" earlier on PATH would otherwise run instead of the real one.
    private static readonly string PowercfgPath = Path.Combine(Environment.SystemDirectory, "powercfg.exe");

    public virtual List<PowerPlan> GetAvailablePlans()
    {
        var output = RunPowercfg("/list");
        return PowerPlanListParser.Parse(output);
    }

    public virtual void SetActivePlan(Guid planGuid)
    {
        RunPowercfg($"/setactive {planGuid:D}");
    }

    private static string RunPowercfg(string arguments)
    {
        var startInfo = new ProcessStartInfo(PowercfgPath, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start powercfg.exe");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"powercfg {arguments} failed (exit code {process.ExitCode}): {error}{output}".Trim());
        }

        return output;
    }
}
