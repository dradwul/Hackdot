using System.Text;

namespace Hackdot.Core.Services.Terminal;

/// <summary>A command line that cannot be parsed. <see cref="ResourceKey"/> points at the localized message.</summary>
public sealed class CommandSyntaxException(string resourceKey) : Exception(resourceKey)
{
    public string ResourceKey { get; } = resourceKey;
}

/// <summary>
/// Splits a command line into a pipeline of commands and their arguments, shell style:
/// whitespace separates arguments, <c>'single'</c> and <c>"double"</c> quotes group them,
/// a backslash escapes the next character and <c>|</c> pipes one command into the next.
/// </summary>
public static class CommandLineParser
{
    public static IReadOnlyList<IReadOnlyList<string>> Parse(string input)
    {
        var stages = new List<IReadOnlyList<string>>();
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inToken = false;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            switch (c)
            {
                case '\'' or '"':
                    inToken = true;
                    i = ReadQuoted(input, i, current);
                    break;

                case '\\':
                    inToken = true;
                    if (i + 1 < input.Length)
                    {
                        current.Append(input[++i]);
                    }
                    break;

                case '|':
                    EndToken();
                    if (tokens.Count == 0)
                    {
                        throw new CommandSyntaxException("Error.UnexpectedPipe");
                    }

                    stages.Add(tokens);
                    tokens = [];
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

        if (tokens.Count > 0)
        {
            stages.Add(tokens);
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
        }
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
