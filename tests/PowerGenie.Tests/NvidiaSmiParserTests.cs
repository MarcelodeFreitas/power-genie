using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class NvidiaSmiParserTests
{
    [Fact]
    public void Reads_every_card_with_its_identity_and_limits()
    {
        var cards = NvidiaSmiParser.Parse(
            "0, GPU-aaa, 00000000:04:00.0, NVIDIA GeForce RTX 3090, 290.00, 420.00, 100.00, 450.00\r\n" +
            "1, GPU-bbb, 00000000:2B:00.0, NVIDIA GeForce RTX 4070, 200.00, 200.00, 100.00, 220.00\r\n");

        Assert.Equal(2, cards.Count);
        var first = cards[0];
        Assert.Equal(0, first.Index);
        Assert.Equal("GPU-aaa", first.Uuid);
        Assert.Equal("00000000:04:00.0", first.PciBusId);
        Assert.Equal("NVIDIA GeForce RTX 3090", first.Name);
        Assert.Equal(290, first.CurrentWatts);
        Assert.Equal(420, first.DefaultWatts);
        Assert.Equal(100, first.MinWatts);
        Assert.Equal(450, first.MaxWatts);
        Assert.Equal(1, cards[1].Index);
        Assert.Equal("GPU-bbb", cards[1].Uuid);
    }

    [Fact]
    public void Skips_a_card_without_power_limits_and_keeps_the_others()
    {
        var cards = NvidiaSmiParser.Parse(
            "0, GPU-int, 00000000:00:02.0, Intel Graphics, [N/A], [N/A], [N/A], [N/A]\r\n" +
            "1, GPU-aaa, 00000000:04:00.0, NVIDIA GeForce RTX 3090, 290.00, 420.00, 100.00, 450.00\r\n");

        var card = Assert.Single(cards);
        Assert.Equal("GPU-aaa", card.Uuid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0, GPU-aaa, 00000000:04:00.0, NVIDIA GeForce RTX 3090, 290.00")]
    public void Returns_no_cards_when_output_is_unusable(string output)
    {
        Assert.Empty(NvidiaSmiParser.Parse(output));
    }
}
