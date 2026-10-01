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
        Assert.Equal(FogEmitterMode.Bones, t1.EmitterMode);
        Assert.Equal(0f, t1.MeshRate);
        Assert.Equal(6f, t1.Rate, 5);
        Assert.Equal(0.6f, t1.Size, 5);
        Assert.Equal(3.5f, t1.Lifetime, 5);
        Assert.Equal(0.35f, t1.Alpha, 5);
        Assert.Equal(0.03f, t1.Speed, 5);
        Assert.Equal(0.03f, t1.Drift, 5);
        Assert.True(t1.Trail);
        Assert.Equal(FogTrailMode.Trail, t1.InnerTrailMode);
        Assert.All(t1.Anchors, a => Assert.True(a.Enabled));
        Assert.Equal("0.88,0.9,0.93", t1.Get("Color"));
        Assert.False(t1.DynamicColor);
        Assert.Equal(0.25f, t1.Emission, 5);
        Assert.Equal(0.04f, t1.DistortionStrength, 5);
        Assert.Equal(0.5f, t1.DA, 5);

        var t2 = FogSettings.Defaults(2);
        Assert.True(t2.Enabled);
        Assert.False(t2.Trail);
        Assert.Equal(FogEmitterMode.Bones, t2.EmitterMode);
        Assert.All(t2.Anchors, a => Assert.True(a.Enabled));
        Assert.Equal(12f, t2.Rate, 5);
        Assert.Equal(0.7f, t2.Size, 5);
        Assert.Equal(2.5f, t2.Lifetime, 5);
        Assert.Equal(0.45f, t2.Alpha, 5);
        Assert.Equal(0.4f, t2.SpreadY, 5);
        Assert.Equal("0.85,0.87,0.9", t2.Get("Color"));
        Assert.False(t2.DynamicColor);
        Assert.Equal(0.25f, t2.Emission, 5);
        Assert.True(t2.OuterEnabled);
        Assert.True(t2.OuterTrail);
        Assert.Equal(FogTrailMode.Trail, t2.OuterTrailMode);
        Assert.Equal(new[] { "Hips" }, t2.OuterAnchors);
        Assert.Equal(1.4f, t2.OuterRadius, 5);
        Assert.Equal(0.35f, t2.OuterAlpha, 5);
        Assert.Equal(14f, t2.OuterRate, 5);
        Assert.Equal(1.8f, t2.OuterSize, 5);
        Assert.Equal(3f, t2.OuterLifetime, 5);
        Assert.Equal(0.15f, t2.OuterSpreadY, 5);
        Assert.Equal(0.1f, t2.DistortionStrength, 5);
        Assert.Equal(0.08f, t2.DA, 5);

        var t3 = FogSettings.Defaults(3);
        Assert.False(t3.Enabled);
        Assert.False(t3.OuterEnabled);
        Assert.Equal(0f, t3.Emission);
        Assert.False(t3.DynamicColor);
        Assert.Equal(0.03f, t3.DistortionStrength, 5);
        Assert.Equal(0.02f, t3.DA, 5);
        Assert.Equal(FogSettings.DefaultOuterAnchors, t3.OuterAnchors);
    }

    [Fact]
    public void OuterKeys_AreAbsolute_AndFactorKeysAreObsolete()
    {
        var names = new HashSet<string>();
        foreach (var k in FogSettings.Keys) names.Add(k.Name);
        foreach (var k in new[] { "OuterRadius", "OuterAlpha", "OuterRate", "OuterSize", "OuterLifetime", "Emission" })
        {
            Assert.Contains(k, names);
            Assert.Equal(FogValueKind.Float, Find(k).Kind);
            Assert.False(string.IsNullOrWhiteSpace(Find(k).Description));
        }
        var obsolete = new[] { "OuterRadiusMultiplier", "OuterAlphaFactor", "OuterRateFactor", "OuterSizeFactor", "OuterLifetimeFactor" };
        Assert.Equal(obsolete, FogSettings.ObsoleteKeys);
        foreach (var k in obsolete)
        {
            Assert.DoesNotContain(k, names);
            Assert.False(FogSettings.Defaults(2).TrySet(k, "1"));
        }
    }

    private static FogKey Find(string name)
    {
        foreach (var k in FogSettings.Keys) if (k.Name == name) return k;
        return null;
    }

    [Fact]
    public void OuterAndEmission_ClampOnSet()
    {
        var s = FogSettings.Defaults(2);
        Assert.True(s.TrySet("Emission", "3")); Assert.Equal(1f, s.Emission);
        Assert.True(s.TrySet("Emission", "-1")); Assert.Equal(0f, s.Emission);
        Assert.True(s.TrySet("OuterAlpha", "2")); Assert.Equal(1f, s.OuterAlpha);
        Assert.True(s.TrySet("OuterRate", "-5")); Assert.Equal(0f, s.OuterRate);
        Assert.True(s.TrySet("OuterRadius", "-1")); Assert.Equal(0f, s.OuterRadius);
        Assert.True(s.TrySet("OuterSize", "0")); Assert.Equal(0.01f, s.OuterSize, 5);
        Assert.True(s.TrySet("OuterLifetime", "0")); Assert.Equal(0.05f, s.OuterLifetime, 5);
        Assert.False(s.TrySet("OuterRate", "many"));
    }

    [Fact]
    public void OuterLayer_IsIndependentOfTheInnerRate()
    {
        var s = FogSettings.Defaults(2);
        Assert.True(s.InnerActive);
        Assert.True(s.OuterActive);
        s.Rate = 0f;
        Assert.False(s.InnerActive);
        Assert.True(s.OuterActive);   // the outer layer alone must still spawn
        s.Alpha = 0f;
        Assert.True(s.OuterActive);
        s.OuterRate = 0f;
        Assert.False(s.OuterActive);
        s.OuterRate = 14f; s.OuterAnchors.Clear();
        Assert.False(s.OuterActive);
        s.OuterAnchors.Add("Hips"); s.OuterEnabled = false;
        Assert.False(s.OuterActive);
        var mesh = FogSettings.Defaults(1);
        mesh.EmitterMode = FogEmitterMode.Mesh; mesh.Rate = 0f; mesh.MeshRate = 10f;
        Assert.True(mesh.InnerActive);
    }

    [Fact]
    public void NewKeys_RoundTripAndParse()
    {
        var s = FogSettings.Defaults(3);
        s.Trail = true; s.OuterTrail = true; s.MeshRate = 12.5f; s.OuterSpreadY = 0.2f; s.OuterLifetime = 2f;
        s.OuterRadius = 2.5f; s.OuterAlpha = 0.6f; s.OuterRate = 9f; s.OuterSize = 2.2f; s.Emission = 0.4f;
        s.OuterAnchors = new List<string> { "LeftFoot", "Head" };
        var text = new Dictionary<string, string>();
        foreach (var k in FogSettings.Keys) text[k.Name] = s.Get(k.Name);
        Assert.Equal("LeftFoot,Head", text["OuterAnchors"]);
        Assert.Equal("true", text["Trail"]);

        var warnings = new List<string>();
        var parsed = FogSettings.Parse(3, k => text.TryGetValue(k, out var v) ? v : null, warnings);

        Assert.Empty(warnings);
        Assert.True(parsed.Trail);
        Assert.True(parsed.OuterTrail);
        Assert.Equal(12.5f, parsed.MeshRate, 5);
        Assert.Equal(0.2f, parsed.OuterSpreadY, 5);
        Assert.Equal(2f, parsed.OuterLifetime, 5);
        Assert.Equal(2.5f, parsed.OuterRadius, 5);
        Assert.Equal(0.6f, parsed.OuterAlpha, 5);
        Assert.Equal(9f, parsed.OuterRate, 5);
        Assert.Equal(2.2f, parsed.OuterSize, 5);
        Assert.Equal(0.4f, parsed.Emission, 5);
        Assert.Equal(new[] { "LeftFoot", "Head" }, parsed.OuterAnchors);
    }

    [Fact]
    public void OuterAnchors_CanonicalCaseUnknownRejectedEmptyAllowed()
    {
        var s = FogSettings.Defaults(2);
        Assert.True(s.TrySet("OuterAnchors", " chest , HIPS,chest"));
        Assert.Equal(new[] { "Chest", "Hips" }, s.OuterAnchors);
        Assert.False(s.TrySet("OuterAnchors", "Chest,Tail"));
        Assert.Equal(new[] { "Chest", "Hips" }, s.OuterAnchors);
        Assert.True(s.TrySet("OuterAnchors", ""));
        Assert.Empty(s.OuterAnchors);
        Assert.Equal("", s.Get("OuterAnchors"));
    }

    [Fact]
    public void NewKeys_ClampAndCloneIsDeep()
    {
        var s = FogSettings.Defaults(2);
        Assert.True(s.TrySet("MeshRate", "-3"));
        Assert.Equal(0f, s.MeshRate);
        Assert.True(s.TrySet("OuterSpreadY", "0"));
        Assert.Equal(0.01f, s.OuterSpreadY, 5);
        Assert.False(s.TrySet("OuterTrail", "maybe"));
        var copy = s.Clone();
        copy.OuterAnchors.Add("LeftFoot");
        Assert.DoesNotContain("LeftFoot", s.OuterAnchors);
    }

    [Fact]
    public void MeshRate_ZeroMeansRateTimesAnchors()
    {
        var s = FogSettings.Defaults(2);
        s.Rate = 2f;
        Assert.Equal(26f, s.MeshEmitterRate(13), 5);
        s.MeshRate = 18f;
        Assert.Equal(18f, s.MeshEmitterRate(13), 5);
        Assert.Equal(18f * s.Lifetime, s.LiveParticlesMesh, 3);
    }

    [Fact]
    public void OuterParticleBudget_UsesOuterRateAndLifetime()
    {
        var s = FogSettings.Defaults(2);   // outer rate 14, outer life 3
        Assert.Equal(42f, s.LiveParticlesOuter, 3);
        s.Rate = 0f;   // independent of the inner rate
        Assert.Equal(42f, s.LiveParticlesOuter, 3);
        Assert.Equal(FogSettings.MaxParticles(14f, 3f), (int)System.Math.Ceiling(14f * 3f * 1.3f + 4f));
        s.OuterEnabled = false;
        Assert.Equal(0f, s.LiveParticlesOuter);
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
        var s = FogSettings.Defaults(2);   // Bones: per-emitter budget is Rate x Lifetime
        s.Rate = 4f; s.Lifetime = 5f;
        Assert.False(s.ExceedsParticleBudget);
        s.Rate = 30f; s.Lifetime = 8f;
        Assert.True(s.ExceedsParticleBudget);
        Assert.Equal(FogSettings.ParticleHardCap, s.MaxParticles(s.Rate));
        Assert.True(s.MaxParticles(0f) >= 8);
        var mesh = FogSettings.Defaults(1);   // Mesh: MeshRate x Lifetime
        mesh.EmitterMode = FogEmitterMode.Mesh; mesh.MeshRate = 18f;
        Assert.False(mesh.ExceedsParticleBudget);
        mesh.MeshRate = 60f;
        Assert.True(mesh.ExceedsParticleBudget);
    }

    [Fact]
    public void NewLookKeys_DefaultsAndRoundTrip()
    {
        var s = FogSettings.Defaults(2);
        Assert.Equal("swamp_mist", s.Get("FogMaterial"));
        Assert.Equal("Bones", s.Get("FogEmitterMode"));
        Assert.Equal("-1", s.Get("DistortionWave"));
        Assert.Equal("0.03", s.Get("MeshOffset"));

        s.FogMaterial = "slowwispysmoke"; s.EmitterMode = FogEmitterMode.Mesh; s.DistortionWave = 3.5f; s.MeshOffset = 0.08f;
        var text = new Dictionary<string, string>();
        foreach (var k in FogSettings.Keys) text[k.Name] = s.Get(k.Name);
        var warnings = new List<string>();
        var parsed = FogSettings.Parse(2, k => text.TryGetValue(k, out var v) ? v : null, warnings);

        Assert.Empty(warnings);
        Assert.Equal("slowwispysmoke", parsed.FogMaterial);
        Assert.Equal(FogEmitterMode.Mesh, parsed.EmitterMode);
        Assert.Equal(3.5f, parsed.DistortionWave, 5);
        Assert.Equal(0.08f, parsed.MeshOffset, 5);
    }

    [Fact]
    public void NewLookKeys_NormalizeAndReject()
    {
        var s = FogSettings.Defaults(1);
        Assert.True(s.TrySet("FogMaterial", " Ghost_Smoke "));
        Assert.Equal("ghost_smoke", s.FogMaterial);
        Assert.False(s.TrySet("FogMaterial", "lava"));
        Assert.Equal("ghost_smoke", s.FogMaterial);
        Assert.True(s.TrySet("FogEmitterMode", "mesh"));
        Assert.Equal(FogEmitterMode.Mesh, s.EmitterMode);
        Assert.False(s.TrySet("FogEmitterMode", "surface"));
        Assert.True(s.TrySet("DistortionWave", "-7"));
        Assert.Equal(-1f, s.DistortionWave);
        Assert.True(s.TrySet("MeshOffset", "-1"));
        Assert.Equal(0f, s.MeshOffset);
        foreach (var n in FogSettings.FogMaterialNames) Assert.Equal(n, FogSettings.NormalizeFogMaterial(n.ToUpperInvariant()));
    }

    [Fact]
    public void Parse_MalformedFogMaterialKeepsDefaultAndWarns()
    {
        var warnings = new List<string>();
        var parsed = FogSettings.Parse(2, k => k == "FogMaterial" ? "nope" : k == "FogEmitterMode" ? "x" : null, warnings);
        Assert.Equal(2, warnings.Count);
        Assert.Equal(FogSettings.DefaultFogMaterial, parsed.FogMaterial);
        Assert.Equal(FogEmitterMode.Bones, parsed.EmitterMode);
    }

    [Fact]
    public void GlobalVeilLookDefaults_AreParseableColors()
    {
        var g = new InvisibilityPotion.Config.GlobalConfig();
        Assert.True(FloatList.TryParse(g.ShadowColor, 4, out var shadow));
        Assert.Equal(0.12f, shadow[3], 5);
        Assert.True(FloatList.TryParse(g.SpiritColor, 3, out var spirit));
        Assert.Equal(0.7f, spirit[1], 5);
        Assert.Equal(0.25f, g.SpiritStrength, 5);
    }
}
