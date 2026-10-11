using System.ComponentModel;
using System.IO;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.App.ViewModels;

// One plan's row in Settings: three drop-downs (dot color, symbol color, symbol) and a live
// preview drawn by the same renderer the tray uses.
public sealed class PlanIconRow : INotifyPropertyChanged
{
    private PaletteColor _dotColor;
    private PaletteColor _glyphColor;
    private GlyphOption _glyph;
    private BitmapSource _preview;

    public PlanIconRow(Guid planGuid, string planName, PlanIconStyle style, bool isCustomized)
    {
        PlanGuid = planGuid;
        PlanName = planName;
        IsCustomized = isCustomized;
        _dotColor = PlanColorPalette.Find(style.DotColor);
        _glyphColor = PlanColorPalette.Find(style.GlyphColor);
        _glyph = PlanGlyphs.Find(style.Glyph);
        _preview = RenderPreview();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid PlanGuid { get; }
    public string PlanName { get; }

    // True once the user changed anything for this plan; only then is it saved, so plans the
    // user never touched keep following the automatic, performance-based choice.
    public bool IsCustomized { get; private set; }

    public IReadOnlyList<PaletteColor> ColorOptions => PlanColorPalette.Colors;
    public IReadOnlyList<GlyphChoice> GlyphOptions => GlyphChoice.All;

    public BitmapSource Preview => _preview;

    public PaletteColor DotColor
    {
        get => _dotColor;
        set => Change(ref _dotColor, value);
    }

    public PaletteColor GlyphColor
    {
        get => _glyphColor;
        set => Change(ref _glyphColor, value);
    }

    public GlyphChoice Glyph
    {
        get => GlyphChoice.For(_glyph);
        set => Change(ref _glyph, value.Option);
    }

    public PlanIconStyle ToStyle() => new()
    {
        DotColor = _dotColor.Hex,
        GlyphColor = _glyphColor.Hex,
        Glyph = _glyph.Id,
    };

    private void Change<T>(ref T field, T value) where T : class
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }

        field = value;
        IsCustomized = true;
        _preview = RenderPreview();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private BitmapSource RenderPreview()
    {
        using var bitmap = TrayIconRenderer.Render(ToStyle(), 48);
        return ToBitmapSource(bitmap);
    }

    internal static BitmapSource ToBitmapSource(System.Drawing.Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}

// A symbol option plus its small preview image for the drop-down list.
public sealed class GlyphChoice
{
    private static readonly Lazy<IReadOnlyList<GlyphChoice>> AllChoices = new(() =>
        PlanGlyphs.Options.Select(o => new GlyphChoice(o)).ToList());

    private GlyphChoice(GlyphOption option)
    {
        Option = option;
        using var bitmap = TrayIconRenderer.RenderGlyphOnly(option.Id, "#404040", 32);
        Preview = option.Id == PlanGlyphs.None ? null : PlanIconRow.ToBitmapSource(bitmap);
    }

    public static IReadOnlyList<GlyphChoice> All => AllChoices.Value;

    public static GlyphChoice For(GlyphOption option) => All.First(c => c.Option.Id == option.Id);

    public GlyphOption Option { get; }
    public string Name => Option.Name;
    public BitmapSource? Preview { get; }
}
