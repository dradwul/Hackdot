using System.Text;

namespace Hackdot.Core.Services.Terminal;

/// <summary>A command line that cannot be parsed. <see cref="ResourceKey"/> points at the localized message.</summary>
public sealed class CommandSyntaxException(string resourceKey) : Exception(resourceKey)
{
    public string ResourceKey { get; } = resourceKey;
}

/// <summary>
/// Sends a command's output somewhere else: <c>&gt; file</c> (stream 1, replace), <c>&gt;&gt; file</c> (append),
/// <c>2&gt; file</c> (error messages) or <c>2&gt;&amp;1</c> (error messages go wherever the output goes).
/// </summary>
public sealed record Redirection(int Stream, string Target, bool Append)
{
    public const string ToOutput = "&1";

    public const string Nowhere = "/dev/null";
}

/// <summary>One command of a pipeline: its words and where its output is redirected.</summary>
public sealed record ParsedCommand(IReadOnlyList<string> Words, IReadOnlyList<Redirection> Redirections);

/// <summary>
/// Splits a command line into a pipeline of commands and their arguments, shell style:
/// whitespace separates arguments, <c>'single'</c> and <c>"double"</c> quotes group them,
/// a backslash escapes the next character, <c>|</c> pipes one command into the next and
/// <c>&gt;</c>, <c>&gt;&gt;</c>, <c>2&gt;</c> redirect output to a file.
/// </summary>
public static class CommandLineParser
{
    /// <summary>The words of each command, without redirections.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Parse(string input) =>
        [.. ParseLine(input).Select(command => command.Words)];

    public static IReadOnlyList<ParsedCommand> ParseLine(string input)
    {
        var stages = new List<ParsedCommand>();
        var tokens = new List<string>();
        var redirections = new List<Redirection>();
        var current = new StringBuilder();
        var inToken = false;
        var quoted = false;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            switch (c)
            {
                case '\'' or '"':
                    inToken = quoted = true;
                    i = ReadQuoted(input, i, current);
                    break;

                case '\\':
                    inToken = quoted = true;
                    if (i + 1 < input.Length)
                    {
                        current.Append(input[++i]);
                    }
                    break;

                case '>':
                    // "2>" redirects error messages; a 2 that is part of a longer word is just text.
                    var stream = 1;
                    if (inToken && !quoted && current.ToString() == "2")
                    {
                        stream = 2;
                        current.Clear();
                        inToken = false;
                    }
                    else
                    {
                        EndToken();
                    }

                    var append = i + 1 < input.Length && input[i + 1] == '>';
                    if (append)
                    {
                        i++;
                    }

                    if (stream == 2 && input.AsSpan(i + 1).StartsWith(Redirection.ToOutput))
                    {
                        redirections.Add(new Redirection(2, Redirection.ToOutput, append));
                        i += Redirection.ToOutput.Length;
                        break;
                    }

                    var target = ReadTarget(input, ref i);
                    redirections.Add(new Redirection(stream, target, append));
                    break;

                case '|':
                    EndToken();
                    if (tokens.Count == 0 && redirections.Count == 0)
                    {
                        throw new CommandSyntaxException("Error.UnexpectedPipe");
                    }

                    stages.Add(new ParsedCommand(tokens, redirections));
                    tokens = [];
                    redirections = [];
                    break;

                case var _ when char.IsWhiteSpace(c):
                    EndToken();
                    break;

                default:
                    inToken = true;
                    current.Append(c);
                    break;
            }
        }

        EndToken();

        if (tokens.Count > 0 || redirections.Count > 0)
        {
            stages.Add(new ParsedCommand(tokens, redirections));
        }
        else if (stages.Count > 0)
        {
            throw new CommandSyntaxException("Error.UnexpectedPipe");
        }

        return stages;

        void EndToken()
        {
            if (inToken)
            {
                tokens.Add(current.ToString());
                current.Clear();
                inToken = false;
            }

            quoted = false;
        }
    }

    /// <summary>Reads the file name after <c>&gt;</c>: one word, which may be quoted.</summary>
    private static string ReadTarget(string input, ref int index)
    {
        var target = new StringBuilder();
        var i = index + 1;

        while (i < input.Length && char.IsWhiteSpace(input[i]))
        {
            i++;
        }

        for (; i < input.Length; i++)
        {
            var c = input[i];

            if (char.IsWhiteSpace(c) || c is '|' or '>')
            {
                break;
            }

            if (c is '\'' or '"')
            {
                i = ReadQuoted(input, i, target);
            }
            else if (c == '\\' && i + 1 < input.Length)
            {
                target.Append(input[++i]);
            }
            else
            {
                target.Append(c);
            }
        }

        if (target.Length == 0)
        {
            throw new CommandSyntaxException("Error.MissingRedirectTarget");
        }

        index = i - 1;
        return target.ToString();
    }

    /// <returns>The index of the closing quote.</returns>
    private static int ReadQuoted(string input, int openIndex, StringBuilder current)
    {
        var quote = input[openIndex];

        for (var i = openIndex + 1; i < input.Length; i++)
        {
            var c = input[i];

            if (c == quote)
            {
                return i;
            }

            // Inside double quotes, \" and \\ are escapes. Single quotes are fully literal.
            if (quote == '"' && c == '\\' && i + 1 < input.Length && input[i + 1] is '"' or '\\')
            {
                current.Append(input[++i]);
                continue;
            }

            current.Append(c);
        }

        throw new CommandSyntaxException("Error.UnterminatedQuote");
    }
}
