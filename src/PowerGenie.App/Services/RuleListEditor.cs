using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

// A rule is identified by the process it watches (RuleResolver matches on exe name only), so two
// rules for the same exe would be contradictory. Display names and plans may repeat freely.
public static class RuleListEditor
{
    // Existing rules that share an exe name with any incoming rule. "ignore" is the rule being
    // edited, which naturally shares its own exe name and is not a conflict.
    public static List<AppRule> FindConflicts(
        IReadOnlyList<AppRule> existing, IEnumerable<AppRule> incoming, AppRule? ignore = null)
    {
        var incomingNames = new HashSet<string>(incoming.Select(r => r.ExeName), StringComparer.OrdinalIgnoreCase);
        return existing
            .Where(r => !ReferenceEquals(r, ignore) && incomingNames.Contains(r.ExeName))
            .ToList();
    }

    // New rules replace a same-exe rule in place (keeping its position) or are appended.
    public static void AddOrReplace(List<AppRule> rules, IEnumerable<AppRule> incoming)
    {
        foreach (var rule in incoming)
        {
            var index = rules.FindIndex(r => string.Equals(r.ExeName, rule.ExeName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                rules[index] = rule;
            }
            else
            {
                rules.Add(rule);
            }
        }
    }

    // The edited rule is overwritten in place; any other rule for the edited rule's new exe is
    // removed, since the user confirmed replacing it.
    public static void ReplaceEdited(List<AppRule> rules, int index, AppRule edited)
    {
        rules[index] = edited;
        rules.RemoveAll(r => !ReferenceEquals(r, edited)
            && string.Equals(r.ExeName, edited.ExeName, StringComparison.OrdinalIgnoreCase));
    }
}
