using System.Text.RegularExpressions;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class PowerPlanListParser
{
    private static readonly Regex LineRegex = new(
        @"Power Scheme GUID:\s*(?<guid>[0-9a-fA-F-]{36})\s*\((?<name>.+?)\)\s*(?<active>\*)?\s*$",
        RegexOptions.Compiled);

    public static List<PowerPlan> Parse(string powercfgListOutput)
    {
        var plans = new List<PowerPlan>();

        foreach (var rawLine in powercfgListOutput.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var match = LineRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            plans.Add(new PowerPlan(
                Guid.Parse(match.Groups["guid"].Value),
                match.Groups["name"].Value.Trim(),
                match.Groups["active"].Success));
        }

        return plans;
    }
}
