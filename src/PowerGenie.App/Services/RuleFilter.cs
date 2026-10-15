using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public enum RuleStatusFilter { All, Active, Inactive }

public static class RuleFilter
{
    public static List<AppRule> Apply(
        IEnumerable<AppRule> rules,
        string? query,
        RuleStatusFilter filter,
        Func<Guid, string> planNameLookup)
    {
        var trimmedQuery = query?.Trim() ?? string.Empty;

        var result = new List<AppRule>();
        foreach (var rule in rules)
        {
            if (filter == RuleStatusFilter.Active && !rule.IsEnabled)
            {
                continue;
            }

            if (filter == RuleStatusFilter.Inactive && rule.IsEnabled)
            {
                continue;
            }

            if (trimmedQuery.Length > 0 && !Matches(rule, trimmedQuery, planNameLookup))
            {
                continue;
            }

            result.Add(rule);
        }

        return result;
    }

    private static bool Matches(AppRule rule, string query, Func<Guid, string> planNameLookup)
    {
        return rule.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || rule.ExeName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (planNameLookup(rule.PlanGuid) ?? string.Empty)
                .Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
