using Awoo.Audio;

namespace Awoo.ViewModels;

/// <summary>
/// The Segoe Fluent Icons code points awoo draws audio with. Shared so the
/// flyout's buttons, the device rows and the tray icon never disagree about
/// which speaker a given volume draws.
/// </summary>
/// <remarks>
/// Written as escapes rather than literal characters: these live in the
/// font's private use area, where a literal is an empty box in every editor
/// and diff that reads this file.
/// </remarks>
internal static class VolumeGlyph
{
    /// <summary>Windows 11's icon font; Windows 10's is the fallback.</summary>
    internal const string FontFamily = "Segoe Fluent Icons";

    internal const string FallbackFontFamily = "Segoe MDL2 Assets";

    private const string Mute = "\uE74F";
    private const string Volume0 = "\uE992";
    private const string Volume1 = "\uE993";
    private const string Volume2 = "\uE994";
    private const string Volume3 = "\uE995";
    private const string Microphone = "\uE720";
    private const string MicrophoneOff = "\uEC54";

    /// <summary>The speaker for an output's level, muted included.</summary>
    internal static string For(VolumeLevel level) => level switch
    {
        VolumeLevel.Muted => Mute,
        VolumeLevel.Silent => Volume0,
        VolumeLevel.Low => Volume1,
        VolumeLevel.Medium => Volume2,
        _ => Volume3,
    };

    /// <summary>
    /// What kind of device a row is: a speaker or a microphone, regardless of
    /// how loud it is. This is the row's device icon rather than its control.
    /// </summary>
    internal static string ForKind(DeviceKind kind) =>
        kind == DeviceKind.Output ? Volume3 : Microphone;

    /// <summary>
    /// What a row's mute button shows. An output says how loud it is, the way
    /// the header does; a microphone only has the two states.
    /// </summary>
    internal static string ForMute(DeviceKind kind, VolumeLevel level) => kind switch
    {
        DeviceKind.Output => For(level),
        _ => level == VolumeLevel.Muted ? MicrophoneOff : Microphone,
    };

    /// <summary>
    /// What an application's row shows where its icon could not be
    /// extracted. Playing or recording, rather than anything about the app —
    /// there is nothing else known about it at that point.
    /// </summary>
    internal static string ForSession(SessionKind kind) =>
        kind == SessionKind.Playback ? Volume3 : Microphone;

    /// <summary>An application row's mute button, the same way a device row's works.</summary>
    internal static string ForMute(SessionKind kind, VolumeLevel level) => kind switch
    {
        SessionKind.Playback => For(level),
        _ => level == VolumeLevel.Muted ? MicrophoneOff : Microphone,
    };
}
