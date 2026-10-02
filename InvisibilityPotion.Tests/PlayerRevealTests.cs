using InvisibilityPotion.Net;
using Xunit;

public class PlayerRevealTests
{
    [Fact]
    public void Not_hidden_is_never_spoofed() => Assert.False(PlayerReveal.ShouldSpoof(false, false, 0, true));

    [Fact]
    public void Owner_gets_the_real_position() => Assert.False(PlayerReveal.ShouldSpoof(true, true, 0, true));

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void Goggles_level_three_sees_through(int goggles, bool spoof) => Assert.Equal(spoof, PlayerReveal.ShouldSpoof(true, false, goggles, true));

    [Fact]
    public void Switch_off_spoofs_even_level_three() => Assert.True(PlayerReveal.ShouldSpoof(true, false, 3, false));
}
