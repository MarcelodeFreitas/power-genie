using System.Diagnostics;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public sealed class PowerPlanService
{
    public List<PowerPlan> GetAvailablePlans()
    {
        var output = RunPowercfg("/list");
        return PowerPlanListParser.Parse(output);
    }

    public void SetActivePlan(Guid planGuid)
    {
        RunPowercfg($"/setactive {planGuid:D}");
    }

    private static string RunPowercfg(string arguments)
    {
        var startInfo = new ProcessStartInfo("powercfg", arguments)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start powercfg.exe");

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
