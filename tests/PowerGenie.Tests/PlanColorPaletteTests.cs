using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class PlanColorPaletteTests
{
    [Fact]
    public void Find_is_case_insensitive()
    {
        Assert.Equal("Red", PlanColorPalette.Find("#d13438").Name);
    }

    [Fact]
    public void Find_falls_back_to_gray_for_a_color_outside_the_palette()
    {
        Assert.Equal("Gray", PlanColorPalette.Find("#123456").Name);
        Assert.Equal("Gray", PlanColorPalette.Find(null).Name);
    }

    [Fact]
    public void Palette_colors_are_unique()
    {
        Assert.Equal(PlanColorPalette.Colors.Count, PlanColorPalette.Colors.Select(c => c.Hex).Distinct().Count());
    }
}
