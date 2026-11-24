using System.Text.Json;
using System.Text.Json.Serialization;

namespace Awoo.Settings;

/// <summary>
/// Where awoo's settings live and how they get there. An unpackaged
/// application has no application data store of its own, so it is a file in
/// the user's local app data, the way every other portable Windows app does
/// it.
/// </summary>
internal static class SettingsStore
{
    internal static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "awoo",
        "settings.json");

    /// <summary>
    /// Reads the settings, falling back to the defaults for anything the file
    /// does not say — and for there being no file, which is what a first run
    /// looks like.
    /// </summary>
    /// <remarks>
    /// A file that cannot be read is treated the same way. A volume control
    /// that refuses to start because of its own settings file would be worse
    /// than one that starts with the defaults, and the next save puts a
    /// readable file back.
    /// </remarks>
    internal static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            using var stream = File.OpenRead(FilePath);
            var settings = JsonSerializer.Deserialize(stream, SettingsJson.Default.AppSettings);
            return (settings ?? new AppSettings()).Normalised();
        }
        catch (Exception exception) when (exception
            is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            return new AppSettings();
        }
    }

    internal static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            // Written beside the real file and moved over it, so an
            // interrupted write cannot leave half a settings file behind.
            var temporary = FilePath + ".new";
            using (var stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, settings, SettingsJson.Default.AppSettings);
            }

            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception
            is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Nowhere to write it. The setting holds for this run and is
            // forgotten, which is all that can be done about a disk that will
            // not take it.
        }
    }
}

/// <summary>
/// The serializer for <see cref="AppSettings"/>, generated at build time
/// rather than reflected over at run time, so nothing here depends on
/// metadata a trimmed publish would have taken away.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJson : JsonSerializerContext;
