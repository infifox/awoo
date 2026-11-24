using System.Text.Json;
using System.Text.Json.Serialization;

namespace Awoo.Settings;

/// <summary>
/// Reads a list of endpoint ids out of whatever the settings file has where
/// one should be, and writes an ordinary array of strings back.
/// </summary>
/// <remarks>
/// The settings file is the user's to edit, and <see cref="SettingsStore"/>
/// answers a file it cannot read at all with the defaults — which would mean
/// one stray character in the hidden list silently costing the volume step
/// as well. So this takes the entries that are strings and steps over
/// everything else: a number, an object, a null, or the whole list being
/// something other than a list. Which ids are real is not this converter's
/// question; a hidden device does not have to be plugged in.
/// </remarks>
internal sealed class DeviceIdListConverter : JsonConverter<IReadOnlyList<string>>
{
    public override IReadOnlyList<string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            // Not a list at all. Skip puts the reader past the whole value,
            // however deep it goes, so the rest of the file still reads.
            reader.Skip();
            return [];
        }

        var ids = new List<string>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                ids.Add(reader.GetString()!);
            }
            else
            {
                reader.Skip();
            }
        }

        return ids;
    }

    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyList<string> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var id in value)
        {
            writer.WriteStringValue(id);
        }

        writer.WriteEndArray();
    }
}
