using InvisibilityPotion.Config;
using InvisibilityPotion.Goggles;
using Xunit;

public class GoggleLevelTests
{
    [Theory]
    [InlineData("VeilGoggles_T1", 1)]
    [InlineData("VeilGoggles_T2", 2)]
    [InlineData("VeilGoggles_T3", 3)]
    [InlineData("VeilGoggles_T0", 0)]
    [InlineData("VeilGoggles_T4", 0)]
    [InlineData("VeilGoggles_T10", 0)]
    [InlineData("veilgoggles_t1", 0)]
    [InlineData("VeilGoggles_T1(Clone)", 0)]
    [InlineData("HelmetLeather", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void LevelOf_maps_prefab_names(string name, int expected) => Assert.Equal(expected, GoggleLevel.LevelOf(name));

    [Fact]
    public void Reveals_table()
    {
        for (var level = 0; level <= 3; level++)
            for (var required = 1; required <= 3; required++)
                Assert.Equal(level >= required, GoggleLevel.Reveals(level, required));
    }

    [Fact]
    public void Reveals_without_requirement_and_out_of_range_levels()
    {
        Assert.True(GoggleLevel.Reveals(0, 0));
        Assert.True(GoggleLevel.Reveals(9, 3));    // clamped to 3
        Assert.False(GoggleLevel.Reveals(-1, 1));
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(2, true, false)]
    [InlineData(3, true, true)]
    [InlineData(3, false, false)]
    [InlineData(7, true, true)]
    public void SeesHiddenPlayers_needs_level_three_and_switch(int level, bool sw, bool expected) =>
        Assert.Equal(expected, GoggleLevel.SeesHiddenPlayers(level, sw));

    [Fact]
    public void Default_recipes_parse_and_chain_the_previous_goggles()
    {
        for (var t = 1; t <= 3; t++) Assert.NotEmpty(RecipeParser.Parse(GoggleLevel.DefaultRecipe(t)));
        Assert.Contains(("VeilGoggles_T1", 1), RecipeParser.Parse(GoggleLevel.DefaultRecipe(2)));
        Assert.Contains(("VeilGoggles_T2", 1), RecipeParser.Parse(GoggleLevel.DefaultRecipe(3)));
        Assert.Equal("forge", GoggleLevel.Station(1));
        Assert.Equal("forge", GoggleLevel.Station(2));
        Assert.Equal("blackforge", GoggleLevel.Station(3));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => GoggleLevel.DefaultRecipe(4));
    }

    [Fact]
    public void Station_levels_follow_ruling_n1()
    {
        Assert.Equal(1, GoggleLevel.StationLevel(1));
        Assert.Equal(3, GoggleLevel.StationLevel(2));
        Assert.Equal(2, GoggleLevel.StationLevel(3));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => GoggleLevel.StationLevel(0));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => GoggleLevel.StationLevel(4));
    }
}
