using InvisibilityPotion.Visuals;
using Xunit;

public class FogSettingsTests
{
    [Fact]
    public void Anchor_RoundTrips()
    {
        Assert.True(FogAnchor.TryParse("Chest", "on,0.3,0,0.1,0", out var a));
        Assert.True(a.Enabled);
        Assert.Equal(0.3f, a.Radius, 5);
        Assert.Equal(0.1f, a.Y, 5);
        Assert.Equal("on,0.3,0,0.1,0", a.Format());
    }

    [Fact]
    public void Anchor_ParsesOffAndRejectsMalformed()
    {
        Assert.True(FogAnchor.TryParse("Head", " off , 0.15 , 0 , 0.05 , 0 ", out var a));
        Assert.False(a.Enabled);
        Assert.False(FogAnchor.TryParse("Head", "maybe,0.1,0,0,0", out _));
        Assert.False(FogAnchor.TryParse("Head", "on,0.1,0,0", out _));
        Assert.False(FogAnchor.TryParse("Head", "on,-1,0,0,0", out _));
    }

    [Fact]
    public void FloatList_RoundTripsColor()
    {
        Assert.True(FloatList.TryParse("1,1,1,0.15", 4, out var c));
        Assert.Equal(0.15f, c[3], 5);
        Assert.Equal("1,1,1,0.15", FloatList.Format(c));
        Assert.False(FloatList.TryParse("1,1,1", 4, out _));
    }

    [Fact]
    public void Defaults_HaveAllAnchors_AndAlphaScalesWithDensity()
    {
        var s = FogSettings.Defaults();
        Assert.Equal(FogSettings.AnchorNames.Length, s.Anchors.Count);
        Assert.NotNull(s.Anchor("lefthand"));
        Assert.Equal(s.Alpha, s.EffectiveAlpha(FogSettings.ReferenceDensity), 5);
        Assert.Equal(0f, s.EffectiveAlpha(0f), 5);
        var copy = s.Clone();
        copy.Anchor("Head").Radius = 9f;
        Assert.NotEqual(9f, s.Anchor("Head").Radius);
    }
}
