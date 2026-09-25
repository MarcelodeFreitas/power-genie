using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class RuleResolver
{
    public static Guid ResolveActivePlan(
        IReadOnlyList<string> runningExeNames,
        IReadOnlyList<AppRule> rules,
        IReadOnlySet<Guid> availablePlanGuids,
        Guid defaultPlanGuid)
    {
        var runningSet = new HashSet<string>(runningExeNames, StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            if (!availablePlanGuids.Contains(rule.PlanGuid))
            {
                continue;
            }

            if (runningSet.Contains(rule.ExeName))
            {
                return rule.PlanGuid;
            }
        }

        // Guid.Empty is the sentinel ProcessMonitorService.Tick treats as "don't touch the
        // active plan" - returning it for a stale/deleted default avoids retrying a doomed
        // powercfg call (and logging a failure) on every single tick forever.
        return availablePlanGuids.Contains(defaultPlanGuid) ? defaultPlanGuid : Guid.Empty;
    }
}
