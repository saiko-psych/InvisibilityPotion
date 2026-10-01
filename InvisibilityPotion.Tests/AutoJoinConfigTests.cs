using InvisibilityPotion.Dev;
using Xunit;

public class AutoJoinConfigTests
{
    [Fact]
    public void Parse_ReadsBothKeysFromFile()
    {
        var (world, character) = AutoJoinConfig.Parse("world=testing\ncharacter=Bob\n", "", "");

        Assert.Equal("testing", world);
        Assert.Equal("Bob", character);
    }

    [Fact]
    public void Parse_NullFileContent_FallsBackToEnvForBoth()
    {
        var (world, character) = AutoJoinConfig.Parse(null, "envWorld", "envChar");

        Assert.Equal("envWorld", world);
        Assert.Equal("envChar", character);
    }

    [Fact]
    public void Parse_EmptyFileKey_FallsBackToEnvForThatKeyOnly()
    {
        var (world, character) = AutoJoinConfig.Parse("world=\ncharacter=Bob\n", "envWorld", "envChar");

        Assert.Equal("envWorld", world);
        Assert.Equal("Bob", character);
    }

    [Fact]
    public void Parse_IgnoresBlankLinesAndComments()
    {
        var (world, character) = AutoJoinConfig.Parse("\n# comment\n   \n#world=ignored\nworld=real\n\ncharacter=Bob", "", "");

        Assert.Equal("real", world);
        Assert.Equal("Bob", character);
    }

    [Fact]
    public void Parse_TrimsWhitespaceAroundKeysAndValues()
    {
        var (world, character) = AutoJoinConfig.Parse("  world  =  my world  \n\tcharacter\t=\tBob\t\n", "", "");

        Assert.Equal("my world", world);
        Assert.Equal("Bob", character);
    }

    [Fact]
    public void Parse_HandlesWindowsLineEndings()
    {
        var (world, character) = AutoJoinConfig.Parse("world=testing\r\ncharacter=Bob\r\n", "", "");

        Assert.Equal("testing", world);
        Assert.Equal("Bob", character);
    }

    [Fact]
    public void Parse_ValueContainingEquals_KeepsEverythingAfterFirstEquals()
    {
        var (world, _) = AutoJoinConfig.Parse("world=a=b=c", "", "");

        Assert.Equal("a=b=c", world);
    }

    [Fact]
    public void Parse_IgnoresUnknownKeys()
    {
        var (world, character) = AutoJoinConfig.Parse("foo=bar\nworld=testing\n", "", "");

        Assert.Equal("testing", world);
        Assert.Equal("", character);
    }

    [Fact]
    public void Parse_BothSourcesEmpty_YieldsEmptyWorld()
    {
        var (world, character) = AutoJoinConfig.Parse("", null, null);

        Assert.Equal("", world);
        Assert.Equal("", character);
    }

    [Fact]
    public void Parse_KeysAreCaseInsensitive()
    {
        var (world, character) = AutoJoinConfig.Parse("World=testing\nCHARACTER=Bob\n", "", "");

        Assert.Equal("testing", world);
        Assert.Equal("Bob", character);
    }

    [Fact]
    public void Parse_DuplicateKey_LastOneWins()
    {
        var (world, _) = AutoJoinConfig.Parse("world=first\nworld=second\n", "", "");

        Assert.Equal("second", world);
    }

    [Fact]
    public void Parse_LineWithoutKey_IsIgnored()
    {
        var (world, character) = AutoJoinConfig.Parse("=value\nworld=testing\n", "", "");

        Assert.Equal("testing", world);
        Assert.Equal("", character);
    }
}
