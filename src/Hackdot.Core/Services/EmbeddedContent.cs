namespace Hackdot.Core.Services;

/// <summary>Reads JSON files embedded from the <c>Data/</c> folder.</summary>
public static class EmbeddedContent
{
    private const string Prefix = "Hackdot.Core.Data.";

    private static readonly System.Reflection.Assembly Assembly = typeof(EmbeddedContent).Assembly;

    /// <param name="relativePath">Path below <c>Data/</c>, e.g. <c>FileSystems/base.json</c>.</param>
    public static string Read(string relativePath) => ReadResource(ToResourceName(relativePath));

    /// <summary>Reads every embedded <c>.json</c> file in a folder below <c>Data/</c>, sorted by file name.</summary>
    public static IReadOnlyList<(string Name, string Content)> ReadAll(string folder)
    {
        var prefix = ToResourceName(folder) + ".";

        return Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => (name[prefix.Length..], ReadResource(name)))
            .ToList();
    }

    private static string ToResourceName(string relativePath) =>
        Prefix + relativePath.Replace('/', '.').Replace('\\', '.');

    private static string ReadResource(string resourceName)
    {
        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded content '{resourceName}' was not found.", resourceName);
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
