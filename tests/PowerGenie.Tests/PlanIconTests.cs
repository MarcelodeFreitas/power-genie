using System.Drawing;
using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class PlanIconTests
{
    private static PowerPlan Plan(string name) => new(Guid.NewGuid(), name, false);

    [Fact]
    public void Defaults_run_from_cool_to_hot_by_performance_regardless_of_plan_names()
    {
        var saver = Plan("Zzz");
        var balanced = Plan("Qqq");
        var high = Plan("Aaa");
        var ultimate = Plan("Mmm");
        var scores = new Dictionary<Guid, int>
        {
            [saver.Guid] = 50_005, [balanced.Guid] = 100_005, [high.Guid] = 100_090, [ultimate.Guid] = 100_100,
        };

        var icons = PlanIconDefaults.Compute(new[] { saver, balanced, high, ultimate }, scores);

        Assert.Equal("leaf", icons[saver.Guid].Glyph);
        Assert.Equal("balance", icons[balanced.Guid].Glyph);
        Assert.Equal("bolt", icons[high.Guid].Glyph);
        Assert.Equal("flame", icons[ultimate.Guid].Glyph);
    }

    [Fact]
    public void Plans_with_equal_performance_still_get_distinct_icons()
    {
        var a = Plan("A");
        var b = Plan("B");
        var c = Plan("C");
        var scores = new Dictionary<Guid, int> { [a.Guid] = 100_100, [b.Guid] = 100_100, [c.Guid] = 100_100 };

        var icons = PlanIconDefaults.Compute(new[] { a, b, c }, scores);

        var pairs = new[] { a, b, c }.Select(p => (icons[p.Guid].DotColor, icons[p.Guid].Glyph)).ToList();
        Assert.Equal(3, pairs.Distinct().Count());
    }

    [Fact]
    public void Defaults_are_deterministic()
    {
        var plans = new[] { Plan("A"), Plan("B"), Plan("C") };
        var scores = plans.ToDictionary(p => p.Guid, _ => 100_100);

        var first = PlanIconDefaults.Compute(plans, scores);
        var second = PlanIconDefaults.Compute(plans, scores);

        Assert.All(plans, p =>
        {
            Assert.Equal(first[p.Guid].DotColor, second[p.Guid].DotColor);
            Assert.Equal(first[p.Guid].Glyph, second[p.Guid].Glyph);
        });
    }

    [Fact]
    public void A_plan_without_a_score_is_treated_as_the_lowest()
    {
        var unscored = Plan("New");
        var scored = Plan("Old");

        var icons = PlanIconDefaults.Compute(new[] { unscored, scored }, new Dictionary<Guid, int> { [scored.Guid] = 100_100 });

        Assert.Equal("leaf", icons[unscored.Guid].Glyph);
        Assert.Equal("flame", icons[scored.Guid].Glyph);
    }

    [Fact]
    public void A_lone_performance_level_sits_in_the_middle_of_the_ladder()
    {
        Assert.Equal(1, PlanIconDefaults.LadderStep(0, 1));
        Assert.Equal(0, PlanIconDefaults.LadderStep(0, 5));
        Assert.Equal(3, PlanIconDefaults.LadderStep(4, 5));
    }

    [Theory]
    [InlineData("#E8B923", "#212121")]
    [InlineData("#FFFFFF", "#212121")]
    [InlineData("#D13438", "#FFFFFF")]
    [InlineData("#212121", "#FFFFFF")]
    [InlineData("garbage", "#FFFFFF")]
    public void Symbol_color_contrasts_with_the_dot(string dot, string expected)
    {
        Assert.Equal(expected, PlanIconDefaults.ContrastingGlyphColor(dot));
    }

    [Fact]
    public void Resolve_prefers_the_customized_icon_then_automatic_then_plain_gray()
    {
        var planA = Guid.NewGuid();
        var planB = Guid.NewGuid();
        var custom = new PlanIconStyle { Glyph = "moon" };
        var auto = new PlanIconStyle { Glyph = "leaf" };

        var customized = new Dictionary<Guid, PlanIconStyle> { [planA] = custom };
        var automatic = new Dictionary<Guid, PlanIconStyle> { [planA] = auto, [planB] = auto };

        Assert.Same(custom, PlanIconDefaults.Resolve(planA, customized, automatic));
        Assert.Same(auto, PlanIconDefaults.Resolve(planB, customized, automatic));
        Assert.Equal(PlanGlyphs.None, PlanIconDefaults.Resolve(Guid.NewGuid(), customized, automatic).Glyph);
    }

    [Fact]
    public void Render_draws_a_dot_in_the_chosen_color()
    {
        using var bitmap = TrayIconRenderer.Render(
            new PlanIconStyle { DotColor = "#D13438", Glyph = PlanGlyphs.None }, 32);

        var center = bitmap.GetPixel(16, 16);
        Assert.Equal(0xD1, center.R);
        Assert.Equal(0x34, center.G);
        Assert.Equal(0x38, center.B);
        Assert.Equal(0, bitmap.GetPixel(0, 0).A);
    }

    [Theory]
    [InlineData("leaf")]
    [InlineData("balance")]
    [InlineData("bolt")]
    [InlineData("flame")]
    [InlineData("rocket")]
    [InlineData("moon")]
    [InlineData("battery")]
    [InlineData("star")]
    public void Every_symbol_draws_something_on_top_of_the_dot(string glyph)
    {
        using var plain = TrayIconRenderer.Render(new PlanIconStyle { DotColor = "#000000", GlyphColor = "#FFFFFF", Glyph = PlanGlyphs.None }, 32);
        using var withGlyph = TrayIconRenderer.Render(new PlanIconStyle { DotColor = "#000000", GlyphColor = "#FFFFFF", Glyph = glyph }, 32);

        var differing = 0;
        for (var x = 0; x < 32; x++)
        {
            for (var y = 0; y < 32; y++)
            {
                if (plain.GetPixel(x, y) != withGlyph.GetPixel(x, y))
                {
                    differing++;
                }
            }
        }

        Assert.True(differing > 20, $"{glyph} changed only {differing} pixels");
    }

    [Fact]
    public void Unknown_symbol_ids_fall_back_to_none()
    {
        Assert.Equal(PlanGlyphs.None, PlanGlyphs.Find("does-not-exist").Id);
        Assert.Empty(PlanGlyphs.CreatePaths("does-not-exist"));
    }
}
