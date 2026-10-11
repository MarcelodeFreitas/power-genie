using System.Drawing;
using System.Drawing.Drawing2D;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

// Draws plan icons. Shared by the tray and the Settings preview so what you pick is exactly
// what appears in the tray.
public static class TrayIconRenderer
{
    // The symbol fills about this much of the dot's width: big enough to read at 16 px,
    // small enough to stay inside the circle.
    private const float GlyphScale = 0.56f;

    public static Bitmap Render(PlanIconStyle style, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var inset = size * 0.09f;
        var dot = new RectangleF(inset, inset, size - 2 * inset, size - 2 * inset);

        using (var brush = new SolidBrush(ParseColor(style.DotColor, Color.Gray)))
        {
            graphics.FillEllipse(brush, dot);
        }

        // A faint light rim keeps dark dots visible on a dark taskbar.
        using (var rim = new Pen(Color.FromArgb(150, Color.White), Math.Max(1f, size / 24f)))
        {
            graphics.DrawEllipse(rim, dot);
        }

        DrawGlyph(graphics, style.Glyph, style.GlyphColor, size, GlyphScale);
        return bitmap;
    }

    // Just the symbol on a transparent background, for the symbol drop-down.
    public static Bitmap RenderGlyphOnly(string glyphId, string colorHex, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        DrawGlyph(graphics, glyphId, colorHex, size, 0.8f);
        return bitmap;
    }

    private static void DrawGlyph(Graphics graphics, string? glyphId, string colorHex, int size, float scale)
    {
        var paths = PlanGlyphs.CreatePaths(glyphId);
        if (paths.Count == 0)
        {
            return;
        }

        var glyphSize = size * scale;
        var offset = (size - glyphSize) / 2f;
        using var brush = new SolidBrush(ParseColor(colorHex, Color.White));
        using var matrix = new Matrix(glyphSize, 0, 0, glyphSize, offset, offset);

        foreach (var path in paths)
        {
            using (path)
            {
                path.Transform(matrix);
                graphics.FillPath(brush, path);
            }
        }
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try
        {
            return ColorTranslator.FromHtml(hex);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return fallback;
        }
    }
}
