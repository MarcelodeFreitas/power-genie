namespace PowerGenie.App.Models;

// How one power plan is drawn in the tray: a colored dot with an optional symbol on top.
public sealed class PlanIconStyle
{
    public string DotColor { get; set; } = "#616161";
    public string GlyphColor { get; set; } = "#FFFFFF";
    public string Glyph { get; set; } = PlanGlyphs.None;
}
