using System.Linq;
using InvisibilityPotion.Config;
using Xunit;

public class MeadRecipesTests
{
    [Theory]
    [InlineData(1, "Honey:10,Thistle:5,VeilIngredient_T1:2")]
    [InlineData(2, "Honey:10,Thistle:5,Bloodbag:3,VeilIngredient_T2:2")]
    [InlineData(3, "Honey:10,Thistle:5,Bloodbag:3,YmirRemains:1,VeilIngredient_T3:2")]
    public void New_defaults_add_two_ingredients(int tier, string expected)
    {
        Assert.Equal(expected, MeadRecipes.NewDefault(tier));
        Assert.Contains(($"VeilIngredient_T{tier}", 2), RecipeParser.Parse(MeadRecipes.NewDefault(tier)));
    }

    [Fact]
    public void Old_default_migrates()
    {
        var (value, outcome) = MeadRecipes.Migrate(2, "Honey:10, Thistle:5, Bloodbag:3");
        Assert.Equal(MeadRecipes.NewDefault(2), value);
        Assert.Equal(MeadRecipes.Outcome.Migrated, outcome);
    }

    [Fact]
    public void New_default_is_unchanged() => Assert.Equal(MeadRecipes.Outcome.Unchanged, MeadRecipes.Migrate(1, MeadRecipes.NewDefault(1)).outcome);

    [Fact]
    public void Custom_recipe_is_kept_with_warning()
    {
        var (value, outcome) = MeadRecipes.Migrate(1, "Honey:3");
        Assert.Equal("Honey:3", value);
        Assert.Equal(MeadRecipes.Outcome.MissingIngredient, outcome);
    }

    [Fact]
    public void Custom_recipe_with_ingredient_is_unchanged() =>
        Assert.Equal(MeadRecipes.Outcome.Unchanged, MeadRecipes.Migrate(3, "Honey:1,VeilIngredient_T3:5").outcome);

    [Fact]
    public void Malformed_recipe_is_reported() => Assert.Equal(MeadRecipes.Outcome.Malformed, MeadRecipes.Migrate(1, "Honey").outcome);

    [Fact]
    public void Without_ingredients_drops_every_veil_ingredient()
    {
        var parsed = RecipeParser.Parse("Honey:10,VeilIngredient_T1:2,VeilIngredient_T2:1,Thistle:5");
        Assert.Equal(new[] { "Honey", "Thistle" }, MeadRecipes.WithoutIngredients(parsed).Select(e => e.item).ToArray());
    }
}
