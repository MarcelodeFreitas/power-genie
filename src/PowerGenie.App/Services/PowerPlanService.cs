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
        RunPowercfg("/setactive", planGuid.ToString("D"));
    }

    // Processor power management subgroup and its max/min processor state settings. A plan's
    // "performance" is ranked by these (see GetPerformanceScore) because they are what actually
    // differs between Power saver / Balanced / High performance / Ultimate and vendor plans.
    private const string ProcessorSubgroup = "54533251-82be-4824-96c1-47b60b740d00";
    private const string MaxProcessorState = "bc5038f7-23e0-4960-96da-33abaf5935ec";
    private const string MinProcessorState = "893dee8e-2bef-41e0-89c6-b55d0929964c";

    // Higher means a more performance-oriented plan: maximum processor state (AC) dominates,
    // minimum processor state (AC) breaks ties. Returns 0 when a plan can't be queried, so an
    // unreadable plan ranks as the least performant instead of failing the whole refresh.
    public virtual int GetPerformanceScore(Guid planGuid)
    {
        try
        {
            var max = PowerPlanSettingParser.ParseAcIndex(
                RunPowercfg("/query", planGuid.ToString("D"), ProcessorSubgroup, MaxProcessorState));
            var min = PowerPlanSettingParser.ParseAcIndex(
                RunPowercfg("/query", planGuid.ToString("D"), ProcessorSubgroup, MinProcessorState));
            return (max ?? 0) * 1000 + (min ?? 0);
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    // Each argument is passed separately (ProcessStartInfo.ArgumentList) so Windows quotes
    // and escapes it for us. A plan name containing quotes, ampersands or spaces can never
    // be interpreted as extra arguments or shell syntax.
    protected virtual string RunPowercfg(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(PowercfgPath)
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
            ?? throw new InvalidOperationException("Failed to start powercfg.exe");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"powercfg {string.Join(' ', arguments)} failed (exit code {process.ExitCode}): {error}{output}".Trim());
        }

        return output;
    }
}
