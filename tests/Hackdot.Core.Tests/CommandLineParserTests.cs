using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Tests;

public class CommandLineParserTests
{
    [Fact]
    public void Parse_SplitsOnWhitespace()
    {
        var stages = CommandLineParser.Parse("  ls   -la  /var/www ");

        Assert.Equal(["ls", "-la", "/var/www"], Assert.Single(stages));
    }

    [Theory]
    [InlineData("grep \"Failed password\" auth.log", "Failed password")]
    [InlineData("grep 'it''s' auth.log", "its")]
    [InlineData("grep Failed\\ password auth.log", "Failed password")]
    [InlineData("grep \"say \\\"hi\\\"\" auth.log", "say \"hi\"")]
    [InlineData("grep '$HOME \\n' auth.log", "$HOME \\n")]
    [InlineData("grep \"\" auth.log", "")]
    public void Parse_HandlesQuotesAndEscapes(string input, string expectedPattern)
    {
        var stage = Assert.Single(CommandLineParser.Parse(input));

        Assert.Equal(3, stage.Count);
        Assert.Equal(expectedPattern, stage[1]);
    }

    [Fact]
    public void Parse_SplitsPipeline()
    {
        var stages = CommandLineParser.Parse("cat auth.log|grep Failed | grep -i root");

        Assert.Equal(3, stages.Count);
        Assert.Equal(["cat", "auth.log"], stages[0]);
        Assert.Equal(["grep", "Failed"], stages[1]);
        Assert.Equal(["grep", "-i", "root"], stages[2]);
    }

    [Fact]
    public void Parse_QuotedPipeIsNotAPipe()
    {
        var stage = Assert.Single(CommandLineParser.Parse("grep 'a|b' file"));

        Assert.Equal("a|b", stage[1]);
    }

    [Theory]
    [InlineData("cat 'oops", "Error.UnterminatedQuote")]
    [InlineData("cat \"oops", "Error.UnterminatedQuote")]
    [InlineData("| grep x", "Error.UnexpectedPipe")]
    [InlineData("cat file |", "Error.UnexpectedPipe")]
    [InlineData("cat file || grep x", "Error.UnexpectedPipe")]
    public void Parse_InvalidInput_ThrowsWithResourceKey(string input, string key)
    {
        var ex = Assert.Throws<CommandSyntaxException>(() => CommandLineParser.Parse(input));

        Assert.Equal(key, ex.ResourceKey);
    }

    [Theory]
    [InlineData("auth.log", "*.log", false, true)]
    [InlineData("auth.log", "*.txt", false, false)]
    [InlineData(".secret_flag", ".*", false, true)]
    [InlineData("notes.txt", "note?.txt", false, true)]
    [InlineData("NOTES.TXT", "notes.txt", true, true)]
    [InlineData("NOTES.TXT", "notes.txt", false, false)]
    [InlineData("a", "*a*", false, true)]
    [InlineData("abc", "a*c*", false, true)]
    public void Glob_MatchesShellWildcards(string text, string pattern, bool ignoreCase, bool expected)
    {
        Assert.Equal(expected, Glob.IsMatch(text, pattern, ignoreCase));
    }
}
