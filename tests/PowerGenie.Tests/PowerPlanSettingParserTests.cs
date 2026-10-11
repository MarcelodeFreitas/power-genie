using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class PowerPlanSettingParserTests
{
    [Fact]
    public void ParseAcIndex_reads_the_AC_value_regardless_of_label_language()
    {
        const string output = """
            Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)
              Subgroup GUID: 54533251-82be-4824-96c1-47b60b740d00  (Processor power management)
                Power Setting GUID: bc5038f7-23e0-4960-96da-33abaf5935ec  (Maximum processor state)
                  Minimum Possible Setting: 0x00000000
                  Maximum Possible Setting: 0x00000064
                  Possible Settings increment: 0x00000001
                  Possible Settings units: %
                Valor de indice de corriente alterna: 0x00000032
                Valor de indice de corriente continua: 0x00000064
            """;

        Assert.Equal(0x32, PowerPlanSettingParser.ParseAcIndex(output));
    }

    [Fact]
    public void ParseAcIndex_returns_null_when_there_are_no_values()
    {
        Assert.Null(PowerPlanSettingParser.ParseAcIndex("nothing useful here"));
    }
}
