using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Tests;

public class TextCommandTests
{
    private const string Numbers = "1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11\n12\n";

    private const string Visits =
        "203.0.113.7 GET /login\n" +
        "10.0.0.5 GET /health\n" +
        "203.0.113.7 POST /login\n" +
        "198.51.100.2 GET /\n" +
        "203.0.113.7 POST /login\n" +
        "10.0.0.5 GET /health\n";

    private static CommandInterpreter Create() => TestTerminal.Create(fs =>
    {
        fs.WriteFile("/home/user/numbers.txt", Numbers);
        fs.WriteFile("/home/user/visits.log", Visits);
        fs.WriteFile("/home/user/words.txt", "banana\nApple\ncherry\napple\nbanana\n");
    });

    [Theory]
    [InlineData("head numbers.txt", "1\n2\n3\n4\n5\n6\n7\n8\n9\n10")]
    [InlineData("head -n 3 numbers.txt", "1\n2\n3")]
    [InlineData("head -n3 numbers.txt", "1\n2\n3")]
    [InlineData("head -3 numbers.txt", "1\n2\n3")]
    [InlineData("tail -n 2 numbers.txt", "11\n12")]
    [InlineData("tail -1 numbers.txt", "12")]
    [InlineData("cat numbers.txt | head -n 2", "1\n2")]
    [InlineData("head -n 0 numbers.txt", "")]
    public void HeadAndTail_ShowTheFirstOrLastLines(string command, string expected)
    {
        Assert.Equal(expected, Create().Run(command));
    }

    [Fact]
    public void Head_WithSeveralFiles_PrintsHeaders()
    {
        Assert.Equal("==> numbers.txt <==\n1\n\n==> words.txt <==\nbanana", Create().Run("head -n 1 numbers.txt words.txt"));
    }

    [Theory]
    [InlineData("head -n x numbers.txt", "head: invalid number of lines: 'x'")]
    [InlineData("head -n", "head: missing argument to '-n'")]
    [InlineData("tail", "tail: nothing to read. Give it a file – 'tail <file>' – or send text into it with a pipe: 'cat <file> | tail'.")]
    [InlineData("head nope.txt", "head: nope.txt: No such file or directory")]
    [InlineData("head /root/top_secret.txt", "head: /root/top_secret.txt: Permission denied")]
    [InlineData("tail Documents", "tail: Documents: Is a directory")]
    public void HeadAndTail_ReportProblems(string command, string error)
    {
        var result = Create().Execute(command);

        Assert.Equal(error, result.Lines[0].Text);
        Assert.True(result.Lines[0].IsError);
    }

    [Theory]
    [InlineData("wc -l numbers.txt", "12 numbers.txt")]
    [InlineData("cat numbers.txt | wc -l", "12")]
    [InlineData("wc words.txt", " 5  5 33 words.txt")]
    [InlineData("wc -w visits.log", "18 visits.log")]
    [InlineData("wc -l numbers.txt words.txt", "12 numbers.txt\n 5 words.txt\n17 total")]
    [InlineData("grep login visits.log | wc -l", "3")]
    public void Wc_CountsLinesWordsAndBytes(string command, string expected)
    {
        Assert.Equal(expected, Create().Run(command));
    }

    [Theory]
    [InlineData("sort words.txt", "Apple\napple\nbanana\nbanana\ncherry")]
    [InlineData("sort -r words.txt", "cherry\nbanana\nbanana\napple\nApple")]
    [InlineData("sort -u words.txt", "Apple\napple\nbanana\ncherry")]
    [InlineData("tail -n 4 numbers.txt | sort", "10\n11\n12\n9")]
    [InlineData("tail -n 4 numbers.txt | sort -n", "9\n10\n11\n12")]
    [InlineData("tail -n 4 numbers.txt | sort -rn", "12\n11\n10\n9")]
    public void Sort_SortsByLettersOrNumbers(string command, string expected)
    {
        Assert.Equal(expected, Create().Run(command));
    }

    [Theory]
    [InlineData("sort words.txt | uniq", "Apple\napple\nbanana\ncherry")]
    [InlineData("uniq words.txt", "banana\nApple\ncherry\napple\nbanana")]
    [InlineData("sort words.txt | uniq -c", "      1 Apple\n      1 apple\n      2 banana\n      1 cherry")]
    public void Uniq_CollapsesAdjacentRepeats(string command, string expected)
    {
        Assert.Equal(expected, Create().Run(command));
    }

    [Theory]
    [InlineData("cut -d: -f1 /etc/passwd", "root\nuser")]
    [InlineData("cut -d : -f 1,6 /etc/passwd", "root:/root\nuser:/home/user")]
    [InlineData("cut -d: -f6- /etc/passwd", "/root:/bin/bash\n/home/user:/bin/bash")]
    [InlineData("cut -d: -f-2 /etc/passwd", "root:x\nuser:x")]
    [InlineData("cut -d' ' -f1 visits.log | head -n 2", "203.0.113.7\n10.0.0.5")]
    [InlineData("cut -d, -f1 numbers.txt | head -n 1", "1")]
    public void Cut_PicksFields(string command, string expected)
    {
        Assert.Equal(expected, Create().Run(command));
    }

    [Theory]
    [InlineData("cut -d: /etc/passwd", "cut: say which fields you want with -f, e.g. 'cut -d: -f1 /etc/passwd'")]
    [InlineData("cut -d: -f0 /etc/passwd", "cut: invalid field list '0'. Fields are numbered from 1, e.g. -f1, -f1,3 or -f2-4.")]
    [InlineData("cut -d:: -f1 /etc/passwd", "cut: the delimiter must be a single character. For a space, use quotes: -d' '")]
    public void Cut_ExplainsMistakes(string command, string error)
    {
        Assert.Equal(error, Create().Run(command));
    }

    [Fact]
    public void CountingPipeline_FindsTheMostActiveVisitor()
    {
        Assert.Equal(
            "      3 203.0.113.7",
            Create().Run("cut -d' ' -f1 visits.log | sort | uniq -c | sort -n | tail -n 1"));
    }

    [Theory]
    [InlineData("echo hello world", "hello world")]
    [InlineData("echo 'a | b'", "a | b")]
    [InlineData("echo", "")]
    [InlineData("echo -n hi", "hi")]
    public void Echo_PrintsItsArguments(string command, string expected)
    {
        Assert.Equal(expected, Create().Run(command));
    }

    [Theory]
    [InlineData("echo hello | base64", "aGVsbG8K")]
    [InlineData("echo -n hello | base64", "aGVsbG8=")]
    [InlineData("echo aGVsbG8K | base64 -d", "hello")]
    [InlineData("echo aGVsbG8 | base64 -d", "hello")]
    [InlineData("echo hello | base64 | base64 -d", "hello")]
    [InlineData("echo hi | base64 | base64 | base64 -d | base64 -d", "hi")]
    public void Base64_EncodesAndDecodes(string command, string expected)
    {
        Assert.Equal(expected, Create().Run(command));
    }

    [Fact]
    public void Base64_WrapsLongOutputAt76Characters()
    {
        var lines = Create().Run("base64 numbers.txt").Split('\n');

        Assert.Equal("MQoyCjMKNAo1CjYKNwo4CjkKMTAKMTEKMTIK", lines.Single());
        Assert.All(Create().Run("cat numbers.txt numbers.txt numbers.txt | base64").Split('\n'), line => Assert.True(line.Length <= 76));
    }

    [Fact]
    public void Base64_RejectsTextThatIsNotBase64()
    {
        Assert.Equal("base64: invalid input – this does not look like base64 text", Create().Run("echo 'not base64!' | base64 -d"));
    }

    [Fact]
    public void History_ListsTypedCommandsWithoutRepeatsOrPasswords()
    {
        var terminal = TestTerminal.Create(fs => fs.AddUser(new Models.VirtualUser("admin", "/home/admin", "secret")));

        terminal.Execute("ls");
        terminal.Execute("ls");
        terminal.Execute("su admin");
        terminal.Execute("secret");
        terminal.Execute("exit");

        Assert.Equal("    1  ls\n    2  su admin\n    3  exit\n    4  history", terminal.Run("history"));
        Assert.Equal("    4  history\n    5  history 2", terminal.Run("history 2"));
        // Like bash, the search itself is in the history too.
        Assert.Equal("    2  su admin\n    6  history | grep su", terminal.Run("history | grep su"));

        terminal.Execute("history -c");
        Assert.Equal("    1  history", terminal.Run("history"));
    }

    [Fact]
    public void History_ForgetsTrailingSpaces()
    {
        var terminal = Create();

        terminal.Run("cat numbers.txt ");
        terminal.Run("cat numbers.txt");
        terminal.Run(@"cat my\ ");

        Assert.Equal(["cat numbers.txt", @"cat my\ "], terminal.History);
    }

    [Fact]
    public void History_CanBeSharedBetweenInterpreters()
    {
        var history = new List<string>();
        var first = new CommandInterpreter(new Services.ShellSession(Services.FileSystemLoader.CreateDefault()), TestTerminal.Text, history: history);
        first.Execute("pwd");

        var second = new CommandInterpreter(new Services.ShellSession(Services.FileSystemLoader.CreateDefault()), TestTerminal.Text, history: history);

        Assert.Equal(["pwd"], second.History);
    }

    [Fact]
    public void Ls_InAPipe_WritesOneNamePerLine()
    {
        var terminal = Create();

        Assert.Equal("Documents/  Downloads/  numbers.txt  visits.log  words.txt", terminal.Run("ls"));
        Assert.Equal("Documents\nDownloads\nnumbers.txt\nvisits.log\nwords.txt", terminal.Run("ls | cat"));
        Assert.Equal("5", terminal.Run("ls | wc -l"));
        Assert.Equal("visits.log", terminal.Run("ls | grep log"));
    }

    [Fact]
    public void Folders_WithoutReadPermission_CanBePassedThroughButNotListed()
    {
        var terminal = TestTerminal.Create(fs =>
        {
            fs.CreateDirectory("/srv/vault", owner: "root", mode: 0b111_001_001); // 711
            fs.WriteFile("/srv/vault/known-name.txt", "found me");
            fs.CreateDirectory("/srv/closed", owner: "root", mode: 0b111_000_000); // 700
            fs.WriteFile("/srv/closed/open-file.txt", "should stay hidden");
        });

        Assert.Equal("ls: cannot open directory '/srv/vault': Permission denied", terminal.Run("ls /srv/vault"));
        Assert.Equal("found me", terminal.Run("cat /srv/vault/known-name.txt"));
        Assert.Contains("Permission denied", terminal.Run("grep -r found /srv/vault"));

        // The file itself is readable (644), but the folder on the way is closed (700).
        Assert.Equal("cat: /srv/closed/open-file.txt: Permission denied", terminal.Run("cat /srv/closed/open-file.txt"));
    }

    [Fact]
    public void CaseHint_DoesNotLeakNamesFromFoldersThePlayerCannotList()
    {
        var terminal = TestTerminal.Create(fs =>
        {
            fs.CreateDirectory("/srv/vault", owner: "root", mode: 0b111_001_001); // 711
            fs.WriteFile("/srv/vault/Secret.txt", "x");
        });

        Assert.Equal("cat: /srv/vault/secret.txt: No such file or directory", terminal.Run("cat /srv/vault/secret.txt"));
    }
}
