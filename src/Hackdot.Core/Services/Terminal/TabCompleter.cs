using System.Text;
using Hackdot.Core.Models;

namespace Hackdot.Core.Services.Terminal;

/// <summary>One way to finish the word being typed: <see cref="Label"/> is shown, <see cref="Input"/> replaces the line.</summary>
public sealed record CompletionOption(string Label, string Input);

/// <summary>
/// The result of pressing Tab: <see cref="Input"/> is the line extended as far as all matches agree, and
/// <see cref="Options"/> lists the matches when there is more than one to choose from.
/// </summary>
public sealed record Completion(string Input, IReadOnlyList<CompletionOption> Options);

/// <summary>
/// Tab completion like bash: the first word of a command (and the word after <c>sudo</c>) completes to a
/// command name, every other word to a file or folder name. Names are only completed in folders the player
/// is allowed to list, so Tab never reveals what <c>ls</c> wouldn't. Hidden names need a leading dot.
/// </summary>
public static class TabCompleter
{
    public static Completion Complete(CommandInterpreter interpreter, string input)
    {
        var none = new Completion(input, []);

        if (Split(input) is not { } line)
        {
            return none;
        }

        var commandPosition = line.PreviousWords.Count == 0
            || (line.PreviousWords is ["sudo"] && !line.Word.StartsWith('-'));

        var matches = commandPosition && !line.Word.Contains('/')
            ? CommandMatches(interpreter, line.Word)
            : PathMatches(interpreter.Session, line.Word, directoriesOnly: line.PreviousWords is ["cd", ..]);

        if (matches.Count == 0)
        {
            return none;
        }

        var head = input[..line.WordStart];
        string Build(string text, bool finished) => head + Write(text, line.Quote, finished);

        if (matches.Count == 1)
        {
            var match = matches[0];
            return new Completion(Build(match.Text, finished: !match.IsDirectory), []);
        }

        var common = CommonPrefix(matches.Select(match => match.Text));
        var options = matches
            .Select(match => new CompletionOption(match.Label, Build(match.Text, finished: !match.IsDirectory)))
            .ToList();

        return new Completion(Build(common, finished: false), options);
    }

    /// <param name="Text">The whole word once completed, e.g. <c>Documents/welcome.txt</c>.</param>
    /// <param name="Label">What the option is called, e.g. <c>welcome.txt</c> or <c>Documents/</c>.</param>
    /// <param name="IsDirectory">Folders end in <c>/</c> so the next Tab can continue into them.</param>
    private sealed record Match(string Text, string Label, bool IsDirectory);

    private static List<Match> CommandMatches(CommandInterpreter interpreter, string prefix) =>
        interpreter.Commands
            .Select(command => command.Name)
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => new Match(name, name, IsDirectory: false))
            .ToList();

    private static List<Match> PathMatches(ShellSession session, string word, bool directoriesOnly)
    {
        // '~' on its own becomes the home folder, like in bash.
        if (word == "~")
        {
            return [new Match("~/", "~/", IsDirectory: true)];
        }

        var slash = word.LastIndexOf('/');
        var folderPart = word[..(slash + 1)];
        var namePrefix = word[(slash + 1)..];

        var folder = session.FileSystem.GetDirectory(session.Resolve(folderPart.Length == 0 ? "." : folderPart));

        // Listing names needs read permission on the folder (and execute on the way there).
        if (folder is null || !session.CanRead(folder))
        {
            return [];
        }

        var matches = folder.SortedChildren()
            .Where(child => child.Name.StartsWith(namePrefix, StringComparison.Ordinal))
            .Where(child => !child.IsHidden || namePrefix.StartsWith('.'))
            .Where(child => child.IsDirectory || !directoriesOnly)
            .Select(child => child.IsDirectory
                ? new Match(folderPart + child.Name + "/", child.Name + "/", IsDirectory: true)
                : new Match(folderPart + child.Name, child.Name, IsDirectory: false))
            .ToList();

        // 'cd ..' + Tab → 'cd ../'
        if (namePrefix == ".." && matches.Count == 0)
        {
            matches.Add(new Match(folderPart + "../", "../", IsDirectory: true));
        }

        return matches;
    }

    private static string CommonPrefix(IEnumerable<string> values)
    {
        string? common = null;

        foreach (var value in values)
        {
            if (common is null)
            {
                common = value;
                continue;
            }

            var length = 0;
            while (length < common.Length && length < value.Length && common[length] == value[length])
            {
                length++;
            }

            common = common[..length];
        }

        return common ?? "";
    }

    /// <summary>
    /// Writes a completed word back into the line the way it was started: inside its open quote, or with
    /// backslashes before spaces and other special characters. A finished word (a file or a command) is
    /// closed and followed by a space, ready for the next argument.
    /// </summary>
    private static string Write(string word, char? quote, bool finished)
    {
        var text = new StringBuilder();

        if (quote is { } q)
        {
            text.Append(q).Append(word);
            if (finished)
            {
                text.Append(q).Append(' ');
            }

            return text.ToString();
        }

        foreach (var c in word)
        {
            if (char.IsWhiteSpace(c) || c is '\\' or '\'' or '"' or '|')
            {
                text.Append('\\');
            }

            text.Append(c);
        }

        if (finished)
        {
            text.Append(' ');
        }

        return text.ToString();
    }

    /// <param name="PreviousWords">The finished words of the current command (after the last <c>|</c>).</param>
    /// <param name="Word">The word being typed, with quotes and escapes removed.</param>
    /// <param name="WordStart">Where that word starts in the line.</param>
    /// <param name="Quote">The quote the word was opened with and not yet closed, if any.</param>
    private sealed record Line(IReadOnlyList<string> PreviousWords, string Word, int WordStart, char? Quote);

    /// <summary>Reads the line the way <see cref="CommandLineParser"/> does, keeping track of the last word.</summary>
    private static Line? Split(string input)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        var inWord = false;
        var wordStart = 0;
        char? quote = null;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (quote is { } q)
            {
                if (c == q)
                {
                    quote = null;
                }
                else if (q == '"' && c == '\\' && i + 1 < input.Length && input[i + 1] is '"' or '\\')
                {
                    current.Append(input[++i]);
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '\'' or '"':
                    Start(i);
                    quote = c;
                    break;
                case '\\':
                    Start(i);
                    if (i + 1 < input.Length)
                    {
                        current.Append(input[++i]);
                    }
                    break;
                case '|':
                    End();
                    words.Clear();
                    break;
                case '>':
                    // A redirection: the file name after it is a new word.
                    End();
                    break;
                case var _ when char.IsWhiteSpace(c):
                    End();
                    break;
                default:
                    Start(i);
                    current.Append(c);
                    break;
            }
        }

        if (!inWord)
        {
            return new Line(words, "", input.Length, null);
        }

        // A word that mixes quoting styles ("a"b) is too unusual to rewrite safely.
        var raw = input[wordStart..];
        var startsWithQuote = quote is not null && raw.Length > 0 && raw[0] == quote && raw.IndexOf(quote.Value, 1) < 0;

        if (quote is not null && !startsWithQuote || quote is null && raw.IndexOfAny(['\'', '"']) >= 0)
        {
            return null;
        }

        return new Line(words, current.ToString(), wordStart, quote);

        void Start(int index)
        {
            if (!inWord)
            {
                inWord = true;
                wordStart = index;
            }
        }

        void End()
        {
            if (inWord)
            {
                words.Add(current.ToString());
                current.Clear();
                inWord = false;
            }
        }
    }
}
