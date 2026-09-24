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

        return defaultPlanGuid;
    }
}
