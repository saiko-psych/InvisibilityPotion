using System;
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
        // Round L: tier I = the whole body in a light enveloping cloud, the fog volume layer and the ground field.
        Assert.True(t1.OuterEnabled);
        Assert.Equal(FogOuterShape.Volume, t1.OuterShape);
        Assert.Equal(new[] { "Hips" }, t1.OuterAnchors);
        Assert.Equal(0.07f, t1.OuterAlpha, 5);
        Assert.Equal(2.4f, t1.OuterRadius, 5);
        Assert.Equal(3.2f, t1.OuterSize, 5);
        Assert.Equal(0.35f, t1.OuterSpreadY, 5);
        Assert.Equal(10f, t1.OuterRate, 5);
        Assert.Equal(6f, t1.OuterLifetime, 5);
        Assert.Equal(3f, t1.OuterRotation, 5);
        Assert.Equal(0f, t1.OuterOffsetY, 5);
        Assert.False(t1.OuterHorizontal);
        Assert.False(t1.OuterTrail);
        Assert.Equal(FogEmitterMode.Bones, t1.EmitterMode);
        Assert.Equal(0f, t1.MeshRate);
        Assert.Equal(10f, t1.Rate, 5);
        Assert.Equal(0.75f, t1.Size, 5);
        Assert.Equal(2.2f, t1.Lifetime, 5);
        Assert.Equal(0.25f, t1.Alpha, 5);
        Assert.Equal(0.6f, t1.SpreadY, 5);
        Assert.Equal(0.03f, t1.Speed, 5);
        Assert.Equal(0.03f, t1.Drift, 5);
        Assert.False(t1.Trail);
        Assert.Equal(FogTrailMode.Follow, t1.InnerTrailMode);
        Assert.True(t1.GroundEnabled);
        Assert.True(t1.GroundActive);
        Assert.Equal(4f, t1.GroundRate, 5);
        Assert.Equal(2f, t1.GroundRateDistance, 5);
        Assert.Equal(0.8f, t1.GroundSize, 5);
        Assert.Equal(3f, t1.GroundGrow, 5);
        Assert.Equal(7f, t1.GroundLifetime, 5);
        Assert.Equal(0.15f, t1.GroundAlpha, 5);
        Assert.Equal(0.6f, t1.GroundRadius, 5);
        Assert.Equal(0.15f, t1.GroundHeight, 5);
        Assert.Equal(0.05f, t1.GroundDrift, 5);
        Assert.All(t1.Anchors, a => Assert.True(a.Enabled));
        Assert.Equal("0.8,0.82,0.85", t1.Get("Color"));
        Assert.False(t1.DynamicColor);
        Assert.Equal(0f, t1.Emission);   // round I: matte fog, no self-illumination
        Assert.Equal(0.04f, t1.DistortionStrength, 5);
        Assert.Equal(0.5f, t1.DA, 5);

        var t2 = FogSettings.Defaults(2);
        Assert.True(t2.Enabled);
        Assert.False(t2.Trail);
        Assert.Equal(FogEmitterMode.Bones, t2.EmitterMode);
        Assert.All(t2.Anchors, a => Assert.True(a.Enabled));
        // Round L: a denser enveloping inner cloud, Emission 0 (matte).
        Assert.Equal(14f, t2.Rate, 5);
        Assert.Equal(0.8f, t2.Size, 5);
        Assert.Equal(2.5f, t2.Lifetime, 5);
        Assert.Equal(0.05f, t2.Speed, 5);
        Assert.Equal(0.35f, t2.Alpha, 5);
        Assert.Equal(1.125f, t2.SpreadX, 5);
        Assert.Equal(0.6f, t2.SpreadY, 5);
        Assert.Equal(1.025f, t2.SpreadZ, 5);
        Assert.Equal(-0.066f, t2.Drift, 5);
        Assert.Equal("0.8,0.8,0.8", t2.Get("Color"));
        Assert.False(t2.DynamicColor);
        Assert.Equal(0f, t2.Emission);
        Assert.True(t2.OuterEnabled);
        // Round L ruling: the outer layer is a fog volume of large soft camera-facing sprites around the player (local space).
        Assert.False(t2.OuterTrail);
        Assert.Equal(FogTrailMode.Follow, t2.OuterTrailMode);
        Assert.Equal(FogOuterShape.Volume, t2.OuterShape);
        Assert.Equal(new[] { "Hips" }, t2.OuterAnchors);
        Assert.Equal(3f, t2.OuterRadius, 5);
        Assert.Equal(0.1f, t2.OuterAlpha, 5);
        Assert.Equal(10f, t2.OuterRate, 5);
        Assert.Equal(3.2f, t2.OuterSize, 5);
        Assert.Equal(6f, t2.OuterLifetime, 5);
        Assert.Equal(0.35f, t2.OuterSpreadY, 5);
        Assert.Equal(0f, t2.OuterOffsetY, 5);
        Assert.Equal(3f, t2.OuterRotation, 5);
        Assert.False(t2.OuterHorizontal);
        Assert.True(t2.GroundEnabled);
        Assert.Equal(6f, t2.GroundRate, 5);
        Assert.Equal(3f, t2.GroundRateDistance, 5);
        Assert.Equal(1.2f, t2.GroundSize, 5);
        Assert.Equal(2.5f, t2.GroundGrow, 5);
        Assert.Equal(7f, t2.GroundLifetime, 5);
        Assert.Equal(0.18f, t2.GroundAlpha, 5);
        Assert.Equal(0.9f, t2.GroundRadius, 5);
        Assert.Equal(0.15f, t2.GroundHeight, 5);
        Assert.Equal(0.05f, t2.GroundDrift, 5);
        Assert.Equal(0.1f, t2.DistortionStrength, 5);
        Assert.Equal(0.08f, t2.DA, 5);

        var t3 = FogSettings.Defaults(3);
        Assert.False(t3.Enabled);
        Assert.False(t3.OuterEnabled);
        Assert.False(t3.GroundEnabled);
        Assert.False(t3.GroundActive);
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
    public void GroundKeys_AreBoundWithDescriptions()
    {
        Assert.Equal(new[] { "GroundEnabled", "GroundRate", "GroundRateDistance", "GroundSize", "GroundGrow", "GroundLifetime", "GroundAlpha", "GroundRadius", "GroundHeight", "GroundDrift" },
                     FogSettings.GroundKeys);
        foreach (var k in FogSettings.GroundKeys)
        {
            var key = Find(k);
            Assert.NotNull(key);
            Assert.Equal(k == "GroundEnabled" ? FogValueKind.Bool : FogValueKind.Float, key.Kind);
            Assert.False(string.IsNullOrWhiteSpace(key.Description));
            Assert.NotNull(FogSettings.Defaults(1).Get(k));
        }
    }

    [Fact]
    public void GroundKeys_ClampOnSet()
    {
        var s = FogSettings.Defaults(1);
        Assert.True(s.TrySet("GroundRate", "-1")); Assert.Equal(0f, s.GroundRate);
        Assert.True(s.TrySet("GroundRateDistance", "-1")); Assert.Equal(0f, s.GroundRateDistance);
        Assert.True(s.TrySet("GroundSize", "0")); Assert.Equal(0.01f, s.GroundSize, 5);
        Assert.True(s.TrySet("GroundGrow", "0")); Assert.Equal(0.1f, s.GroundGrow, 5);
        Assert.True(s.TrySet("GroundLifetime", "0")); Assert.Equal(0.05f, s.GroundLifetime, 5);
        Assert.True(s.TrySet("GroundAlpha", "5")); Assert.Equal(1f, s.GroundAlpha);
        Assert.True(s.TrySet("GroundRadius", "-2")); Assert.Equal(0f, s.GroundRadius);
        Assert.True(s.TrySet("GroundHeight", "9")); Assert.Equal(2f, s.GroundHeight);
        Assert.True(s.TrySet("GroundHeight", "-9")); Assert.Equal(-0.5f, s.GroundHeight);
        Assert.True(s.TrySet("GroundDrift", "-1")); Assert.Equal(0f, s.GroundDrift);
        Assert.True(s.TrySet("GroundEnabled", "off")); Assert.False(s.GroundEnabled);
        Assert.False(s.TrySet("GroundEnabled", "maybe"));
        Assert.False(s.TrySet("GroundRate", "lots"));
    }

    [Fact]
    public void GroundField_ActiveAndBudget()
    {
        var s = FogSettings.Defaults(1);   // 4/s + 2/m, 7 s
        Assert.True(s.GroundActive);
        Assert.Equal(4f + 2f * FogSettings.GroundBudgetSpeed, s.GroundBudgetRate, 4);
        Assert.Equal((4f + 2f * FogSettings.GroundBudgetSpeed) * 7f, s.LiveParticlesGround, 3);
        Assert.Equal(FogSettings.MaxParticles(s.GroundBudgetRate, 7f), s.GroundMaxParticles);
        Assert.False(s.ExceedsParticleBudget);
        Assert.False(FogSettings.Defaults(2).ExceedsParticleBudget);
        s.Rate = 0f; s.Alpha = 0f;
        Assert.False(s.InnerActive);
        Assert.True(s.GroundActive);   // independent of the inner layer
        s.GroundRate = 0f;
        Assert.True(s.GroundActive);   // distance emission alone still forms the field
        s.GroundRateDistance = 0f;
        Assert.False(s.GroundActive);
        s.GroundRate = 4f; s.GroundAlpha = 0f;
        Assert.False(s.GroundActive);
        s.GroundAlpha = 0.2f; s.GroundEnabled = false;
        Assert.False(s.GroundActive);
        Assert.Equal(0f, s.LiveParticlesGround);
        s.GroundEnabled = true; s.GroundRate = 30f; s.GroundRateDistance = 10f; s.GroundLifetime = 10f;
        Assert.True(s.ExceedsParticleBudget);
        Assert.Equal(FogSettings.ParticleHardCap, s.GroundMaxParticles);
    }

    [Fact]
    public void LookDefaults_Revision7_ResetsTier1And2Fully_AndTier1BodyOff()
    {
        Assert.Equal(7, LookDefaults.FullResetRevision);
        Assert.True(LookDefaults.ResetsTiers(3));
        Assert.True(LookDefaults.ResetsTiers(0));
        Assert.True(LookDefaults.ResetsTiers(4));
        Assert.True(LookDefaults.ResetsTiers(6));
        Assert.False(LookDefaults.ResetsTiers(7));
        Assert.Equal(new[] { 1, 2 }, LookDefaults.ResetTiers);
        Assert.Equal("Off", LookDefaults.BodyVeilModes[1]);
        Assert.Equal("Distortion", LookDefaults.BodyVeilModes[2]);
        Assert.Equal("Distortion", LookDefaults.BodyVeilModes[3]);
        // Below revision 3 the old tier I default Distortion becomes Off.
        Assert.Equal("Off", LookDefaults.MigrateBodyVeilMode(1, "Distortion", 2));
        Assert.Equal("Off", LookDefaults.MigrateBodyVeilMode(1, "Distortion", 0));
        Assert.Equal("Spirit", LookDefaults.MigrateBodyVeilMode(1, "Spirit", 2));
        Assert.Equal("Off", LookDefaults.MigrateBodyVeilMode(1, "Off", 2));
        Assert.Equal("Distortion", LookDefaults.MigrateBodyVeilMode(2, "Distortion", 2));
        Assert.Equal("Distortion", LookDefaults.MigrateBodyVeilMode(3, "Distortion", 2));
        // A revision 3 file already had Off as the default: Distortion there is the user's choice and stays.
        Assert.Equal("Distortion", LookDefaults.MigrateBodyVeilMode(1, "Distortion", 3));
    }

    [Fact]
    public void LookDefaults_Revision6_ResetsOnlyTier2OuterKeys()
    {
        Assert.Equal(7, LookDefaults.Revision);
        Assert.Equal(6, LookDefaults.OuterRingRevision);
        // Revision 4 and 5 files keep their tuning except the [Fog.Tier2] Outer* keys.
        Assert.True(LookDefaults.ResetsOuterKey(2, "OuterRadius", 5));
        Assert.True(LookDefaults.ResetsOuterKey(2, "OuterTrail", 4));
        Assert.True(LookDefaults.ResetsOuterKey(2, "OuterRotation", 5));
        Assert.True(LookDefaults.ResetsOuterKey(2, "OuterAnchors", 5));
        Assert.False(LookDefaults.ResetsOuterKey(2, "Rate", 5));
        Assert.False(LookDefaults.ResetsOuterKey(2, "GroundRate", 5));
        Assert.False(LookDefaults.ResetsOuterKey(2, "Anchor.Hips", 5));
        Assert.False(LookDefaults.ResetsOuterKey(1, "OuterEnabled", 5));
        Assert.False(LookDefaults.ResetsOuterKey(3, "OuterRadius", 5));
        Assert.False(LookDefaults.ResetsOuterKey(2, "OuterRadius", 6));
        // Every Outer* key the reset covers is a real key of the section.
        var outerKeys = 0;
        foreach (var k in FogSettings.Keys) if (LookDefaults.ResetsOuterKey(2, k.Name, 5)) outerKeys++;
        Assert.Equal(13, outerKeys);
        // Since revision 7 the full reset covers every older file, so the outer-only reset no longer runs on its own.
        Assert.True(LookDefaults.ResetsTiers(3));
        Assert.True(LookDefaults.ResetsTiers(5));
    }

    [Fact]
    public void OuterRing_DefaultsPerTier_AndRotationKey()
    {
        Assert.True(FogSettings.Defaults(1).OuterEnabled);
        var t3 = FogSettings.Defaults(3);
        Assert.False(t3.OuterEnabled);
        Assert.Equal(0f, t3.OuterOffsetY, 5);
        Assert.Equal(0.25f, FogSettings.OuterRingThickness, 5);
        Assert.Contains(FogSettings.Keys, k => k.Name == "OuterRotation" && k.Kind == FogValueKind.Float);
        Assert.Contains(FogSettings.Keys, k => k.Name == "OuterOffsetY" && k.Kind == FogValueKind.Float);

        var s = FogSettings.Defaults(2);
        Assert.Equal(3f * (float)Math.PI / 180f, s.OuterOrbitalRadPerSecond, 5);
        Assert.True(s.TrySet("OuterRotation", "-12"));
        Assert.Equal(-12f, s.OuterRotation, 5);
        Assert.Equal("-12", s.Get("OuterRotation"));
        Assert.True(s.TrySet("OuterRotation", "999"));
        Assert.Equal(FogSettings.MaxOuterRotation, s.OuterRotation, 5);
        Assert.True(s.TrySet("OuterOffsetY", "-0.35"));
        Assert.Equal("-0.35", s.Get("OuterOffsetY"));
        Assert.False(s.TrySet("OuterRotation", "fast"));
        var parsed = FogSettings.Parse(2, k => k == "OuterRotation" ? "4" : k == "OuterOffsetY" ? "0.1" : null, new List<string>());
        Assert.Equal(4f, parsed.OuterRotation, 5);
        Assert.Equal(0.1f, parsed.OuterOffsetY, 5);
    }

    [Fact]
    public void Defaults_AreMatte_AllTiers()
    {
        for (var t = 1; t <= 3; t++) Assert.Equal(0f, FogSettings.Defaults(t).Emission);
    }

    [Fact]
    public void Drift_AllowsNegative_AndRoundTrips()
    {
        var s = FogSettings.Defaults(1);
        Assert.True(s.TrySet("Drift", "-0.066"));
        Assert.Equal(-0.066f, s.Drift, 5);
        Assert.Equal("-0.066", s.Get("Drift"));
        var parsed = FogSettings.Defaults(1);
        Assert.True(parsed.TrySet("Drift", s.Get("Drift")));
        Assert.Equal(s.Drift, parsed.Drift, 5);
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
        s.Trail = true; s.OuterTrail = true; s.OuterHorizontal = false; s.MeshRate = 12.5f; s.OuterSpreadY = 0.2f; s.OuterLifetime = 2f;
        s.OuterRadius = 2.5f; s.OuterAlpha = 0.6f; s.OuterRate = 9f; s.OuterSize = 2.2f; s.Emission = 0.4f;
        s.OuterAnchors = new List<string> { "LeftFoot", "Head" };
        s.GroundEnabled = true; s.GroundRate = 3f; s.GroundRateDistance = 1.5f; s.GroundSize = 1.2f; s.GroundGrow = 2.5f;
        s.GroundLifetime = 5f; s.GroundAlpha = 0.4f; s.GroundRadius = 0.7f; s.GroundHeight = 0.3f; s.GroundDrift = 0.1f;
        var text = new Dictionary<string, string>();
        foreach (var k in FogSettings.Keys) text[k.Name] = s.Get(k.Name);
        Assert.Equal("LeftFoot,Head", text["OuterAnchors"]);
        Assert.Equal("true", text["Trail"]);

        var warnings = new List<string>();
        var parsed = FogSettings.Parse(3, k => text.TryGetValue(k, out var v) ? v : null, warnings);

        Assert.Empty(warnings);
        Assert.True(parsed.Trail);
        Assert.True(parsed.OuterTrail);
        Assert.False(parsed.OuterHorizontal);
        Assert.Equal(12.5f, parsed.MeshRate, 5);
        Assert.Equal(0.2f, parsed.OuterSpreadY, 5);
        Assert.Equal(2f, parsed.OuterLifetime, 5);
        Assert.Equal(2.5f, parsed.OuterRadius, 5);
        Assert.Equal(0.6f, parsed.OuterAlpha, 5);
        Assert.Equal(9f, parsed.OuterRate, 5);
        Assert.Equal(2.2f, parsed.OuterSize, 5);
        Assert.Equal(0.4f, parsed.Emission, 5);
        Assert.Equal(new[] { "LeftFoot", "Head" }, parsed.OuterAnchors);
        Assert.True(parsed.GroundEnabled);
        Assert.Equal(3f, parsed.GroundRate, 5);
        Assert.Equal(1.5f, parsed.GroundRateDistance, 5);
        Assert.Equal(1.2f, parsed.GroundSize, 5);
        Assert.Equal(2.5f, parsed.GroundGrow, 5);
        Assert.Equal(5f, parsed.GroundLifetime, 5);
        Assert.Equal(0.4f, parsed.GroundAlpha, 5);
        Assert.Equal(0.7f, parsed.GroundRadius, 5);
        Assert.Equal(0.3f, parsed.GroundHeight, 5);
        Assert.Equal(0.1f, parsed.GroundDrift, 5);
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
        var s = FogSettings.Defaults(2);   // outer rate 10, outer life 6
        Assert.Equal(60f, s.LiveParticlesOuter, 3);
        s.Rate = 0f;   // independent of the inner rate
        Assert.Equal(60f, s.LiveParticlesOuter, 3);
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
        mesh.MeshRate = 120f;   // x lifetime 2 s = 240
        Assert.True(mesh.ExceedsParticleBudget);
    }

    [Fact]
    public void NewLookKeys_DefaultsAndRoundTrip()
    {
        var s = FogSettings.Defaults(2);
        Assert.Equal("soft", s.Get("FogMaterial"));
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

public class FloatListNonFiniteTests
{
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void TryParseOne_RejectsNonFinite(string text)
    {
        Assert.False(FloatList.TryParseOne(text, out _));
        Assert.False(FloatList.TryParse("1," + text, 2, out _));
    }

    [Fact]
    public void SoftFogMaterial_IsTheDefaultOfEveryTier()
    {
        Assert.Equal("soft", FogSettings.DefaultFogMaterial);
        Assert.Contains("soft", FogSettings.FogMaterialNames);
        Assert.Equal("soft", FogSettings.NormalizeFogMaterial(" SOFT "));
        for (var t = 1; t <= 3; t++) Assert.Equal("soft", FogSettings.Defaults(t).FogMaterial);
        // soft is a swamp_mist copy with a generated texture: it is not borrowed from another prefab.
        Assert.Equal("swamp_mist", FogSettings.SoftFogBase);
    }

    [Fact]
    public void LookDefaults_Revision5_MovesSwampMistToSoftOnly()
    {
        Assert.Equal("soft", LookDefaults.MigrateFogMaterial("swamp_mist", 4));
        Assert.Equal("soft", LookDefaults.MigrateFogMaterial(" Swamp_Mist ", 0));
        Assert.Equal("ghost_smoke", LookDefaults.MigrateFogMaterial("ghost_smoke", 4));
        Assert.Equal("swamp_mist", LookDefaults.MigrateFogMaterial("swamp_mist", 5));   // chosen after revision 5: the user's choice
        Assert.Equal("soft", LookDefaults.MigrateFogMaterial("soft", 4));
    }

    [Fact]
    public void GroundField_TwoHeightLayers()
    {
        var s = FogSettings.Defaults(1);
        Assert.Equal(0.7f, FogSettings.GroundSizeMinFactor, 5);
        Assert.Equal(1.3f, FogSettings.GroundSizeMaxFactor, 5);
        Assert.Equal(0.15f, s.GroundHeight, 5);
        Assert.Equal(0.45f, s.GroundUpperHeight, 5);   // GroundHeight + GroundUpperOffset
        Assert.Equal(0.5f, FogSettings.GroundUpperRateFactor, 5);
        Assert.Equal(0.7f, FogSettings.GroundUpperAlphaFactor, 5);
        Assert.Equal(s.GroundAlpha * 0.7f, s.GroundUpperAlpha, 5);
        Assert.Equal(s.GroundRate * 0.5f, s.GroundUpperRate, 5);
        Assert.Equal(s.GroundRateDistance * 0.5f, s.GroundUpperRateDistance, 5);
        s.GroundHeight = 1f;
        Assert.Equal(1.3f, s.GroundUpperHeight, 5);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GroundField_BothLayersStayWithinTheBudget(int tier)
    {
        var s = FogSettings.Defaults(tier);
        s.GroundEnabled = true;
        Assert.True(s.GroundLowerMaxParticles + s.GroundUpperMaxParticles <= FogSettings.GroundParticleBudget);
        Assert.Equal(300, FogSettings.GroundParticleBudget);
        Assert.True(s.GroundUpperMaxParticles >= 4);
        Assert.True(s.GroundLowerMaxParticles >= s.GroundUpperMaxParticles);
        // Expected live particles of each layer while running fit into its share (no clipping at the defaults).
        var lowerLive = s.GroundBudgetRate * s.GroundLifetime;
        Assert.True(lowerLive <= s.GroundLowerMaxParticles, $"T{tier}: lower live {lowerLive} > {s.GroundLowerMaxParticles}");
        Assert.True(lowerLive * FogSettings.GroundUpperRateFactor <= s.GroundUpperMaxParticles);
    }

    [Fact]
    public void GroundField_HugeRatesAreCappedAcrossBothLayers()
    {
        var s = FogSettings.Defaults(2);
        s.GroundRate = 100f; s.GroundRateDistance = 50f; s.GroundLifetime = 20f;
        Assert.Equal(FogSettings.GroundParticleBudget, s.GroundLowerMaxParticles + s.GroundUpperMaxParticles);
        s.GroundRate = 0.01f; s.GroundRateDistance = 0f; s.GroundLifetime = 0.05f;
        Assert.True(s.GroundLowerMaxParticles >= 8);
        Assert.True(s.GroundUpperMaxParticles >= 4);
    }

    [Fact]
    public void OuterShape_KeyParsesAndRoundTrips()
    {
        Assert.Contains(FogSettings.Keys, k => k.Name == "OuterShape" && k.Kind == FogValueKind.Text);
        var s = FogSettings.Defaults(2);
        Assert.Equal("Volume", s.Get("OuterShape"));
        Assert.True(s.TrySet("OuterShape", " ring "));
        Assert.Equal(FogOuterShape.Ring, s.OuterShape);
        Assert.Equal("Ring", s.Get("OuterShape"));
        Assert.False(s.TrySet("OuterShape", "Cube"));
        Assert.False(s.TrySet("OuterShape", "5"));
        Assert.Equal(FogOuterShape.Ring, s.OuterShape);
        Assert.Equal(FogOuterShape.Volume, FogSettings.Defaults(3).OuterShape);
        var parsed = FogSettings.Parse(1, k => k == "OuterShape" ? "Ring" : null, new List<string>());
        Assert.Equal(FogOuterShape.Ring, parsed.OuterShape);
        Assert.Equal(FogOuterShape.Ring, parsed.Clone().OuterShape);
    }

    [Fact]
    public void OuterVolume_LookConstants()
    {
        Assert.Equal(0.8f, FogSettings.OuterVolumeSizeMinFactor, 5);
        Assert.Equal(1.2f, FogSettings.OuterVolumeSizeMaxFactor, 5);
        Assert.Equal(0.03f, FogSettings.OuterVolumeSpeed, 5);
        Assert.Equal(0.2f, FogSettings.OuterVolumeFadeIn, 5);
        // Height of the flattened volume: about +-1 m around the anchor at the tier II defaults.
        var t2 = FogSettings.Defaults(2);
        Assert.InRange(t2.OuterRadius * t2.OuterSpreadY, 0.9f, 1.1f);
        Assert.False(t2.ExceedsParticleBudget);
        Assert.False(FogSettings.Defaults(1).ExceedsParticleBudget);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Tier1And2_WholeBodyCloud_AllAnchorsOn_LimbRadiusAtLeast012(int tier)
    {
        var s = FogSettings.Defaults(tier);
        Assert.Equal(FogSettings.AnchorNames.Length, s.Anchors.Count);
        Assert.All(s.Anchors, a => Assert.True(a.Enabled, a.Name));
        Assert.All(s.Anchors, a => Assert.True(a.Radius >= 0.12f - 1e-5f, $"{a.Name} radius {a.Radius}"));
        Assert.Equal(0.6f, s.SpreadY, 5);
        Assert.Equal(0f, s.Emission);
        Assert.True(s.GroundEnabled);
    }

    [Fact]
    public void Tier3_Untouched()
    {
        var t3 = FogSettings.Defaults(3);
        Assert.False(t3.Enabled);
        Assert.False(t3.OuterEnabled);
        Assert.False(t3.GroundEnabled);
        Assert.Equal(4f, t3.Rate, 5);
        Assert.Equal(1.5f, t3.Size, 5);
        Assert.Equal(1.4f, t3.OuterRadius, 5);
        Assert.True(t3.OuterHorizontal);
    }

    [Fact]
    public void FogSprite_FalloffIsSoftAndReachesZeroAtTheEdge()
    {
        Assert.Equal(FogSprite.CentreAlpha, FogSprite.Falloff(0f), 5);
        Assert.True(FogSprite.CentreAlpha <= 0.8f);
        Assert.Equal(0f, FogSprite.Falloff(1f), 5);
        Assert.Equal(0f, FogSprite.Falloff(1.4f), 5);
        // Monotonic and gaussian-like: well below linear at half radius.
        var prev = float.MaxValue;
        for (var i = 0; i <= 100; i++)
        {
            var a = FogSprite.Falloff(i / 100f);
            Assert.True(a <= prev + 1e-6f);
            Assert.InRange(a, 0f, FogSprite.CentreAlpha);
            prev = a;
        }
        Assert.True(FogSprite.Falloff(0.5f) < 0.5f * FogSprite.CentreAlpha);
        Assert.True(FogSprite.Falloff(0.9f) < 0.05f);
    }

    [Fact]
    public void FogSprite_PixelsAreBoundedNoisyAndDeterministic()
    {
        const int n = FogSprite.Size;
        float min = float.MaxValue, max = float.MinValue;
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
        {
            var a = FogSprite.Alpha(x, y, n);
            Assert.InRange(a, 0f, FogSprite.CentreAlpha);
            Assert.Equal(a, FogSprite.Alpha(x, y, n));
            var noise = FogSprite.Noise(x, y, n);
            Assert.InRange(noise, -1f, 1f);
            if (noise < min) min = noise;
            if (noise > max) max = noise;
            var r = FogSprite.Radius(x, y, n);
            var f = FogSprite.Falloff(r);
            // Noise is at most +-NoiseAmount of the falloff (the centre is clamped to CentreAlpha).
            Assert.True(a >= f * (1f - FogSprite.NoiseAmount) - 1e-5f);
            Assert.True(a <= f * (1f + FogSprite.NoiseAmount) + 1e-5f);
        }
        Assert.True(max - min > 0.5f, "noise should vary across the sprite");
        // Border pixels (row 0, column 0) and corners are fully transparent.
        for (var i = 0; i < n; i++)
        {
            Assert.Equal(0f, FogSprite.Alpha(i, 0, n), 3);
            Assert.Equal(0f, FogSprite.Alpha(0, i, n), 3);
        }
        Assert.Equal(0f, FogSprite.Alpha(n - 1, n - 1, n));
    }
}
