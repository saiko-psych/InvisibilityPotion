using InvisibilityPotion.Items;
using Xunit;

public class ModelScaleTests
{
    [Fact]
    public void Groups_HaveTheRuledFactors()
    {
        Assert.Equal(2.4f, ModelScale.BottleWorld);
        Assert.Equal(2.0f, ModelScale.BottleAttach);
        Assert.Equal(3.0f, ModelScale.Bowl);
        Assert.Equal(2.2f, ModelScale.Plant);
        Assert.Equal(4.5f, ModelScale.Lichen);
        Assert.Equal(2.2f, ModelScale.GogglesWorld);
        Assert.Equal(1f, ModelScale.GogglesAttach);
        Assert.Equal(8f, ModelScale.Ingredient);
    }

    [Theory]
    [InlineData("MeadBottle_T1", 2.4f, 2.0f)]
    [InlineData("MeadBottle_T3", 2.4f, 2.0f)]
    [InlineData("MeadBowl_T2", 3.0f, 3.0f)]
    [InlineData("Plant_T1", 4.5f, 4.5f)]
    [InlineData("Plant_T1_Flat_a", 4.5f, 4.5f)]
    [InlineData("Plant_T2_b", 2.2f, 2.2f)]
    [InlineData("Plant_T3_picked", 2.2f, 2.2f)]
    [InlineData("Goggles_T2", 2.2f, 1f)]
    [InlineData("Ingredient_T1", 8f, 8f)]
    [InlineData("Ingredient_T3", 8f, 8f)]
    [InlineData("Something", 1f, 1f)]
    [InlineData("", 1f, 1f)]
    [InlineData(null, 1f, 1f)]
    public void Factors_MapBundlePrefabNamesToTheirGroup(string prefab, float world, float attach)
    {
        Assert.Equal(world, ModelScale.WorldFactor(prefab));
        Assert.Equal(attach, ModelScale.AttachFactor(prefab));
    }

    [Theory]
    [InlineData("MeadBottle_T2", 1.2f)]
    [InlineData("Goggles_T1", 2.2f)]
    [InlineData("MeadBowl_T1", 1f)]
    [InlineData("Ingredient_T2", 1f)]
    [InlineData("Plant_T2", 1f)]
    [InlineData(null, 1f)]
    public void DroppedRatio_IsWorldOverAttach(string prefab, float expected)
    {
        Assert.Equal(expected, ModelScale.DroppedRatio(prefab), 4);
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
