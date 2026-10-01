using System.Collections.Generic;
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
    public void Defaults_HaveAllAnchors_AndCloneIsDeep()
    {
        var s = FogSettings.Defaults(1);
        Assert.Equal(FogSettings.AnchorNames.Length, s.Anchors.Count);
        Assert.NotNull(s.Anchor("lefthand"));
        Assert.NotNull(s.Anchor("LeftShoulder"));
        Assert.NotNull(s.Anchor("RightLowerLeg"));
        var copy = s.Clone();
        copy.Anchor("Head").Radius = 9f;
        copy.Rate = 99f;
        Assert.NotEqual(9f, s.Anchor("Head").Radius);
        Assert.NotEqual(99f, s.Rate);
    }

    [Fact]
    public void Defaults_PerTier()
    {
        var t1 = FogSettings.Defaults(1);
        Assert.True(t1.Enabled);
        Assert.False(t1.OuterEnabled);
        Assert.Equal(0f, t1.Drift);
        Assert.Equal(0.35f, t1.SpreadY, 5);
        var t2 = FogSettings.Defaults(2);
        Assert.True(t2.Enabled);
        Assert.True(t2.OuterEnabled);
        Assert.Equal(0.45f, t2.Alpha, 5);
        Assert.Equal(0.1f, t2.DistortionStrength, 5);
        var t3 = FogSettings.Defaults(3);
        Assert.False(t3.Enabled);
        Assert.Equal(0.03f, t3.DistortionStrength, 5);
        Assert.Equal(0.02f, t3.DA, 5);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Settings_RoundTripThroughConfigText(int tier)
    {
        var s = FogSettings.Defaults(tier);
        s.Rate = 7.5f; s.SpreadY = 0.2f; s.DynamicColor = false; s.R = 0.5f; s.DA = 0.3f;
        s.Anchor("LeftUpperLeg").Enabled = false;
        var text = new Dictionary<string, string>();
        foreach (var k in FogSettings.Keys) text[k.Name] = s.Get(k.Name);

        var warnings = new List<string>();
        var parsed = FogSettings.Parse(tier, k => text.TryGetValue(k, out var v) ? v : null, warnings);

        Assert.Empty(warnings);
        foreach (var k in FogSettings.Keys) Assert.Equal(text[k.Name], parsed.Get(k.Name));
        Assert.False(parsed.Anchor("LeftUpperLeg").Enabled);
    }

    [Fact]
    public void Parse_MalformedValueKeepsDefaultAndWarns()
    {
        var warnings = new List<string>();
        var parsed = FogSettings.Parse(2, k => k == "Rate" ? "lots" : k == "Color" ? "1,1" : null, warnings);
        Assert.Equal(2, warnings.Count);
        Assert.Equal(FogSettings.Defaults(2).Rate, parsed.Rate);
    }

    [Fact]
    public void TrySet_ClampsAndRejectsUnknownKeys()
    {
        var s = FogSettings.Defaults(1);
        Assert.True(s.TrySet("Alpha", "3"));
        Assert.Equal(1f, s.Alpha);
        Assert.True(s.TrySet("Rate", "-2"));
        Assert.Equal(0f, s.Rate);
        Assert.False(s.TrySet("Nope", "1"));
        Assert.False(s.TrySet("Anchor.Tail", "on,0.1,0,0,0"));
    }

    [Fact]
    public void Blend_IsLinearAndClamped()
    {
        var a = new FogRgb(1f, 0f, 0.5f);
        var b = new FogRgb(0f, 1f, 0.5f);
        var mid = FogRgb.Blend(a, b, 0.5f);
        Assert.Equal(0.5f, mid.R, 5);
        Assert.Equal(0.5f, mid.G, 5);
        Assert.Equal(0.5f, mid.B, 5);
        Assert.Equal(1f, FogRgb.Blend(a, b, -1f).R, 5);
        Assert.Equal(1f, FogRgb.Blend(a, b, 2f).G, 5);
    }

    [Fact]
    public void ParticleBudget_WarnsAndCaps()
    {
        var s = FogSettings.Defaults(1);
        s.Rate = 4f; s.Lifetime = 5f;
        Assert.False(s.ExceedsParticleBudget);
        s.Rate = 30f; s.Lifetime = 8f;
        Assert.True(s.ExceedsParticleBudget);
        Assert.Equal(FogSettings.ParticleHardCap, s.MaxParticles(s.Rate));
        Assert.True(s.MaxParticles(0f) >= 8);
    }
}
