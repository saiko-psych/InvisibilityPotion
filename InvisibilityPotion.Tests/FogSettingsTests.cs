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
        // Round M: the volume is emitted into the world and left behind (world space, no turn), faint.
        Assert.True(t1.OuterEnabled);
        Assert.Equal(FogOuterShape.Volume, t1.OuterShape);
        Assert.Equal(new[] { "Hips" }, t1.OuterAnchors);
        // Round P: a strong, wide volume that exists the moment the effect starts (OuterBurst).
        Assert.Equal(0.3f, t1.OuterAlpha, 5);   // round T: subtle (round S 0.35)
        Assert.Equal(7f, t1.OuterRadius, 5);   // round T: much wider (round S 3.5)
        Assert.Equal(0.8f, t1.OuterHeightSigma, 5);   // round T (round S 0.9)
        Assert.Equal(4.5f, t1.OuterSize, 5);   // round T: larger, softer puffs (round S 3.6)
        Assert.Equal(0.35f, t1.OuterSpreadY, 5);
        Assert.Equal(8f, t1.OuterRate, 5);
        Assert.Equal(3f, t1.OuterRateDistance, 5);
        Assert.Equal(12f, t1.OuterLifetime, 5);   // round T (was 9)
        Assert.Equal(60f, t1.OuterBurst, 5);   // round T (round R 50)
        Assert.Equal(0.4f, t1.OuterFollowShare, 5);
        Assert.Equal(0f, t1.OuterRotation, 5);
        Assert.Equal(0f, t1.OuterOffsetY, 5);
        Assert.False(t1.OuterHorizontal);
        Assert.True(t1.OuterTrail);
        Assert.Equal(FogTrailMode.Trail, t1.OuterTrailMode);
        Assert.Equal(FogEmitterMode.Bones, t1.EmitterMode);
        Assert.Equal(0f, t1.MeshRate);
        Assert.Equal(7f, t1.Rate, 5);
        Assert.Equal(1.1f, t1.Size, 5);
        Assert.Equal(2.4f, t1.Lifetime, 5);
        Assert.Equal(0.12f, t1.Alpha, 5);   // round S (was 0.16)
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
        Assert.Equal(0.14f, t1.GroundAlpha, 5);
        Assert.Equal(0.6f, t1.GroundRadius, 5);
        Assert.Equal(0.15f, t1.GroundHeight, 5);
        Assert.Equal(0.05f, t1.GroundDrift, 5);
        Assert.All(t1.Anchors, a => Assert.True(a.Enabled));
        Assert.Equal("0.55,0.57,0.6", t1.Get("Color"));   // round M: mid grey, darker than the sky (haze, never light)
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
        Assert.Equal(9f, t2.Rate, 5);
        Assert.Equal(1.2f, t2.Size, 5);
        Assert.Equal(2.6f, t2.Lifetime, 5);
        Assert.Equal(0.05f, t2.Speed, 5);
        Assert.Equal(0.14f, t2.Alpha, 5);   // round S: the body silhouette stays visible (was 0.22)
        Assert.Equal(1.125f, t2.SpreadX, 5);
        Assert.Equal(0.6f, t2.SpreadY, 5);
        Assert.Equal(1.025f, t2.SpreadZ, 5);
        Assert.Equal(-0.066f, t2.Drift, 5);
        Assert.Equal("0.55,0.57,0.6", t2.Get("Color"));
        Assert.False(t2.DynamicColor);
        Assert.Equal(0f, t2.Emission);
        Assert.True(t2.OuterEnabled);
        // Round M ruling: the fog volume is emitted into the world (world space) and left behind.
        Assert.True(t2.OuterTrail);
        Assert.Equal(FogTrailMode.Trail, t2.OuterTrailMode);
        Assert.Equal(FogOuterShape.Volume, t2.OuterShape);
        Assert.Equal(new[] { "Hips" }, t2.OuterAnchors);
        // Round P: tier II stronger and wider than tier I.
        // Round S: lighter and wider at the ground (alpha 0.45, radius 4.5, size 3.6, burst 60).
        // Round T: much wider and subtle (radius 9, alpha 0.4, sigma 1, size 5.5, lifetime 14, burst 80).
        Assert.Equal(9f, t2.OuterRadius, 5);
        Assert.Equal(0.4f, t2.OuterAlpha, 5);
        Assert.Equal(1f, t2.OuterHeightSigma, 5);
        Assert.Equal(10f, t2.OuterRate, 5);
        Assert.Equal(4f, t2.OuterRateDistance, 5);
        Assert.Equal(5.5f, t2.OuterSize, 5);
        Assert.Equal(14f, t2.OuterLifetime, 5);
        Assert.Equal(80f, t2.OuterBurst, 5);   // round T
        Assert.Equal(0.4f, t2.OuterFollowShare, 5);
        Assert.Equal(0.35f, t2.OuterSpreadY, 5);
        Assert.Equal(0f, t2.OuterOffsetY, 5);
        Assert.Equal(0f, t2.OuterRotation, 5);
        Assert.False(t2.OuterHorizontal);
        Assert.True(t2.GroundEnabled);
        Assert.Equal(6f, t2.GroundRate, 5);
        Assert.Equal(3f, t2.GroundRateDistance, 5);
        Assert.Equal(1.2f, t2.GroundSize, 5);
        Assert.Equal(2.5f, t2.GroundGrow, 5);
        Assert.Equal(7f, t2.GroundLifetime, 5);
        Assert.Equal(0.16f, t2.GroundAlpha, 5);
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
        Assert.Equal(0f, t3.OuterBurst);
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
    public void LookDefaults_FullResetRevision_ResetsTier1And2Fully_AndTier1BodyOff()
    {
        Assert.Equal(LookDefaults.Revision, LookDefaults.FullResetRevision);
        Assert.True(LookDefaults.ResetsTiers(3));
        Assert.True(LookDefaults.ResetsTiers(0));
        Assert.True(LookDefaults.ResetsTiers(4));
        Assert.True(LookDefaults.ResetsTiers(6));
        Assert.True(LookDefaults.ResetsTiers(LookDefaults.FullResetRevision - 1));
        Assert.False(LookDefaults.ResetsTiers(LookDefaults.FullResetRevision));
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
        Assert.Equal(LookDefaults.FullResetRevision, LookDefaults.Revision);
        Assert.True(LookDefaults.OuterRingRevision < LookDefaults.FullResetRevision);
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
        Assert.Equal(17, outerKeys);   // round P: OuterBurst; round Q: OuterFollowShare; round S: OuterHeightSigma
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
        Assert.Equal(0f, s.OuterOrbitalRadPerSecond, 5);   // round M: the world-space volume does not turn
        s.OuterRotation = 3f;
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
        s.OuterRadius = 2.5f; s.OuterAlpha = 0.6f; s.OuterRate = 9f; s.OuterSize = 2.2f; s.Emission = 0.4f; s.OuterRateDistance = 1.5f;
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
        Assert.Equal(1.5f, parsed.OuterRateDistance, 5);
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
        var s = FogSettings.Defaults(2);   // outer rate 10, outer life 14 (round T)
        Assert.Equal(140f, s.LiveParticlesOuter, 3);
        s.Rate = 0f;   // independent of the inner rate
        Assert.Equal(140f, s.LiveParticlesOuter, 3);
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

    [Fact]
    public void LookDefaults_Revision14_FullReset()
    {
        Assert.Equal(14, LookDefaults.Revision);
        Assert.Equal(LookDefaults.Revision, LookDefaults.FullResetRevision);
        Assert.True(LookDefaults.ResetsTiers(13));
        Assert.False(LookDefaults.ResetsTiers(14));
    }

    [Fact]
    public void OuterBurst_KeyClampsRoundTripsAndParses()
    {
        var key = Find("OuterBurst");
        Assert.NotNull(key);
        Assert.Equal(FogValueKind.Float, key.Kind);
        var s = FogSettings.Defaults(2);
        Assert.Equal("80", s.Get("OuterBurst"));   // round T
        Assert.True(s.TrySet("OuterBurst", "-3")); Assert.Equal(0f, s.OuterBurst);
        Assert.True(s.TrySet("OuterBurst", "12")); Assert.Equal(12f, s.OuterBurst, 5);
        Assert.False(s.TrySet("OuterBurst", "lots"));
        Assert.Equal(12f, s.Clone().OuterBurst, 5);
        var parsed = FogSettings.Parse(1, k => k == "OuterBurst" ? "7" : null, new List<string>());
        Assert.Equal(7f, parsed.OuterBurst, 5);
    }

    [Fact]
    public void OuterBurstCount_RoundedCappedVolumeOnly()
    {
        var s = FogSettings.Defaults(2);
        Assert.Equal(80, s.OuterBurstCount);   // round T
        Assert.Equal(60, FogSettings.Defaults(1).OuterBurstCount);
        Assert.Equal(0, FogSettings.Defaults(3).OuterBurstCount);
        s.OuterBurst = 12.6f;
        Assert.Equal(13, s.OuterBurstCount);
        Assert.Equal(s.OuterFollowBurstCount + s.OuterTrailBurstCount, s.OuterBurstCount);
        // Never more than the emitter can hold.
        s.OuterBurst = 500f;
        Assert.Equal(s.OuterMaxParticles, s.OuterBurstCount);
        // The burst is a volume feature: the ring builds up as before.
        s.OuterShape = FogOuterShape.Ring;
        Assert.Equal(0, s.OuterBurstCount);
        // An inactive outer layer emits nothing.
        var off = FogSettings.Defaults(2);
        off.OuterEnabled = false;
        Assert.Equal(0, off.OuterBurstCount);
        // The defaults fit below the volume cap.
        Assert.True(FogSettings.Defaults(2).OuterBurstCount <= FogSettings.Defaults(2).OuterMaxParticles);
    }

    [Fact]
    public void OuterBurstRemaining_IsThirtyToEightyFivePercentOfStartLifetime()
    {
        // Round Q ruling 1a: burst puffs start already aged, past the fade-in, so they are at full alpha in the first frame.
        // Round T: the fade-in is 15 %, so the youngest burst puff has 85 % left (was 90 %).
        Assert.Equal(0.3f, FogSettings.OuterBurstRemainingMinFactor, 5);
        Assert.Equal(0.85f, FogSettings.OuterBurstRemainingMaxFactor, 5);
        Assert.Equal(2.7f, FogSettings.OuterBurstRemainingLifetime(9f, 0f), 4);
        Assert.Equal(7.65f, FogSettings.OuterBurstRemainingLifetime(9f, 1f), 4);
        Assert.Equal(2.7f, FogSettings.OuterBurstRemainingLifetime(9f, -2f), 4);   // clamped
        Assert.Equal(7.65f, FogSettings.OuterBurstRemainingLifetime(9f, 7f), 4);
        // The youngest burst puff (remaining 85 %) is already 15 % into its life, i.e. at the end of the fade-in.
        Assert.True(1f - FogSettings.OuterBurstRemainingMaxFactor >= FogSettings.OuterVolumeFadeIn - 1e-5f);
    }

    [Fact]
    public void OuterFollowShare_KeyClampsRoundTripsAndParses()
    {
        var key = Find("OuterFollowShare");
        Assert.NotNull(key);
        Assert.Equal(FogValueKind.Float, key.Kind);
        Assert.Contains("moves with you", key.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("trail", key.Description, StringComparison.OrdinalIgnoreCase);
        var s = FogSettings.Defaults(2);
        Assert.Equal("0.4", s.Get("OuterFollowShare"));
        Assert.True(s.TrySet("OuterFollowShare", "-1")); Assert.Equal(0f, s.OuterFollowShare);
        Assert.True(s.TrySet("OuterFollowShare", "3")); Assert.Equal(1f, s.OuterFollowShare);
        Assert.True(s.TrySet("OuterFollowShare", "0.25")); Assert.Equal(0.25f, s.OuterFollowShare, 5);
        Assert.False(s.TrySet("OuterFollowShare", "half"));
        Assert.Equal(0.25f, s.Clone().OuterFollowShare, 5);
        Assert.Equal(0.7f, FogSettings.Parse(1, k => k == "OuterFollowShare" ? "0.7" : null, new List<string>()).OuterFollowShare, 5);
    }

    [Fact]
    public void OuterSplit_FollowAndTrailShareRateCapAndBurst()
    {
        // Round Q ruling 1b/c, round R ruling A3: tier II defaults: rate 10, burst 80 (round T), share 0.4, cap 200 (round T)
        // shared 80/120; the follow part gets 60 % of the burst (48) and holds its burst on top of its rate budget.
        Assert.Equal(200, FogSettings.OuterVolumeParticleCap);
        Assert.Equal(0.7f, FogSettings.OuterFollowRadiusFactor, 5);
        Assert.Equal(0.6f, FogSettings.OuterFollowBurstFraction, 5);
        var s = FogSettings.Defaults(2);
        Assert.Equal(0.4f, s.OuterFollowShareEffective, 5);
        Assert.True(s.OuterHasFollow);
        Assert.True(s.OuterHasTrail);
        Assert.Equal(4f, s.OuterFollowRate, 4);
        Assert.Equal(6f, s.OuterTrailRate, 4);
        Assert.Equal(6.3f, s.OuterFollowRadius, 4);
        Assert.Equal(48, s.OuterFollowBurstCount);
        Assert.Equal(32, s.OuterTrailBurstCount);
        Assert.Equal(Math.Min(80, FogSettings.MaxParticles(4f, 14f) + 48), s.OuterFollowMaxParticles);
        Assert.Equal(Math.Min(120, FogSettings.MaxParticles(6f + 4f * FogSettings.GroundBudgetSpeed, 14f)), s.OuterTrailMaxParticles);
        Assert.True(s.OuterFollowBurstCount <= s.OuterFollowMaxParticles);
        Assert.True(s.OuterFollowMaxParticles + s.OuterTrailMaxParticles <= FogSettings.OuterVolumeParticleCap);
        Assert.Equal(s.OuterFollowMaxParticles + s.OuterTrailMaxParticles, s.OuterMaxParticles);
        // Tier I: burst 60 -> 36 follow + 24 trail.
        var t1 = FogSettings.Defaults(1);
        Assert.Equal(36, t1.OuterFollowBurstCount);
        Assert.Equal(24, t1.OuterTrailBurstCount);
        Assert.True(t1.OuterFollowBurstCount <= t1.OuterFollowMaxParticles);
        // Share 0: one trail system as in round P (whole rate, whole cap, whole burst).
        s.OuterFollowShare = 0f;
        Assert.False(s.OuterHasFollow);
        Assert.Equal(0, s.OuterFollowMaxParticles);
        Assert.Equal(0, s.OuterFollowBurstCount);
        Assert.Equal(10f, s.OuterTrailRate, 4);
        Assert.Equal(Math.Min(200, FogSettings.MaxParticles(10f + 4f * FogSettings.GroundBudgetSpeed, 14f)), s.OuterTrailMaxParticles);
        Assert.Equal(80, s.OuterTrailBurstCount);
        // Share 1: everything follows, no trail system.
        s.OuterFollowShare = 1f;
        Assert.True(s.OuterHasFollow);
        Assert.False(s.OuterHasTrail);
        Assert.Equal(0, s.OuterTrailMaxParticles);
        Assert.Equal(0, s.OuterTrailBurstCount);
        Assert.Equal(80, s.OuterFollowBurstCount);   // the whole burst follows
        Assert.Equal(10f, s.OuterFollowRate, 4);
        // Without OuterTrail the whole volume already moves with the player: no split, one system.
        var local = FogSettings.Defaults(2);
        local.OuterTrail = false;
        Assert.Equal(0f, local.OuterFollowShareEffective);
        Assert.False(local.OuterHasFollow);
        Assert.Equal(10f, local.OuterTrailRate, 4);
        // The ring is never split.
        var ring = FogSettings.Defaults(2);
        ring.OuterShape = FogOuterShape.Ring;
        Assert.Equal(0f, ring.OuterFollowShareEffective);
        Assert.Equal(FogSettings.MaxParticles(ring.OuterRate, ring.OuterLifetime), ring.OuterTrailMaxParticles);
        // An inactive outer layer has no systems.
        var off = FogSettings.Defaults(2);
        off.OuterEnabled = false;
        Assert.False(off.OuterHasFollow);
        Assert.False(off.OuterHasTrail);
    }

    [Fact]
    public void OuterFollow_SmallerPuffsAndSlowOrbit()
    {
        // Round R ruling A3/A4: follow puffs 0.85 x OuterSize; they orbit at 0.15 m/s at the reference radius (0.65 x follow radius).
        Assert.Equal(0.85f, FogSettings.OuterFollowSizeFactor, 5);
        Assert.Equal(0.15f, FogSettings.OuterFollowTangentialSpeed, 5);
        var s = FogSettings.Defaults(2);
        Assert.Equal(5.5f * 0.85f, s.OuterFollowSize, 4);
        var reference = s.OuterFollowRadius * FogSettings.OuterFollowOrbitRadiusFactor;
        Assert.Equal(0.15f / reference, s.OuterFollowOrbitalRadPerSecond, 4);
        Assert.Equal(0.15f, s.OuterFollowOrbitalRadPerSecond * reference, 4);
        // OuterRotation adds on top (deg/s -> rad/s).
        s.OuterRotation = 90f;
        Assert.Equal(0.15f / reference + (float)Math.PI / 2f, s.OuterFollowOrbitalRadPerSecond, 4);
        // A degenerate radius gives no drift instead of infinity.
        s.OuterRotation = 0f; s.OuterRadius = 0f;
        Assert.Equal(0f, s.OuterFollowOrbitalRadPerSecond);
    }

    [Fact]
    public void VolumeShape_RadiusDensityIsAGaussianAroundThePlayer()
    {
        // Round T ruling 2: ground-area density exp(-(r / sigma_r)^2 / 2) with sigma_r = 0.45 R (vanilla-like soft patch), cut at R.
        const float radius = 7f;
        Assert.Equal(0.45f, FogVolumeShape.RadialSigmaFactor, 5);
        var sr = 0.45 * radius;
        Assert.Equal(1f, FogVolumeShape.AreaDensity(0f, radius), 5);
        Assert.Equal((float)Math.Exp(-0.5), FogVolumeShape.AreaDensity((float)sr, radius), 4);   // one sigma
        Assert.Equal((float)Math.Exp(-0.5 / (0.45 * 0.45)), FogVolumeShape.AreaDensity(radius, radius), 4);   // 0.085 at R
        Assert.Equal(0f, FogVolumeShape.AreaDensity(radius * 1.01f, radius), 5);
        Assert.Equal(0f, FogVolumeShape.AreaDensity(1f, 0f), 5);
        var prevDensity = 2f;
        for (var i = 0; i <= 100; i++)
        {
            var d = FogVolumeShape.AreaDensity(radius * i / 100f, radius);
            Assert.True(d < prevDensity, $"AreaDensity not falling at {i}");
            prevDensity = d;
        }
        // Share within x = r/R: truncated Rayleigh, 0 at the player, 1 at R; RadiusAt inverts it.
        Assert.Equal(0.0, FogVolumeShape.RadiusShare(0), 6);
        Assert.Equal(1.0, FogVolumeShape.RadiusShare(1), 6);
        var cut = 1 - Math.Exp(-0.5 / (0.45 * 0.45));
        Assert.Equal((1 - Math.Exp(-0.5)) / cut, FogVolumeShape.RadiusShare(0.45), 5);
        Assert.Equal(0f, FogVolumeShape.RadiusAt(0f, radius), 5);
        Assert.Equal(radius, FogVolumeShape.RadiusAt(1f, radius), 3);
        foreach (var u in new[] { 0.1f, 0.37f, 0.5f, 0.9f })
            Assert.Equal(u, (float)FogVolumeShape.RadiusShare(FogVolumeShape.RadiusAt(u, radius) / radius), 4);
        var prev = -1f;
        for (var i = 0; i <= 100; i++)
        {
            var r = FogVolumeShape.RadiusAt(i / 100f, radius);
            Assert.True(r >= prev, $"RadiusAt not monotonic at {i}");
            Assert.InRange(r, 0f, radius + 1e-4f);
            prev = r;
        }
        // A stratified sample: puffs per square metre fall ring by ring from the player to the rim, and the outer ring keeps a
        // thin share (the cloud thins out instead of ending), unlike the round S (1 - r/R)^2 profile that reached 0.
        const int n = 20000; const int rings = 7;
        var counts = new int[rings];
        for (var i = 0; i < n; i++)
        {
            var r = FogVolumeShape.RadiusAt((i + 0.5f) / n, radius);
            counts[Math.Min(rings - 1, (int)(r / radius * rings))]++;
        }
        var lastDensity = double.MaxValue;
        var densities = new double[rings];
        for (var k = 0; k < rings; k++)
        {
            double r0 = radius * k / rings, r1 = radius * (k + 1) / rings;
            densities[k] = counts[k] / (Math.PI * (r1 * r1 - r0 * r0));
            Assert.True(densities[k] < lastDensity, $"ring {k}: {densities[k]} per m2 is not below {lastDensity}");
            lastDensity = densities[k];
        }
        Assert.InRange(densities[rings - 1] / densities[0], 0.08, 0.2);
        // Wider than round S: the inner half of the radius (a quarter of the area) holds about half of the puffs (round S: 69 %).
        var innerHalf = 0;
        for (var i = 0; i < n; i++) if (FogVolumeShape.RadiusAt((i + 0.5f) / n, radius) < radius / 2f) innerHalf++;
        Assert.InRange(innerHalf / (double)n, 0.45, 0.6);
        Assert.Equal(0f, FogVolumeShape.RadiusAt(0.7f, 0f));
    }

    [Fact]
    public void VolumeShape_HeightIsAHalfGaussianAboveTheGround()
    {
        const float sigma = 0.9f;
        Assert.Equal(0.9f, FogVolumeShape.DefaultHeightSigma, 5);
        Assert.Equal(0.1f, FogVolumeShape.HeightFloor, 5);
        // Floor: never below 0.1 m, also for u = 0 and a tiny sigma.
        Assert.Equal(0.1f, FogVolumeShape.HeightAt(0f, sigma), 5);
        Assert.True(FogVolumeShape.HeightAt(0.5f, 0.01f) >= 0.1f);
        // Monotonic in u, capped at 3 sigma.
        var prev = 0f;
        for (var i = 0; i <= 100; i++)
        {
            var h = FogVolumeShape.HeightAt(i / 100f, sigma);
            Assert.True(h >= prev - 1e-6f, $"HeightAt not monotonic at {i}");
            Assert.InRange(h, 0.1f, 3f * sigma + 1e-4f);
            prev = h;
        }
        // Median of a half-normal: 0.6745 sigma; 68 % below one sigma.
        Assert.Equal(0.6745f * sigma, FogVolumeShape.HeightAt(0.5f, sigma), 2);
        Assert.Equal(sigma, FogVolumeShape.HeightAt(0.6827f, sigma), 2);
        // Frequent near the ground, rare up high: counts per 0.3 m band fall with height.
        const int n = 20000; const int bands = 6; const float band = 0.3f;
        var counts = new int[bands];
        for (var i = 0; i < n; i++)
        {
            var h = FogVolumeShape.HeightAt((i + 0.5f) / n, sigma);
            var k = (int)(h / band);
            if (k < bands) counts[k]++;
        }
        for (var k = 1; k < bands; k++) Assert.True(counts[k] < counts[k - 1], $"band {k}: {counts[k]} not below {counts[k - 1]}");
        // The density itself falls with height.
        Assert.True(FogVolumeShape.HeightDensity(0.2f, sigma) > FogVolumeShape.HeightDensity(1f, sigma));
        Assert.True(FogVolumeShape.HeightDensity(1f, sigma) > FogVolumeShape.HeightDensity(2f, sigma));
        Assert.Equal(1f, FogVolumeShape.HeightDensity(0f, sigma), 5);
    }

    [Fact]
    public void VolumeShape_SizeGrowsWithHeightAndDistance_AlphaFallsLikeAGaussian()
    {
        const float sigma = 0.8f, radius = 7f;
        // Height: 0.7 x at the ground, 1.0 x from two sigma up, linear in between (round S, kept).
        Assert.Equal(0.7f, FogVolumeShape.SizeFactor(0f, sigma), 5);
        Assert.Equal(0.85f, FogVolumeShape.SizeFactor(sigma, sigma), 5);
        Assert.Equal(1f, FogVolumeShape.SizeFactor(2f * sigma, sigma), 5);
        Assert.Equal(1f, FogVolumeShape.SizeFactor(5f, sigma), 5);
        // Round T ruling 2: distance: 0.8 x at the player (smaller, denser) to 1.2 x at R (larger, fainter), monotonic.
        Assert.Equal(0.8f, FogVolumeShape.RadialSizeFactor(0f, radius), 5);
        Assert.Equal(1.2f, FogVolumeShape.RadialSizeFactor(radius, radius), 5);
        Assert.Equal(1f, FogVolumeShape.RadialSizeFactor(radius / 2f, radius), 5);
        Assert.Equal(1.2f, FogVolumeShape.RadialSizeFactor(radius * 2f, radius), 5);
        Assert.Equal(1f, FogVolumeShape.RadialSizeFactor(1f, 0f), 5);
        // Alpha: alpha0 x exp(-(r / sigma_r)^2 / 2), lifted so it is exactly the 0.15 floor at R (dissolves, never ends at 0).
        Assert.Equal(0.15f, FogVolumeShape.AlphaAtRim, 5);
        Assert.Equal(1f, FogVolumeShape.AlphaFactor(0f, radius), 5);
        Assert.Equal(0.15f, FogVolumeShape.AlphaFactor(radius, radius), 4);
        Assert.Equal(0.15f, FogVolumeShape.AlphaFactor(radius * 3f, radius), 4);
        Assert.Equal(1f, FogVolumeShape.AlphaFactor(1f, 0f), 5);
        var gR = Math.Exp(-0.5 / (0.45 * 0.45));
        var atSigma = 0.15 + 0.85 * (Math.Exp(-0.5) - gR) / (1 - gR);
        Assert.Equal((float)atSigma, FogVolumeShape.AlphaFactor(0.45f * radius, radius), 4);
        float prevAlpha = 2f, prevSize = 0f;
        for (var i = 0; i <= 100; i++)
        {
            var r = radius * i / 100f;
            var a = FogVolumeShape.AlphaFactor(r, radius);
            var sz = FogVolumeShape.RadialSizeFactor(r, radius);
            Assert.True(a < prevAlpha, $"alpha not falling at {i}");
            Assert.True(a >= 0.15f - 1e-5f);
            Assert.True(sz > prevSize, $"size not growing at {i}");
            prevAlpha = a; prevSize = sz;
        }
        // Gaussian-like: flat near the player (most of alpha kept at 0.2 R), steepest around sigma_r.
        Assert.True(FogVolumeShape.AlphaFactor(0.2f * radius, radius) > 0.85f);
        // A sample carries all of it (no overlap cap given).
        var p = FogVolumeShape.Sample(0.5f, 0.25f, 0.5f, radius, sigma);
        var d = (float)Math.Sqrt(p.X * p.X + p.Z * p.Z);
        Assert.Equal(FogVolumeShape.RadiusAt(0.5f, radius), d, 3);
        Assert.Equal(FogVolumeShape.HeightAt(0.5f, sigma), p.Height, 4);
        Assert.Equal(FogVolumeShape.SizeFactor(p.Height, sigma) * FogVolumeShape.RadialSizeFactor(d, radius), p.SizeFactor, 4);
        Assert.Equal(FogVolumeShape.AlphaFactor(d, radius), p.AlphaFactor, 3);
        Assert.True(p.X < 0.01f && p.Z > 0f);   // u angle 0.25 = a quarter turn
    }

    [Fact]
    public void OverlapCap_KeepsTheCentreBelowTheTargetOpacity()
    {
        // Round T ruling 4: many overlapping puffs near the player must not blow out. Expected column opacity at distance r:
        // 1 - (1 - p x factor)^k(r), k(r) = centre overlap x AreaDensity(r). The cap lowers the factor only where that would
        // exceed MaxOpacity, so the Gaussian alpha profile still rules the outer part.
        const float radius = 7f;
        var cap = new FogOverlapCap(40f, 0.04f, 0.5f);
        Assert.True(cap.Active);
        Assert.False(default(FogOverlapCap).Active);
        Assert.Equal(1f, default(FogOverlapCap).FactorLimit(0f, radius), 5);
        float prevOpacity = 1f;
        for (var i = 0; i <= 100; i++)
        {
            var r = radius * i / 100f;
            var factor = FogVolumeShape.CappedAlphaFactor(r, radius, cap);
            Assert.True(factor <= FogVolumeShape.AlphaFactor(r, radius) + 1e-6f);
            Assert.True(factor > 0f);
            var opacity = cap.ColumnOpacity(r, radius, factor);
            Assert.True(opacity <= 0.5f + 1e-4f, $"column opacity {opacity} at {r} m");
            Assert.True(opacity <= prevOpacity + 1e-4f, $"column opacity rises at {r} m");
            prevOpacity = opacity;
        }
        // The centre is capped (uncapped it would be 1 - 0.96^40 = 0.80), the rim keeps its Gaussian alpha.
        Assert.True(FogVolumeShape.CappedAlphaFactor(0f, radius, cap) < 0.5f);
        Assert.Equal(0.5f, cap.ColumnOpacity(0f, radius, FogVolumeShape.CappedAlphaFactor(0f, radius, cap)), 3);
        Assert.Equal(FogVolumeShape.AlphaFactor(radius, radius), FogVolumeShape.CappedAlphaFactor(radius, radius, cap), 5);
        // A thin cloud is not capped at all.
        var thin = new FogOverlapCap(3f, 0.04f, 0.5f);
        Assert.Equal(FogVolumeShape.AlphaFactor(0f, radius), FogVolumeShape.CappedAlphaFactor(0f, radius, thin), 5);
        // Sample and burst pattern apply the cap.
        var p = FogVolumeShape.Sample(0f, 0f, 0.5f, radius, 0.8f, cap);
        Assert.Equal(FogVolumeShape.CappedAlphaFactor(0f, radius, cap), p.AlphaFactor, 4);
        var b = FogBurstPattern.Point(0, 60, radius, 0.8f, cap);
        var bd = (float)Math.Sqrt(b.X * b.X + b.Z * b.Z);
        Assert.Equal(FogVolumeShape.CappedAlphaFactor(bd, radius, cap), b.AlphaFactor, 4);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void OverlapCap_DefaultsAreCappedButNotStarved(int tier)
    {
        // Round T ruling 4 on the real defaults: several puffs overlap at the player (cap active), the centre column stays at
        // MaxOpacity, and a puff at the player still keeps a visible part of OuterAlpha.
        var s = FogSettings.Defaults(tier);
        Assert.Equal(0.5f, FogSettings.OuterMaxColumnOpacity, 5);
        Assert.True(s.OuterCentreOverlap > 5f, $"centre overlap {s.OuterCentreOverlap}");
        var cap = s.OuterOverlapCap;
        Assert.True(cap.Active);
        Assert.Equal(s.OuterAlpha * FogSettings.OuterVolumeLifetimeMeanAlpha * FogSprite.MeanDiscAlpha, cap.PuffOpacity, 5);
        var centre = FogVolumeShape.CappedAlphaFactor(0f, s.OuterRadius, cap);
        Assert.InRange(centre, 0.2f, 1f);
        Assert.True(cap.ColumnOpacity(0f, s.OuterRadius, centre) <= FogSettings.OuterMaxColumnOpacity + 1e-4f);
        // Inactive layers have no cap.
        var off = FogSettings.Defaults(tier);
        off.OuterEnabled = false;
        Assert.False(off.OuterOverlapCap.Active);
    }

    [Fact]
    public void FogSprite_MeanDiscAlphaMatchesTheFalloff()
    {
        // Mean of the soft sprite's falloff over its disc (used by the overlap estimate), checked by numeric integration.
        double sum = 0; const int steps = 20000;
        for (var i = 0; i < steps; i++)
        {
            var r = (i + 0.5) / steps;
            sum += FogSprite.Falloff((float)r) * 2 * r / steps;
        }
        Assert.Equal((float)sum, FogSprite.MeanDiscAlpha, 3);
        Assert.InRange(FogSprite.MeanDiscAlpha, 0.15f, 0.25f);
    }

    [Fact]
    public void BurstPattern_GroundHeavyEvenAroundThePlayer()
    {
        // Round S: the burst follows the same distribution, deterministic: radius by stratified quantiles, angle on the golden
        // spiral, height from a van der Corput sequence.
        const int n = 60; const float radius = 7f, sigma = 0.8f;
        var pts = new List<FogVolumePoint>();
        for (var i = 0; i < n; i++) pts.Add(FogBurstPattern.Point(i, n, radius, sigma));
        Assert.Equal(pts[5], FogBurstPattern.Point(5, n, radius, sigma));   // deterministic
        var low = 0; var innerHalf = 0;
        foreach (var p in pts)
        {
            var r = (float)Math.Sqrt(p.X * p.X + p.Z * p.Z);
            Assert.InRange(r, 0f, radius + 1e-4f);
            Assert.InRange(p.Height, FogVolumeShape.HeightFloor - 1e-5f, 3f * sigma + 1e-4f);
            if (p.Height < sigma) low++;
            if (r < radius / 2f) innerHalf++;
        }
        Assert.True(low > n / 2, $"{low} of {n} below one sigma");
        Assert.True(innerHalf >= n * 0.45, $"{innerHalf} of {n} in the inner half radius");   // round T: Gaussian, about half
        // Every quadrant of the disc gets about a quarter of the puffs (no spotlight cluster).
        var quadrants = new int[4];
        foreach (var p in pts) quadrants[(p.X >= 0 ? 0 : 1) + (p.Z >= 0 ? 0 : 2)]++;
        foreach (var q in quadrants) Assert.InRange(q, n / 4 - 4, n / 4 + 4);
        // Degenerate input.
        Assert.Equal(default(FogVolumePoint), FogBurstPattern.Point(0, 0, radius, sigma));
        Assert.Equal(0f, FogBurstPattern.Point(0, 1, 0f, sigma).X, 5);
    }

    [Fact]
    public void OuterHeightSigma_KeyClampsRoundTripsAndParses()
    {
        var key = Find("OuterHeightSigma");
        Assert.NotNull(key);
        Assert.Equal(FogValueKind.Float, key.Kind);
        Assert.Contains("ground", key.Description, StringComparison.OrdinalIgnoreCase);
        var s = FogSettings.Defaults(2);
        Assert.Equal("1", s.Get("OuterHeightSigma"));   // round T: tier II sigma 1
        Assert.True(s.TrySet("OuterHeightSigma", "0")); Assert.Equal(FogVolumeShape.HeightFloor, s.OuterHeightSigma, 5);
        Assert.True(s.TrySet("OuterHeightSigma", "9")); Assert.Equal(FogVolumeShape.MaxHeightSigma, s.OuterHeightSigma, 5);
        Assert.True(s.TrySet("OuterHeightSigma", "1.2")); Assert.Equal(1.2f, s.OuterHeightSigma, 5);
        Assert.False(s.TrySet("OuterHeightSigma", "tall"));
        Assert.Equal(1.2f, s.Clone().OuterHeightSigma, 5);
        Assert.Equal(0.5f, FogSettings.Parse(1, k => k == "OuterHeightSigma" ? "0.5" : null, new List<string>()).OuterHeightSigma, 5);
    }

    [Fact]
    public void Descriptions_ArePlainWords()
    {
        foreach (var k in FogSettings.Keys) Assert.False(string.IsNullOrWhiteSpace(k.Description), k.Name);
        Assert.Contains("0 = invisible", Find("OuterAlpha").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 = solid", Find("OuterAlpha").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wide fog", Find("OuterRadius").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("metres", Find("OuterRadius").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("per second", Find("OuterRate").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("per metre walked", Find("OuterRateDistance").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("at once", Find("OuterBurst").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lingers", Find("OuterLifetime").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("metres", Find("OuterSize").Description, StringComparison.OrdinalIgnoreCase);
        // Round T: the changed keys say what the new profile does.
        Assert.Contains("fades out", Find("OuterRadius").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("15 %", Find("OuterAlpha").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("larger", Find("OuterSize").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("grows", Find("OuterLifetime").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("body cloud", Find("Alpha").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("body cloud", Find("Size").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("body cloud", Find("Rate").Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ground fog", Find("GroundAlpha").Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Readme_ExplainsTheThreeLayers()
    {
        var text = FogSettings.Readme;
        Assert.Contains("body cloud", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wide fog", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ground fog", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Outer", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ground", text);
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
        // Round M ruling 2c: at most 60 live ground particles (both layers together), so overlaps cannot saturate.
        Assert.Equal(60, FogSettings.GroundParticleBudget);
        Assert.True(s.GroundUpperMaxParticles >= 4);
        Assert.True(s.GroundLowerMaxParticles >= s.GroundUpperMaxParticles);
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
        // Round T ruling 3 (vanilla-like mist): slow horizontal drift 0.05 m/s, size 1.0 -> 1.35 over the life, alpha in over
        // 15 % and out over the last 35 %, random rotation with a slow spin of up to 4 deg/s either way.
        Assert.Equal(0.05f, FogSettings.OuterVolumeSpeed, 5);
        Assert.Equal(0.15f, FogSettings.OuterVolumeFadeIn, 5);
        Assert.Equal(0.35f, FogSettings.OuterVolumeFadeOut, 5);
        Assert.Equal(1.35f, FogSettings.OuterVolumeGrow, 5);
        Assert.Equal(4f, FogSettings.OuterVolumeSpinDegPerSecond, 5);
        // Mean of the alpha-over-life curve (0 -> 1 over 15 %, flat, 1 -> 0 over 35 %): 0.075 + 0.5 + 0.175.
        Assert.Equal(0.75f, FogSettings.OuterVolumeLifetimeMeanAlpha, 5);
        // Mean of the size-over-life growth (linear 1 -> 1.35).
        Assert.Equal(1.175f, FogSettings.OuterVolumeMeanGrow, 5);
        // Drift: horizontal, 0.05 m/s, in the direction of the angle fraction.
        FogSettings.OuterVolumeDrift(0.25f, out var dx, out var dz);
        Assert.Equal(0f, dx, 4); Assert.Equal(0.05f, dz, 4);
        FogSettings.OuterVolumeDrift(0f, out dx, out dz);
        Assert.Equal(0.05f, dx, 4); Assert.Equal(0f, dz, 4);
        // Round S: the volume is ground-heavy; its height comes from OuterHeightSigma (OuterSpreadY is the ring's band only).
        // Round T: per tier (T1 0.8, T2 1.0).
        var t2 = FogSettings.Defaults(2);
        Assert.Equal(1f, t2.OuterHeightSigma, 5);
        Assert.Equal(0.8f, FogSettings.Defaults(1).OuterHeightSigma, 5);
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

    [Fact]
    public void ParticleCaps_InnerPerAnchorVolumeAndRing()
    {
        // Round M ruling 2c (caps raised by the controller: 24 per anchor, volume 80): brightness is handled by alpha, not by starving.
        Assert.Equal(24, FogSettings.InnerParticleCap);
        Assert.Equal(200, FogSettings.OuterVolumeParticleCap);   // round T (round R 140)
        Assert.Equal(300, FogSettings.ParticleHardCap);
        var s = FogSettings.Defaults(2);   // inner 14/s x 2.5 s = 35 wanted
        Assert.Equal(24, s.InnerMaxParticles);
        s.Rate = 1f; s.Lifetime = 1f;     // small budgets stay below the cap (MaxParticles minimum 8)
        Assert.Equal(FogSettings.MaxParticles(1f, 1f), s.InnerMaxParticles);
        Assert.True(s.InnerMaxParticles <= 24);
        // Mesh: one emitter for the body, capped at 10 per enabled anchor.
        var m = FogSettings.Defaults(2);
        Assert.Equal(Math.Min(24 * 13, FogSettings.MaxParticles(m.MeshEmitterRate(13), m.Lifetime)), m.MeshMaxParticles(13));
        Assert.Equal(Math.Min(48, FogSettings.MaxParticles(m.MeshEmitterRate(2), m.Lifetime)), m.MeshMaxParticles(2));
        Assert.True(m.MeshMaxParticles(0) >= 8);
        // Volume: 4/s + 2/m, 9 s -> capped at 40.
        var v = FogSettings.Defaults(2);
        v.OuterFollowShare = 0f;   // round Q: one system takes the whole cap
        Assert.Equal(Math.Min(200, FogSettings.MaxParticles(v.OuterRate + v.OuterEffectiveRateDistance * FogSettings.GroundBudgetSpeed, v.OuterLifetime)), v.OuterMaxParticles);
        v.OuterRate = 1f; v.OuterRateDistance = 0f; v.OuterLifetime = 2f;
        Assert.Equal(FogSettings.MaxParticles(1f, 2f), v.OuterMaxParticles);
        // Ring keeps the round J budget (rate x lifetime, hard cap only).
        var r = FogSettings.Defaults(2);
        r.OuterShape = FogOuterShape.Ring; r.OuterRate = 14f; r.OuterLifetime = 3f;
        Assert.Equal(FogSettings.MaxParticles(14f, 3f), r.OuterMaxParticles);
    }

    [Fact]
    public void OuterRateDistance_KeyClampsAndOnlyCountsInWorldSpace()
    {
        Assert.Contains(FogSettings.Keys, k => k.Name == "OuterRateDistance" && k.Kind == FogValueKind.Float);
        var s = FogSettings.Defaults(2);
        Assert.Equal("4", s.Get("OuterRateDistance"));   // round P: tier II 4 per metre
        Assert.True(s.TrySet("OuterRateDistance", "-3")); Assert.Equal(0f, s.OuterRateDistance);
        Assert.True(s.TrySet("OuterRateDistance", "1.5")); Assert.Equal(1.5f, s.OuterRateDistance, 5);
        Assert.False(s.TrySet("OuterRateDistance", "far"));
        // rateOverDistance only works in world space: in Follow mode the effective value is 0.
        Assert.Equal(1.5f, s.OuterEffectiveRateDistance, 5);
        s.OuterTrail = false;
        Assert.Equal(0f, s.OuterEffectiveRateDistance);
        Assert.Equal(0f, FogSettings.Defaults(3).OuterRateDistance);
    }

    [Fact]
    public void MaterialCheck_FlagsBrightColourAndEmission()
    {
        var grey = new FogRgb(0.55f, 0.57f, 0.6f);
        Assert.Empty(FogMaterialCheck.Problems(grey, grey, new FogRgb(0f, 0f, 0f), 0f));
        Assert.Empty(FogMaterialCheck.Problems(grey, new FogRgb(0.5f, 0.5f, 0.5f), new FogRgb(0f, 0f, 0f), 0f));
        var bright = FogMaterialCheck.Problems(grey, new FogRgb(1f, 1f, 1f), new FogRgb(0f, 0f, 0f), 0f);
        Assert.Single(bright);
        Assert.Contains("_Color", bright[0]);
        var glow = FogMaterialCheck.Problems(grey, grey, new FogRgb(0.2f, 0.2f, 0.2f), 0f);
        Assert.Single(glow);
        Assert.Contains("_EmissionColor", glow[0]);
        // Emission configured above 0: a non-black _EmissionColor is the user's choice.
        Assert.Empty(FogMaterialCheck.Problems(grey, grey, new FogRgb(0.2f, 0.2f, 0.2f), 0.4f));
        Assert.Contains("_SrcBlend", FogMaterialCheck.BlendProperties);
        Assert.Contains("_DstBlend", FogMaterialCheck.BlendProperties);
        Assert.Contains("_BlendOp", FogMaterialCheck.BlendProperties);
        Assert.Contains("_AlphaChannel", FogMaterialCheck.BlendProperties);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RoundM_SubtleHaze(int tier)
    {
        var s = FogSettings.Defaults(tier);
        // Darker than the sky so overlaps read as haze; never white.
        Assert.True(s.R <= 0.6f && s.G <= 0.6f && s.B <= 0.6f);
        // Round O: alphas halfway between rounds L and M; round S: lighter, the body stays visible through the fog.
        Assert.InRange(s.Alpha, 0.1f, 0.16f);
        // Round P: the wide fog must read by day; tier II stronger than tier I. Round S: never concealing (at most 0.5).
        // Round T: subtle, 0.3 / 0.4.
        Assert.InRange(s.OuterAlpha, 0.3f, 0.5f);
        Assert.True(FogSettings.Defaults(2).OuterAlpha >= 0.4f);
        Assert.True(FogSettings.Defaults(2).OuterAlpha > FogSettings.Defaults(1).OuterAlpha);
        Assert.InRange(s.GroundAlpha, 0.12f, 0.18f);
        Assert.Equal(0f, s.Emission);
        // Round O: fewer, larger particles per bone.
        Assert.Equal(tier == 1 ? 7f : 9f, s.Rate, 5);
        Assert.Equal(tier == 1 ? 1.1f : 1.2f, s.Size, 5);
        Assert.Equal(tier == 1 ? 2.4f : 2.6f, s.Lifetime, 5);
        Assert.Equal(tier == 1 ? 0.12f : 0.14f, s.Alpha, 5);
        Assert.Equal(tier == 1 ? 0.3f : 0.4f, s.OuterAlpha, 5);
        Assert.Equal(tier == 1 ? 0.14f : 0.16f, s.GroundAlpha, 5);
    }
}
