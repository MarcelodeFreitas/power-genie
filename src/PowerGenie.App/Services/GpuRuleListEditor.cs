using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

// The same rules as RuleListEditor, for GPU rules: one rule per exe, since the process is what is matched.
public static class GpuRuleListEditor
{
    // Existing rules that share an exe name with any incoming rule. "ignore" is the rule being
    // edited, which naturally shares its own exe name and is not a conflict.
    public static List<GpuRule> FindConflicts(
        IReadOnlyList<GpuRule> existing, IEnumerable<GpuRule> incoming, GpuRule? ignore = null)
    {
        var incomingNames = new HashSet<string>(incoming.Select(r => r.ExeName), StringComparer.OrdinalIgnoreCase);
        return existing
            .Where(r => !ReferenceEquals(r, ignore) && incomingNames.Contains(r.ExeName))
            .ToList();
    }

    // New rules replace a same-exe rule in place (keeping its position) or are appended.
    public static void AddOrReplace(List<GpuRule> rules, IEnumerable<GpuRule> incoming)
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
    public static void ReplaceEdited(List<GpuRule> rules, int index, GpuRule edited)
    {
        rules[index] = edited;
        rules.RemoveAll(r => !ReferenceEquals(r, edited)
            && string.Equals(r.ExeName, edited.ExeName, StringComparison.OrdinalIgnoreCase));
    }
}
