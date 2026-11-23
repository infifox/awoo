namespace Awoo.Audio;

/// <summary>Which way sound flows through a device, as awoo groups them.</summary>
internal enum DeviceKind
{
    /// <summary>An output: speakers, headphones, a monitor's audio.</summary>
    Output,

    /// <summary>An input: a microphone, a line in.</summary>
    Input,
}

/// <summary>
/// One audio endpoint as the Devices tab shows it. A snapshot, like
/// <see cref="VolumeState"/>, so a reader never sees half of a change.
/// </summary>
/// <param name="Id">
/// The endpoint id Core Audio gives it — stable across unplugging and
/// replugging, which is what lets a row be matched to a device rather than to
/// a position in a list.
/// </param>
/// <param name="Name">The endpoint's friendly name, as Windows shows it.</param>
/// <param name="Kind">Output or input.</param>
/// <param name="Percent">Volume, 0–100.</param>
/// <param name="IsMuted">Whether the endpoint is muted.</param>
/// <param name="IsDefault">Whether this is the default device for its kind.</param>
internal readonly record struct AudioDeviceState(
    string Id,
    string Name,
    DeviceKind Kind,
    int Percent,
    bool IsMuted,
    bool IsDefault);
