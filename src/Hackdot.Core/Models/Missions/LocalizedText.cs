using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hackdot.Core.Models.Missions;

/// <summary>
/// Translatable text in mission files. In JSON it is either a plain string (English) or an object
/// with one entry per language: <c>{ "en": "Hello", "sv": "Hej" }</c>. Missing languages fall back to English.
/// </summary>
[JsonConverter(typeof(LocalizedTextConverter))]
public sealed class LocalizedText
{
    public const string DefaultLanguage = "en";

    private readonly Dictionary<string, string> _values;

    public LocalizedText(IEnumerable<KeyValuePair<string, string>> values)
    {
        _values = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    public LocalizedText(string english)
        : this([new KeyValuePair<string, string>(DefaultLanguage, english)])
    {
    }

    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>
    /// The text for <paramref name="culture"/> (default: the current UI culture), trying e.g.
    /// <c>sv-SE</c>, then <c>sv</c>, then English.
    /// </summary>
    public string Get(CultureInfo? culture = null)
    {
        for (var current = culture ?? CultureInfo.CurrentUICulture;
             !string.IsNullOrEmpty(current.Name);
             current = current.Parent)
        {
            if (_values.TryGetValue(current.Name, out var value))
            {
                return value;
            }
        }

        return _values.TryGetValue(DefaultLanguage, out var english) ? english : _values.Values.FirstOrDefault() ?? "";
    }

    public override string ToString() => Get();
}

public sealed class LocalizedTextConverter : JsonConverter<LocalizedText>
{
    public override LocalizedText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return new LocalizedText(reader.GetString()!);

            case JsonTokenType.StartObject:
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException("Expected a language code, e.g. \"en\".");
                    }

                    var language = reader.GetString()!;

                    if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                    {
                        throw new JsonException($"The text for language '{language}' must be a string.");
                    }

                    values[language] = reader.GetString()!;
                }

                if (!values.ContainsKey(LocalizedText.DefaultLanguage))
                {
                    throw new JsonException("Localized text must include English (\"en\").");
                }

                return new LocalizedText(values);

            default:
                throw new JsonException("Expected a string or an object like { \"en\": \"...\" }.");
        }
    }

    public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options)
    {
        if (value.Values.Count == 1 && value.Values.TryGetValue(LocalizedText.DefaultLanguage, out var english))
        {
            writer.WriteStringValue(english);
            return;
        }

        writer.WriteStartObject();

        foreach (var (language, text) in value.Values)
        {
            writer.WriteString(language, text);
        }

        writer.WriteEndObject();
    }
}
