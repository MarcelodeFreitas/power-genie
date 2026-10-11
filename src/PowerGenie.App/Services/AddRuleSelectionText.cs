namespace PowerGenie.App.Services;

// Pure text/dedupe helpers for the Add Rule dialog's multi-select UX, kept out of the code-behind
// so the wording and counting rules can be unit-tested without spinning up WPF.
public static class AddRuleSelectionText
{
    public const string Hint = "Tip: tick several apps to add a rule for each.";

    // "hidden" counts picks that are currently filtered out of the list by the search box. They
    // stay selected (and will be added), so the summary says so instead of letting them vanish.
    // "ruleCount" is how many rules will really be created: several installed programs can share
    // one exe name (e.g. Uninstall.exe), which collapses into a single rule, and the summary says so
    // rather than letting the ticked count and the button label quietly disagree.
    public static string Summary(int selectedCount, int hiddenCount, int? ruleCount = null)
    {
        if (selectedCount <= 0)
        {
            return Hint;
        }

        var text = selectedCount == 1 ? "1 app selected" : $"{selectedCount} apps selected";
        if (hiddenCount > 0)
        {
            text += $" ({hiddenCount} hidden by filter)";
        }

        if (ruleCount is { } rules && rules < selectedCount)
        {
            text += rules == 1 ? ", 1 unique exe name" : $", {rules} unique exe names";
        }

        return text;
    }

    public static string ConfirmLabel(int ruleCount, bool isEditMode)
    {
        if (isEditMode)
        {
            return "Save";
        }

        return ruleCount >= 2 ? $"Add {ruleCount} rules" : "Add rule";
    }

    // The same exe can be picked twice via different sources (e.g. the installed-programs
    // entry's full path and the running-process entry for it); that must yield one rule, not two.
    public static List<(string ExeName, string DisplayName)> DistinctByExeName(
        IEnumerable<(string ExeName, string DisplayName)> items)
    {
        return items
            .DistinctBy(item => item.ExeName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
