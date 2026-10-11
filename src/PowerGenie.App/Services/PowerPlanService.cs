using System.Diagnostics;
using System.IO;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public class PowerPlanService
{
    // Windows itself accepts far longer names, but a name that long makes the plan list
    // unreadable in Settings and in Windows' own Power Options, so cap it.
    public const int MaxPlanNameLength = 64;

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

    // Returns null when the name is acceptable, otherwise a message fit to show to the user.
    // Control characters are rejected because a newline in a plan name would split one
    // "powercfg /list" entry across two lines, which PowerPlanListParser could not read back.
    public static string? ValidatePlanName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "Enter a name for the new power plan.";
        }

        if (trimmed.Length > MaxPlanNameLength)
        {
            return $"The name can be at most {MaxPlanNameLength} characters long.";
        }

        if (trimmed.Any(char.IsControl))
        {
            return "The name can't contain control characters such as line breaks or tabs.";
        }

        return null;
    }

    // Creates a new power plan as a copy of an existing one (all of its settings included)
    // under the given name, equivalent to "powercfg /duplicatescheme" + "powercfg /changename".
    // Needs no administrator rights. Throws ArgumentException for an invalid name and
    // InvalidOperationException when powercfg fails (e.g. the source plan no longer exists).
    public virtual PowerPlan DuplicatePlan(Guid sourceGuid, string newName)
    {
        var validationError = ValidatePlanName(newName);
        if (validationError is not null)
        {
            throw new ArgumentException(validationError, nameof(newName));
        }

        var name = newName.Trim();

        // Choosing the new plan's GUID ourselves, instead of parsing it out of powercfg's
        // (localized) output, means there is nothing to parse and nothing to get wrong.
        var newGuid = Guid.NewGuid();
        RunPowercfg("/duplicatescheme", sourceGuid.ToString("D"), newGuid.ToString("D"));

        try
        {
            RunPowercfg("/changename", newGuid.ToString("D"), name);
        }
        catch
        {
            // Don't leave an unnamed copy of the source plan lying around in Windows.
            TryDeletePlan(newGuid);
            throw;
        }

        return new PowerPlan(newGuid, name, false);
    }

    private void TryDeletePlan(Guid planGuid)
    {
        try
        {
            RunPowercfg("/delete", planGuid.ToString("D"));
        }
        catch (InvalidOperationException)
        {
            // Best-effort cleanup; the original failure is the one worth reporting.
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
