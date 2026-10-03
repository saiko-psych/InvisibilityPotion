using InvisibilityPotion.Plants;
using Xunit;

public class PlantDefaultsTests
{
    [Theory]
    [InlineData("BaldrZoneChance", 0.167f, 0.5f)]     // revision 0 file: both steps
    [InlineData("BaldrZoneChance", 0.2f, 0.5f)]       // value already at the revision 1 default
    [InlineData("HelFernZoneChance", 0.0667f, 0.5f)]
    [InlineData("HelFernZoneChance", 0.1f, 0.5f)]
    public void Old_default_moves_to_new(string key, float old, float expected) =>
        Assert.Equal(expected, PlantDefaults.Migrate(key, old, 0));

    [Theory]
    [InlineData("BaldrZoneChance", 0.3f)]
    [InlineData("HelFernZoneChance", 0.05f)]
    [InlineData("HelFernZoneChance", 0f)]
    public void Custom_value_is_kept(string key, float custom) => Assert.Equal(custom, PlantDefaults.Migrate(key, custom, 0));

    [Fact]
    public void File_at_revision_1_takes_only_the_second_step()
    {
        Assert.Equal(0.5f, PlantDefaults.Migrate("BaldrZoneChance", 0.2f, 1));
        Assert.Equal(0.167f, PlantDefaults.Migrate("BaldrZoneChance", 0.167f, 1));   // was customised back to the old value: kept
    }

    [Fact]
    public void Migrated_file_is_not_touched_again()
    {
        Assert.Equal(0.1f, PlantDefaults.Migrate("HelFernZoneChance", 0.1f, PlantDefaults.Revision));
        Assert.Equal(0.0667f, PlantDefaults.Migrate("HelFernZoneChance", 0.0667f, PlantDefaults.Revision));
    }

    [Fact]
    public void Unknown_key_is_kept() => Assert.Equal(0.5f, PlantDefaults.Migrate("LichenTreeChance", 0.5f, 0));

    [Fact]
    public void New_defaults_match_vanilla_seeds_one_in_two()
    {
        Assert.Equal(0.5f, PlantDefaults.BaldrZoneChance);
        Assert.Equal(0.5f, PlantDefaults.HelFernZoneChance);
        Assert.Equal(2, PlantDefaults.Revision);
    }
}
