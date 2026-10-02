using InvisibilityPotion.Plants;
using Xunit;

public class PlantDefaultsTests
{
    [Theory]
    [InlineData("BaldrZoneChance", 0.167f, 0.2f)]
    [InlineData("HelFernZoneChance", 0.0667f, 0.1f)]
    public void Old_default_moves_to_new(string key, float old, float expected) =>
        Assert.Equal(expected, PlantDefaults.Migrate(key, old, 0));

    [Theory]
    [InlineData("BaldrZoneChance", 0.3f)]
    [InlineData("HelFernZoneChance", 0.05f)]
    [InlineData("HelFernZoneChance", 0f)]
    public void Custom_value_is_kept(string key, float custom) => Assert.Equal(custom, PlantDefaults.Migrate(key, custom, 0));

    [Fact]
    public void Migrated_file_is_not_touched_again() =>
        Assert.Equal(0.0667f, PlantDefaults.Migrate("HelFernZoneChance", 0.0667f, PlantDefaults.Revision));

    [Fact]
    public void Unknown_key_is_kept() => Assert.Equal(0.5f, PlantDefaults.Migrate("LichenTreeChance", 0.5f, 0));

    [Fact]
    public void New_defaults_are_one_in_five_and_one_in_ten()
    {
        Assert.Equal(0.2f, PlantDefaults.BaldrZoneChance);
        Assert.Equal(0.1f, PlantDefaults.HelFernZoneChance);
    }
}
