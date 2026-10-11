using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class RuleResolver
{
    public static Guid ResolveActivePlan(
        IReadOnlyList<string> runningExeNames,
        IReadOnlyList<AppRule> rules,
        IReadOnlySet<Guid> availablePlanGuids,
        Guid defaultPlanGuid,
        IReadOnlyDictionary<Guid, int>? planPerformanceScores = null)
    {
        var runningSet = new HashSet<string>(runningExeNames, StringComparer.OrdinalIgnoreCase);

        // When several running apps have rules, the most performance-oriented plan wins so a
        // demanding app is never throttled by a lighter app that happens to be listed first.
        // Equal scores (or no scores supplied) fall back to rule order: first rule wins.
        Guid? best = null;
        var bestScore = int.MinValue;

        foreach (var rule in rules)
        {
            if (!availablePlanGuids.Contains(rule.PlanGuid) || !runningSet.Contains(rule.ExeName))
            {
                continue;
            }

            var score = 0;
            planPerformanceScores?.TryGetValue(rule.PlanGuid, out score);
            if (best is null || score > bestScore)
            {
                best = rule.PlanGuid;
                bestScore = score;
            }
        }

        if (best is { } winner)
        {
            return winner;
        }

        // Guid.Empty is the sentinel ProcessMonitorService.Tick treats as "don't touch the
        // active plan" - returning it for a stale/deleted default avoids retrying a doomed
        // powercfg call (and logging a failure) on every single tick forever.
        return availablePlanGuids.Contains(defaultPlanGuid) ? defaultPlanGuid : Guid.Empty;
    }
}
