namespace PowerGenie.App.Services;

public static class PlanColorPalette
{
    private const string DefaultHex = "#616161";

    // The last entry (Gray) is reserved as the "nothing assigned yet, unknown plan" fallback
    // and is deliberately excluded from auto-assignment so every plan a user hasn't touched
    // still gets a distinct, intentional-looking color instead of falling back to gray.
    public static readonly IReadOnlyList<(string Name, string Hex)> Colors = new List<(string, string)>
    {
        ("Green", "#2E7D32"),
        ("Blue", "#1565C0"),
        ("Orange", "#EF6C00"),
        ("Red", "#C62828"),
        ("Purple", "#6A1B9A"),
        ("Teal", "#00838F"),
        ("Gray", DefaultHex),
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
