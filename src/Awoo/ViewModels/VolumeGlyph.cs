using Awoo.Audio;

namespace Awoo.ViewModels;

/// <summary>
/// The Segoe Fluent Icons code point for a volume level. Shared so the
/// flyout's button and the tray icon never disagree about which speaker a
/// given volume draws.
/// </summary>
internal static class VolumeGlyph
{
    /// <summary>Windows 11's icon font; Windows 10's is the fallback.</summary>
    internal const string FontFamily = "Segoe Fluent Icons";

    internal const string FallbackFontFamily = "Segoe MDL2 Assets";

    internal static string For(VolumeLevel level) => level switch
    {
        VolumeLevel.Muted => "",   // Mute
        VolumeLevel.Silent => "",  // Volume0
        VolumeLevel.Low => "",     // Volume1
        VolumeLevel.Medium => "",  // Volume2
        _ => "",                   // Volume3
    };
}
