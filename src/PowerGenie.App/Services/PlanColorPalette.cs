namespace PowerGenie.App.Services;

// A class/record, not a value tuple: WPF data binding resolves property names via runtime
// reflection, which can only see a tuple's real members (Item1/Item2) — the "Name"/"Hex"
// element names on (string Name, string Hex) exist only at compile time, so binding to them
// silently renders blank. A record has real runtime properties, so binding works.
public sealed record PaletteColor(string Name, string Hex);

public static class PlanColorPalette
{
    private const string DefaultHex = "#616161";

    // The last entry (Gray) is reserved as the "nothing assigned yet, unknown plan" fallback
    // and is deliberately excluded from auto-assignment so every plan a user hasn't touched
    // still gets a distinct, intentional-looking color instead of falling back to gray.
    public static readonly IReadOnlyList<PaletteColor> Colors = new List<PaletteColor>
    {
        new("Green", "#2E7D32"),
        new("Blue", "#1565C0"),
        new("Orange", "#EF6C00"),
        new("Red", "#C62828"),
        new("Purple", "#6A1B9A"),
        new("Teal", "#00838F"),
        new("Gray", DefaultHex),
    };

    public static string GetColorForPlan(
        Guid planGuid,
        IReadOnlyDictionary<Guid, string> assignments,
        IReadOnlyList<Guid> availablePlanGuidsInOrder)
    {
        if (assignments.TryGetValue(planGuid, out var assignedHex))
        {
            return assignedHex;
        }

        var index = availablePlanGuidsInOrder.IndexOf(planGuid);
        if (index < 0)
        {
            return DefaultHex;
        }

        var autoAssignablePaletteSize = Colors.Count - 1;
        return Colors[index % autoAssignablePaletteSize].Hex;
    }

    private static int IndexOf(this IReadOnlyList<Guid> list, Guid value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
            {
                return i;
            }
        }

        return -1;
    }
}
