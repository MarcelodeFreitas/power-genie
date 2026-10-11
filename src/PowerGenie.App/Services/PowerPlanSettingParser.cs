using System.Globalization;
using System.Text.RegularExpressions;

namespace PowerGenie.App.Services;

public static class PowerPlanSettingParser
{
    private static readonly Regex HexValue = new("0x([0-9a-fA-F]+)", RegexOptions.Compiled);

    // "powercfg /query <plan> <subgroup> <setting>" prints the setting's possible range and then
    // two "Current AC/DC Power Setting Index: 0x..." lines. The labels are localized but the hex
    // values are not, so the AC value is read as the second-to-last hex number in the output.
    public static int? ParseAcIndex(string queryOutput)
    {
        var values = HexValue.Matches(queryOutput);
        if (values.Count < 2)
        {
            return null;
        }

        var acHex = values[^2].Groups[1].Value;
        return int.TryParse(acHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
