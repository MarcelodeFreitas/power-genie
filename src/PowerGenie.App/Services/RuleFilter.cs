using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public enum RuleStatusFilter { All, Active, Inactive }

// Power plan rules and GPU rules have the same search and filter, written out once per rule type
// so each section stays self-contained. The matching itself is shared in Matches below.
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
            if (!StatusMatches(rule.IsEnabled, filter))
            {
                continue;
            }

            if (trimmedQuery.Length > 0 && !Matches(rule.DisplayName, rule.ExeName, planNameLookup(rule.PlanGuid), trimmedQuery))
            {
                continue;
            }

            result.Add(rule);
        }

        return result;
    }

    public static List<GpuRule> Apply(
        IEnumerable<GpuRule> rules,
        string? query,
        RuleStatusFilter filter,
        Func<Guid, string> presetNameLookup)
    {
        var trimmedQuery = query?.Trim() ?? string.Empty;

        var result = new List<GpuRule>();
        foreach (var rule in rules)
        {
            if (!StatusMatches(rule.IsEnabled, filter))
            {
                continue;
            }

            if (trimmedQuery.Length > 0 && !Matches(rule.DisplayName, rule.ExeName, presetNameLookup(rule.PresetId), trimmedQuery))
            {
                continue;
            }

            result.Add(rule);
        }

        return result;
    }

    private static bool StatusMatches(bool isEnabled, RuleStatusFilter filter)
    {
        if (filter == RuleStatusFilter.Active)
        {
            return isEnabled;
        }

        if (filter == RuleStatusFilter.Inactive)
        {
            return !isEnabled;
        }

        return true;
    }

    private static bool Matches(string displayName, string exeName, string? targetName, string query)
    {
        return displayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || exeName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (targetName ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
