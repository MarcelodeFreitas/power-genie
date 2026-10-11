namespace PowerGenie.App.Services;

// A class/record, not a value tuple: WPF data binding resolves property names via runtime
// reflection, which can only see a tuple's real members (Item1/Item2) — the "Name"/"Hex"
// element names on (string Name, string Hex) exist only at compile time, so binding to them
// silently renders blank. A record has real runtime properties, so binding works.
public sealed record PaletteColor(string Name, string Hex);

// The colors offered for both a plan's dot and its symbol.
public static class PlanColorPalette
{
    public const string DefaultHex = "#616161";
    public const string White = "#FFFFFF";
    public const string Black = "#212121";

    public static readonly IReadOnlyList<PaletteColor> Colors = new List<PaletteColor>
    {
        new("Green", "#2E9E5B"),
        new("Teal", "#00838F"),
        new("Blue", "#1E6FD9"),
        new("Purple", "#7B3FB5"),
        new("Pink", "#D6458C"),
        new("Red", "#D13438"),
        new("Orange", "#EF7B1A"),
        new("Yellow", "#E8B923"),
        new("Gray", DefaultHex),
        new("Black", Black),
        new("White", White),
    };

    // A stored color that isn't in the palette (hand-edited config) still has to resolve to
    // something selectable, so unknown values fall back to Gray.
    public static PaletteColor Find(string? hex) =>
        Colors.FirstOrDefault(c => string.Equals(c.Hex, hex, StringComparison.OrdinalIgnoreCase))
        ?? Colors.First(c => c.Hex == DefaultHex);
}
