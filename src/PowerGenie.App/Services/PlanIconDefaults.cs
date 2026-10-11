using System.Globalization;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

// Picks an icon for every plan automatically from how performance-oriented it is, never from its
// name, so plans with any name (vendor, custom, translated) still get a sensible icon. Cool and
// calm for low-power plans, hot for high-performance ones. Anything the user customizes in
// Settings overrides this.
public static class PlanIconDefaults
{
    private static readonly (string Dot, string Glyph)[] Ladder =
    {
        ("#2E9E5B", "leaf"),
        ("#E8B923", "balance"),
        ("#EF7B1A", "bolt"),
        ("#D13438", "flame"),
    };

    // Plans that rank equally (or share a ladder step because there are more than four distinct
    // levels) would look identical, so repeats cycle through these dot colors until each plan's
    // dot-and-symbol pair is unique.
    private static readonly string[] Alternates =
    {
        "#1E6FD9", "#7B3FB5", "#00838F", "#D6458C", "#616161",
    };

    public static Dictionary<Guid, PlanIconStyle> Compute(
        IReadOnlyList<PowerPlan> plans,
        IReadOnlyDictionary<Guid, int> performanceScores)
    {
        int ScoreOf(PowerPlan plan) => performanceScores.TryGetValue(plan.Guid, out var s) ? s : 0;

        var levels = plans.Select(ScoreOf).Distinct().OrderBy(s => s).ToList();
        var used = new HashSet<(string Dot, string Glyph)>();
        var result = new Dictionary<Guid, PlanIconStyle>();

        // Lowest performance first, list order breaking ties, so the outcome is deterministic.
        foreach (var plan in plans.Select((p, i) => (Plan: p, Index: i)).OrderBy(x => ScoreOf(x.Plan)).ThenBy(x => x.Index))
        {
            var step = LadderStep(levels.IndexOf(ScoreOf(plan.Plan)), levels.Count);
            var (dot, glyph) = Ladder[step];

            foreach (var alternate in Alternates)
            {
                if (used.Add((dot, glyph)))
                {
                    break;
                }

                dot = alternate;
            }

            result[plan.Plan.Guid] = new PlanIconStyle
            {
                DotColor = dot,
                Glyph = glyph,
                GlyphColor = ContrastingGlyphColor(dot),
            };
        }

        return result;
    }

    // Spreads the distinct performance levels across the four ladder steps; a lone level sits in
    // the middle rather than claiming to be either the lowest or the highest plan.
    internal static int LadderStep(int levelIndex, int levelCount)
    {
        if (levelCount <= 1)
        {
            return 1;
        }

        var position = (double)levelIndex / (levelCount - 1);
        return (int)Math.Round(position * (Ladder.Length - 1), MidpointRounding.AwayFromZero);
    }

    // Dark symbol on light dots (yellow, white), white symbol on everything else.
    public static string ContrastingGlyphColor(string dotHex)
    {
        var hex = dotHex.TrimStart('#');
        if (hex.Length != 6
            || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return PlanColorPalette.White;
        }

        var r = (rgb >> 16) & 0xFF;
        var g = (rgb >> 8) & 0xFF;
        var b = rgb & 0xFF;
        var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
        return luminance > 0.6 ? PlanColorPalette.Black : PlanColorPalette.White;
    }

    // Saved customization wins; otherwise the automatic icon; otherwise (a plan the list no
    // longer knows) a plain gray dot.
    public static PlanIconStyle Resolve(
        Guid planGuid,
        IReadOnlyDictionary<Guid, PlanIconStyle> customized,
        IReadOnlyDictionary<Guid, PlanIconStyle> automatic)
    {
        if (customized.TryGetValue(planGuid, out var custom))
        {
            return custom;
        }

        return automatic.TryGetValue(planGuid, out var auto)
            ? auto
            : new PlanIconStyle { DotColor = PlanColorPalette.DefaultHex, GlyphColor = PlanColorPalette.White, Glyph = PlanGlyphs.None };
    }
}
