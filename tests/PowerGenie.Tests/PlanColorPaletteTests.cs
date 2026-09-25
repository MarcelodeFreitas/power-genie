using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class PlanColorPaletteTests
{
    private static readonly Guid PlanA = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid PlanB = Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    private static readonly Guid PlanC = Guid.Parse("a1841308-3541-4fab-bc81-f71556f20b4a");

    [Fact]
    public void Returns_the_explicitly_assigned_color_when_one_exists()
    {
        var assignments = new Dictionary<Guid, string> { [PlanA] = "#ABCDEF" };

        var color = PlanColorPalette.GetColorForPlan(PlanA, assignments, new[] { PlanA, PlanB });

        Assert.Equal("#ABCDEF", color);
    }

    [Fact]
    public void Auto_assigns_a_palette_color_by_position_when_none_is_explicitly_set()
    {
        var assignments = new Dictionary<Guid, string>();

        var colorA = PlanColorPalette.GetColorForPlan(PlanA, assignments, new[] { PlanA, PlanB, PlanC });
        var colorB = PlanColorPalette.GetColorForPlan(PlanB, assignments, new[] { PlanA, PlanB, PlanC });

        Assert.NotEqual(colorA, colorB);
        Assert.Contains(colorA, PlanColorPalette.Colors.Select(c => c.Hex));
        Assert.Contains(colorB, PlanColorPalette.Colors.Select(c => c.Hex));
    }

    [Fact]
    public void Auto_assignment_is_deterministic_for_the_same_position()
    {
        var assignments = new Dictionary<Guid, string>();
        var order = new[] { PlanA, PlanB, PlanC };

        var first = PlanColorPalette.GetColorForPlan(PlanB, assignments, order);
        var second = PlanColorPalette.GetColorForPlan(PlanB, assignments, order);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Returns_the_default_gray_for_a_plan_not_in_the_available_list()
    {
        var assignments = new Dictionary<Guid, string>();
        var unknownPlan = Guid.NewGuid();

        var color = PlanColorPalette.GetColorForPlan(unknownPlan, assignments, new[] { PlanA, PlanB });

        Assert.Equal("#616161", color);
    }
}
