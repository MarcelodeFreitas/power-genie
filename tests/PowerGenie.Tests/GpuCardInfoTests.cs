using PowerGenie.App.Models;

namespace PowerGenie.Tests;

public class GpuCardInfoTests
{
    [Fact]
    public void Label_names_the_model_and_its_slot_so_identical_cards_differ()
    {
        var card = new GpuCardInfo(1, "GPU-bbb", "00000000:2B:00.0", "NVIDIA GeForce RTX 3090", 290, 350, 100, 450);

        Assert.Equal("RTX 3090", card.ShortName);
        Assert.Equal("RTX 3090 (PCI 2B:00)", card.Label);
    }
}
