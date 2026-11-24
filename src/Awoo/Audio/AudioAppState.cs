namespace Awoo.Audio;

/// <summary>Which way sound flows through an application's stream.</summary>
internal enum SessionKind
{
    /// <summary>The app is playing sound.</summary>
    Playback,

    /// <summary>The app is recording from an input.</summary>
    Recording,
}

/// <summary>
/// One application as the Applications tab shows it: a snapshot, like
/// <see cref="AudioDeviceState"/>, so a reader never sees half of a change.
/// </summary>
/// <param name="Id">
/// awoo's key for the application rather than for one stream: everything the
/// same program is playing is one row, the way the Windows mixer shows it. It
/// is what <see cref="IAudioService.SetAppPercent"/> names.
/// </param>
/// <param name="Name">The app's name, as Windows would show it in the mixer.</param>
/// <param name="IconFile">
/// The file to pull the app's icon out of — its executable, or a shell
/// resource with an index — or null where none could be found.
/// </param>
/// <param name="Kind">Playing or recording.</param>
/// <param name="Percent">The app's own volume, 0–100, under the device's.</param>
/// <param name="IsMuted">Whether every stream of this app is muted.</param>
/// <param name="IsSystemSounds">
/// Whether this is Windows' system-sounds session, which is one row like any
/// other but belongs to no process the user would recognise.
/// </param>
/// <param name="RoutedDeviceId">
/// The endpoint this application's sound has been sent to, or null where it
/// follows the default device — which is the ordinary case. The id is one of
/// <see cref="AudioDeviceState.Id"/>'s, so a row can name the device it
/// points at.
/// </param>
/// <param name="CanRoute">
/// Whether awoo can offer to move this application's sound to another
/// device: false where Windows would not give up the routing interface, and
/// false for a row that has no process of its own to route.
/// </param>
internal readonly record struct AudioAppState(
    string Id,
    string Name,
    string? IconFile,
    SessionKind Kind,
    int Percent,
    bool IsMuted,
    bool IsSystemSounds,
    string? RoutedDeviceId,
    bool CanRoute);
