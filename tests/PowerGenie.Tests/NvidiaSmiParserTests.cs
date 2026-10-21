using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class NvidiaSmiParserTests
{
    [Fact]
    public void Reads_name_and_limits_from_query_output()
    {
        var card = NvidiaSmiParser.Parse("NVIDIA GeForce RTX 3090, 290.00, 420.00, 100.00, 450.00\r\n");

        Assert.NotNull(card);
        Assert.Equal("NVIDIA GeForce RTX 3090", card.Name);
        Assert.Equal(290, card.CurrentWatts);
        Assert.Equal(420, card.DefaultWatts);
        Assert.Equal(100, card.MinWatts);
        Assert.Equal(450, card.MaxWatts);
    }

    [Fact]
    public void Reads_only_the_first_gpu_when_several_are_listed()
    {
        var card = NvidiaSmiParser.Parse(
            "NVIDIA GeForce RTX 3090, 290.00, 420.00, 100.00, 450.00\r\n" +
            "NVIDIA GeForce RTX 4070, 200.00, 200.00, 100.00, 220.00\r\n");

        Assert.NotNull(card);
        Assert.Equal("NVIDIA GeForce RTX 3090", card.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("NVIDIA GeForce RTX 3090, 290.00")]
    [InlineData("NVIDIA GeForce RTX 3090, [N/A], 420.00, 100.00, 450.00")]
    public void Returns_null_when_output_is_unusable(string output)
    {
        Assert.Null(NvidiaSmiParser.Parse(output));
    }
}
