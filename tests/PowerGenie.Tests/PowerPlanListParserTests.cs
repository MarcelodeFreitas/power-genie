using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class PowerPlanListParserTests
{
    private const string SampleOutput =
        "Existing Power Schemes (* Active)\r\n" +
        "-----------------------------------\r\n" +
        "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)\r\n" +
        "Power Scheme GUID: 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  (High performance)\r\n" +
        "Power Scheme GUID: a1841308-3541-4fab-bc81-f71556f20b4a  (Power saver) *\r\n";

    [Fact]
    public void Parses_all_schemes_from_powercfg_list_output()
    {
        var plans = PowerPlanListParser.Parse(SampleOutput);

        Assert.Equal(3, plans.Count);
        Assert.Equal("Balanced", plans[0].Name);
        Assert.Equal(Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"), plans[0].Guid);
    }

    [Fact]
    public void Marks_the_scheme_with_a_trailing_asterisk_as_active()
    {
        var plans = PowerPlanListParser.Parse(SampleOutput);

        var active = plans.Single(p => p.IsActive);
        Assert.Equal("Power saver", active.Name);
    }

    [Fact]
    public void Ignores_header_and_separator_lines()
    {
        var plans = PowerPlanListParser.Parse(SampleOutput);

        Assert.All(plans, p => Assert.NotEqual(Guid.Empty, p.Guid));
    }
}
