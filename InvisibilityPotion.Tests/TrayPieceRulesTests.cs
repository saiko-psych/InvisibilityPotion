using InvisibilityPotion.Items;
using Xunit;

public class TrayPieceRulesTests
{
    [Fact]
    public void ItemNames_MeadsThenBases()
    {
        Assert.Equal(new[] { "M1", "M2", "M3", "B1", "B2", "B3" }, TrayPieceRules.ItemNames(t => $"M{t}", t => $"B{t}"));
    }

    [Fact]
    public void TableLookups_JotunnTableNameFirst()
    {
        Assert.Equal("_FeasterPieceTable", TrayPieceRules.TableLookups[0]);
        Assert.Contains("ServingTray", TrayPieceRules.TableLookups);
    }

    [Theory]
    // isStatic, initOnly, literal, public, [SerializeField], [NonSerialized], delegate -> copied
    [InlineData(false, false, false, true, false, false, false, true)]    // public field (m_health)
    [InlineData(false, false, false, false, true, false, false, true)]    // private [SerializeField]
    [InlineData(false, false, false, false, false, false, false, false)]  // private runtime state
    [InlineData(false, false, false, true, false, true, false, false)]    // public [NonSerialized]
    [InlineData(false, false, false, true, false, false, true, false)]    // public Action m_onDestroyed
    [InlineData(true, false, false, true, false, false, false, false)]    // static
    [InlineData(false, true, false, true, false, false, false, false)]    // readonly
    [InlineData(false, false, true, true, false, false, false, false)]    // const
    public void CopyField_OnlyWhatUnitySerializes(bool isStatic, bool initOnly, bool literal, bool isPublic, bool serializeField, bool nonSerialized, bool isDelegate, bool expected)
    {
        Assert.Equal(expected, TrayPieceRules.CopyField(isStatic, initOnly, literal, isPublic, serializeField, nonSerialized, isDelegate));
    }
}
