using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class GpuPresetValidatorTests
{
    private static readonly GpuCardInfo Card = new(0, "GPU-0", "00000000:04:00.0", "NVIDIA GeForce RTX 3090", 290, 420, 100, 450);

    [Theory]
    [InlineData(50, 100)]
    [InlineData(250, 250)]
    [InlineData(900, 450)]
    public void Clamp_keeps_watts_inside_the_card_range(int requested, int expected)
    {
        Assert.Equal(expected, GpuPresetValidator.Clamp(requested, Card));
    }

    [Theory]
    [InlineData("Silent", true)]
    [InlineData("  Silent  ", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsValidName_rejects_empty_names(string? name, bool expected)
    {
        Assert.Equal(expected, GpuPresetValidator.IsValidName(name));
    }

    [Fact]
    public void IsValidName_rejects_names_longer_than_the_limit()
    {
        Assert.False(GpuPresetValidator.IsValidName(new string('a', GpuPresetValidator.MaxNameLength + 1)));
        Assert.True(GpuPresetValidator.IsValidName(new string('a', GpuPresetValidator.MaxNameLength)));
    }
}
