namespace Hackdot.Core.Services;

/// <summary>Reads JSON files embedded from the <c>Data/</c> folder.</summary>
public static class EmbeddedContent
{
    private static readonly System.Reflection.Assembly Assembly = typeof(EmbeddedContent).Assembly;

    /// <param name="relativePath">Path below <c>Data/</c>, e.g. <c>FileSystems/base.json</c>.</param>
    public static string Read(string relativePath)
    {
        var resourceName = $"Hackdot.Core.Data.{relativePath.Replace('/', '.').Replace('\\', '.')}";

        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded content '{relativePath}' was not found.", resourceName);
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
