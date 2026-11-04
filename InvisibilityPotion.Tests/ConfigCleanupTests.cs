using InvisibilityPotion.Config;
using Xunit;

/// <summary>
/// Review 2026-11-04: the startup cleanup removed every key the running build does not bind, so a downgrade deleted the newer
/// version's keys with their customised values, and a Release build deleted the Debug-only [Dev] keys. Only the keys older
/// layouts are known to have used are removed now (PluginConfig.MigrateAndDropOrphans comment, FogSettings.ObsoleteKeys).
/// </summary>
public class ConfigCleanupTests
{
    [Theory]
    [InlineData("Fog", "FogRate")]
    [InlineData("Fog", "FogRateMoving")]
    [InlineData("Fog", "Anchor.Head")]
    [InlineData("Tier1", "FogEnabled")]
    [InlineData("Tier2", "FogDensity")]
    [InlineData("Tier3", "FogDensity")]
    [InlineData("Veil", "DistortionStrength")]
    [InlineData("General", "FogRateStanding")]
    [InlineData("General", "BodyVeilMode")]
    [InlineData("Fog.Tier1", "OuterRadiusMultiplier")]
    [InlineData("Fog.Tier3", "OuterLifetimeFactor")]
    public void Old_layout_keys_are_obsolete(string section, string key) => Assert.True(ConfigCleanup.IsObsolete(section, key));

    [Theory]
    [InlineData("Plants", "SomeKeyFromANewerVersion")]
    [InlineData("Dev", "TuningWindowRect")]           // bound only in Debug builds
    [InlineData("Tier2", "SomethingNew")]
    [InlineData("Tier4", "FogDensity")]               // not a tier section of this mod
    [InlineData("Fog.Tier2", "GroundRate")]
    [InlineData("General", "RevealOnBlock")]
    [InlineData("Veil", "GhostColor")]
    [InlineData("NewSection", "FogRate")]
    public void Other_unknown_keys_are_kept(string section, string key) => Assert.False(ConfigCleanup.IsObsolete(section, key));

    [Fact]
    public void Null_or_empty_is_kept()
    {
        Assert.False(ConfigCleanup.IsObsolete(null, "FogRate"));
        Assert.False(ConfigCleanup.IsObsolete("Fog", null));
        Assert.False(ConfigCleanup.IsObsolete("", ""));
    }
}
