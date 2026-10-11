using System.Drawing;
using System.Drawing.Drawing2D;

namespace PowerGenie.App.Models;

// A record, not a tuple, so WPF data binding can read Name via reflection (see PaletteColor).
public sealed record GlyphOption(string Id, string Name);

// The symbols a plan's tray icon can show. Each is a handful of vector shapes in a unit square
// (0..1 on both axes), so they can be drawn crisply at any icon size without a font or image files.
public static class PlanGlyphs
{
    public const string None = "none";

    public static readonly IReadOnlyList<GlyphOption> Options = new List<GlyphOption>
    {
        new(None, "None"),
        new("leaf", "Leaf"),
        new("balance", "Balance"),
        new("bolt", "Lightning"),
        new("flame", "Flame"),
        new("rocket", "Rocket"),
        new("moon", "Moon"),
        new("battery", "Battery"),
        new("star", "Star"),
    };

    public static GlyphOption Find(string? id) =>
        Options.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Options[0];

    // Each returned path is filled on its own, so shapes may overlap without cutting holes in
    // each other. The caller owns (and disposes) the paths.
    public static List<GraphicsPath> CreatePaths(string? id) => Find(id).Id switch
    {
        "leaf" => new() { Leaf() },
        "balance" => Balance(),
        "bolt" => new() { Polygon(0.62f, 0f, 0.20f, 0.56f, 0.46f, 0.56f, 0.38f, 1f, 0.80f, 0.40f, 0.54f, 0.40f) },
        "flame" => new() { Flame() },
        "rocket" => Rocket(),
        "moon" => new() { Moon() },
        "battery" => Battery(),
        "star" => new() { Star() },
        _ => new(),
    };

    private static GraphicsPath Polygon(params float[] xy)
    {
        var points = new PointF[xy.Length / 2];
        for (var i = 0; i < points.Length; i++)
        {
            points[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
        }

        var path = new GraphicsPath();
        path.AddPolygon(points);
        return path;
    }

    private static GraphicsPath Curves(PointF start, params PointF[] bezierPoints)
    {
        var path = new GraphicsPath();
        var current = start;
        for (var i = 0; i + 2 < bezierPoints.Length; i += 3)
        {
            path.AddBezier(current, bezierPoints[i], bezierPoints[i + 1], bezierPoints[i + 2]);
            current = bezierPoints[i + 2];
        }

        path.CloseFigure();
        return path;
    }

    private static PointF P(float x, float y) => new(x, y);

    // Even-odd fill: the thin midrib sits inside the leaf outline, so it is cut out as a hole.
    private static GraphicsPath Leaf()
    {
        var path = Curves(P(0.08f, 0.92f),
            P(0.06f, 0.42f), P(0.42f, 0.08f), P(0.94f, 0.06f),
            P(0.96f, 0.58f), P(0.60f, 0.94f), P(0.08f, 0.92f));
        path.FillMode = FillMode.Alternate;
        path.StartFigure();
        path.AddPolygon(new[] { P(0.22f, 0.74f), P(0.26f, 0.79f), P(0.76f, 0.29f), P(0.71f, 0.24f) });
        return path;
    }

    private static GraphicsPath Flame() => Curves(P(0.50f, 0f),
        P(0.56f, 0.24f), P(0.86f, 0.40f), P(0.86f, 0.68f),
        P(0.86f, 0.88f), P(0.68f, 1f), P(0.50f, 1f),
        P(0.32f, 1f), P(0.14f, 0.88f), P(0.14f, 0.68f),
        P(0.14f, 0.50f), P(0.28f, 0.38f), P(0.36f, 0.28f),
        P(0.40f, 0.40f), P(0.46f, 0.46f), P(0.52f, 0.44f),
        P(0.58f, 0.30f), P(0.44f, 0.16f), P(0.50f, 0f));

    private static GraphicsPath Moon() => Curves(P(0.62f, 0.04f),
        P(0.22f, 0.08f), P(0.06f, 0.46f), P(0.18f, 0.74f),
        P(0.32f, 1.02f), P(0.72f, 1.06f), P(0.94f, 0.78f),
        P(0.58f, 0.84f), P(0.36f, 0.56f), P(0.48f, 0.28f),
        P(0.52f, 0.18f), P(0.56f, 0.10f), P(0.62f, 0.04f));

    private static GraphicsPath Star()
    {
        var points = new List<float>();
        for (var i = 0; i < 10; i++)
        {
            var radius = i % 2 == 0 ? 0.52f : 0.22f;
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            points.Add(0.5f + radius * (float)Math.Cos(angle));
            points.Add(0.54f + radius * (float)Math.Sin(angle));
        }

        return Polygon(points.ToArray());
    }

    private static List<GraphicsPath> Balance() => new()
    {
        Rect(0.08f, 0.18f, 0.84f, 0.09f),
        Rect(0.46f, 0.27f, 0.08f, 0.61f),
        Rect(0.26f, 0.88f, 0.48f, 0.09f),
        Polygon(0.18f, 0.27f, 0.02f, 0.66f, 0.34f, 0.66f),
        Polygon(0.82f, 0.27f, 0.66f, 0.66f, 0.98f, 0.66f),
    };

    // Drawn pointing up, then tilted 45 degrees so it flies diagonally and fills the dot's corner to corner.
    private static List<GraphicsPath> Rocket()
    {
        var parts = new List<GraphicsPath>
        {
            Curves(P(0.5f, 0f), P(0.76f, 0.12f), P(0.76f, 0.44f), P(0.70f, 0.74f),
                P(0.58f, 0.76f), P(0.42f, 0.76f), P(0.30f, 0.74f),
                P(0.24f, 0.44f), P(0.24f, 0.12f), P(0.5f, 0f)),
            Polygon(0.28f, 0.54f, 0.12f, 0.80f, 0.28f, 0.76f),
            Polygon(0.72f, 0.54f, 0.88f, 0.80f, 0.72f, 0.76f),
            Polygon(0.38f, 0.80f, 0.62f, 0.80f, 0.5f, 1f),
        };

        using var tilt = new Matrix();
        tilt.RotateAt(45f, new PointF(0.5f, 0.5f));
        foreach (var part in parts)
        {
            part.Transform(tilt);
        }

        return parts;
    }

    // Outline with a charge bar inside: the hole and the bar both come from the even-odd fill
    // of one path, so the bar reads as filled inside the hollow body.
    private static List<GraphicsPath> Battery()
    {
        var body = new GraphicsPath(FillMode.Alternate);
        body.AddRectangle(new RectangleF(0.04f, 0.24f, 0.80f, 0.52f));
        body.StartFigure();
        body.AddRectangle(new RectangleF(0.14f, 0.34f, 0.60f, 0.32f));
        body.StartFigure();
        body.AddRectangle(new RectangleF(0.20f, 0.40f, 0.30f, 0.20f));

        return new() { body, Rect(0.86f, 0.40f, 0.10f, 0.20f) };
    }

    private static GraphicsPath Rect(float x, float y, float w, float h)
    {
        var path = new GraphicsPath();
        path.AddRectangle(new RectangleF(x, y, w, h));
        return path;
    }
}
