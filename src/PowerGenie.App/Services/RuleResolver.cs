using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

// Winner is the rule whose plan is applied; Matches lists every enabled, running rule in rule order.
public sealed record RuleResolution(Guid PlanGuid, AppRule? Winner, IReadOnlyList<AppRule> Matches)
{
    public IReadOnlyList<string> Describe(Func<Guid, string> planName)
    {
        if (Matches.Count == 0)
        {
            return new[] { "No rule active - default plan" };
        }

        return Matches
            .Select(rule => (rule == Winner ? "★ " : "   ") + $"{rule.DisplayName} → {planName(rule.PlanGuid)}")
            .ToList();
    }
}

public static class RuleResolver
{
    public static Guid ResolveActivePlan(
        IReadOnlyList<string> runningExeNames,
        IReadOnlyList<AppRule> rules,
        IReadOnlySet<Guid> availablePlanGuids,
        Guid defaultPlanGuid,
        IReadOnlyDictionary<Guid, int>? planPerformanceScores = null)
    {
        return Resolve(runningExeNames, rules, availablePlanGuids, defaultPlanGuid, planPerformanceScores).PlanGuid;
    }

    public static RuleResolution Resolve(
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
        var matches = new List<AppRule>();
        AppRule? winner = null;
        var bestScore = int.MinValue;

        foreach (var rule in rules)
        {
            if (!rule.IsEnabled || !availablePlanGuids.Contains(rule.PlanGuid) || !runningSet.Contains(rule.ExeName))
            {
                continue;
            }

            matches.Add(rule);

            var score = 0;
            planPerformanceScores?.TryGetValue(rule.PlanGuid, out score);
            if (winner is null || score > bestScore)
            {
                winner = rule;
                bestScore = score;
            }
        }

        if (winner is not null)
        {
            return new RuleResolution(winner.PlanGuid, winner, matches);
        }

        // Guid.Empty is the sentinel ProcessMonitorService.Tick treats as "don't touch the
        // active plan" - returning it for a stale/deleted default avoids retrying a doomed
        // powercfg call (and logging a failure) on every single tick forever.
        var fallback = availablePlanGuids.Contains(defaultPlanGuid) ? defaultPlanGuid : Guid.Empty;
        return new RuleResolution(fallback, null, matches);
    }
}
