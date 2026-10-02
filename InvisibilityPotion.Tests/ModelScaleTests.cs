using InvisibilityPotion.Items;
using Xunit;

public class ModelScaleTests
{
    [Fact]
    public void Groups_HaveTheRuledFactors()
    {
        Assert.Equal(2.0f, ModelScale.Bottle);
        Assert.Equal(3.0f, ModelScale.Bowl);
        Assert.Equal(1.5f, ModelScale.Plant);
        Assert.Equal(1f, ModelScale.Goggles);
        Assert.Equal(4.5f, ModelScale.Ingredient);
    }

    [Theory]
    [InlineData("MeadBottle_T1", 2.0f)]
    [InlineData("MeadBottle_T3", 2.0f)]
    [InlineData("MeadBowl_T2", 3.0f)]
    [InlineData("Plant_T1", 3.0f)]
    [InlineData("Plant_T1_Flat_a", 3.0f)]
    [InlineData("Plant_T3_picked", 1.5f)]
    [InlineData("Goggles_T2", 1f)]
    [InlineData("Ingredient_T1", 4.5f)]
    [InlineData("Ingredient_T3", 4.5f)]
    [InlineData("Something", 1f)]
    [InlineData("", 1f)]
    [InlineData(null, 1f)]
    public void For_MapsBundlePrefabNamesToTheirGroup(string prefab, float expected)
    {
        Assert.Equal(expected, ModelScale.For(prefab));
    }

    [Fact]
    public void ScaledChild_IsPerGroup()
    {
        Assert.Equal("attach", ModelScale.ScaledChild("MeadBottle_T1"));
        Assert.Equal("attach", ModelScale.ScaledChild("Goggles_T1"));
        Assert.Equal("model", ModelScale.ScaledChild("Plant_T2_a"));
        Assert.Equal("attach", ModelScale.ScaledChild("Ingredient_T2"));
    }
}
