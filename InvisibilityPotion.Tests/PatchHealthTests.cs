using System.Linq;
using InvisibilityPotion;
using Xunit;

public class PatchHealthTests
{
    [Fact]
    public void MissingTargets_ReturnsEmpty_WhenAllExpectedArePatched()
    {
        var expected = new[] { "BaseAI.CanSenseTarget", "Humanoid.StartAttack" };
        var patched = new[] { "Humanoid.StartAttack", "BaseAI.CanSenseTarget", "FejdStartup.Start" };

        var missing = PatchHealth.MissingTargets(expected, patched);

        Assert.Empty(missing);
    }

    [Fact]
    public void MissingTargets_ListsOnlyUnpatchedExpectedTargets_InExpectedOrder()
    {
        var expected = new[] { "BaseAI.CanSenseTarget", "Humanoid.StartAttack", "ZDOMan.SendZDOs" };
        var patched = new[] { "Humanoid.StartAttack" };

        var missing = PatchHealth.MissingTargets(expected, patched);

        Assert.Equal(new[] { "BaseAI.CanSenseTarget", "ZDOMan.SendZDOs" }, missing);
    }

    [Fact]
    public void MissingTargets_IsCaseSensitive()
    {
        var missing = PatchHealth.MissingTargets(new[] { "BaseAI.CanSenseTarget" }, new[] { "baseai.cansensetarget" });

        Assert.Single(missing);
    }

    [Fact]
    public void TargetKey_FormatsAsTypeDotMethod()
    {
        Assert.Equal("BaseAI.CanSenseTarget", PatchHealth.TargetKey("BaseAI", "CanSenseTarget"));
    }
}
