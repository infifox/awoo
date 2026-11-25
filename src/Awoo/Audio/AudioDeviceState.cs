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
/// Which of Windows' default-device roles one "set as default" covers.
/// Windows keeps a default per role — console, multimedia and communications
/// — and awoo either moves all three together or sets the first two apart
/// from the third, depending on the setting.
/// </summary>
internal enum DefaultRole
{
    /// <summary>
    /// All three roles at once: what one radio button per device means, and
    /// what awoo does unless it has been told to split them.
    /// </summary>
    All,

    /// <summary>
    /// Console and multimedia together, which is what Windows' own Sound
    /// settings calls the default device. Leaves communications where it is.
    /// </summary>
    Audio,

    /// <summary>The communications role alone: the device calls go through.</summary>
    Communications,
}

/// <summary>
/// One spatial sound format an output can be put into — Windows Sonic, Dolby
/// Atmos and the rest — as <see cref="SpatialSound"/> found it.
/// </summary>
/// <param name="Id">
/// The subtype GUID Windows knows the format by, and what
/// <see cref="IAudioService.SetDeviceSpatialFormat"/> is given to ask for it.
/// </param>
/// <param name="Name">Windows' own name for it, as the menu shows it.</param>
internal readonly record struct SpatialFormat(Guid Id, string Name);

/// <summary>
/// What spatial sound one endpoint offers and which of it is in force. Its
/// own type because <see cref="AudioDeviceState"/> is compared by value and
/// a bare list inside one is compared by reference — which would have every
/// published list look changed, or never look changed, depending on which way
/// the collection was built.
/// </summary>
internal sealed class SpatialState : IEquatable<SpatialState>
{
    /// <summary>
    /// What an endpoint with no spatial sound to offer carries: an input,
    /// one Windows says cannot do it, and one nothing could be read from.
    /// Shared, so the common case allocates nothing.
    /// </summary>
    internal static readonly SpatialState None = new(null, []);

    internal SpatialState(SpatialFormat? active, IReadOnlyList<SpatialFormat> offered)
    {
        Active = active;
        Offered = offered;
    }

    /// <summary>The format in force, or null where spatial sound is off here.</summary>
    internal SpatialFormat? Active { get; }

    /// <summary>
    /// Every format this endpoint can be put into right now, in Windows'
    /// order. Formats the machine has not got, or whose licence does not
    /// cover this endpoint, are not in it — picking one of those is a thing
    /// that cannot be done rather than a thing that fails.
    /// </summary>
    internal IReadOnlyList<SpatialFormat> Offered { get; }

    /// <summary>
    /// Whether to offer the control at all. Nothing on offer means nothing to
    /// choose between, so the row shows no dropdown rather than a dead one.
    /// </summary>
    internal bool CanSet => Offered.Count > 0;

    public bool Equals(SpatialState? other) =>
        ReferenceEquals(this, other) ||
        (other is not null && Active == other.Active && Offered.SequenceEqual(other.Offered));

    public override bool Equals(object? obj) => Equals(obj as SpatialState);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Active);

        foreach (var format in Offered)
        {
            hash.Add(format);
        }

        return hash.ToHashCode();
    }
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
/// <param name="IsDefault">
/// Whether this is the default device for its kind — the console role, which
/// is what Windows' own Sound settings means by "default".
/// </param>
/// <param name="IsDefaultMultimedia">
/// Whether this holds the multimedia role for its kind. It rides with the
/// console role in everything awoo or Windows' own settings write, so the UI
/// never shows it — but a per-role switcher can move it alone, and the
/// default-device lock reads this to put it back.
/// </param>
/// <param name="IsDefaultCommunications">
/// Whether this is the communications default for its kind: the device
/// Windows puts calls through. The same device as <paramref name="IsDefault"/>
/// unless something has set the two apart.
/// </param>
/// <param name="Spatial">
/// What spatial sound this endpoint offers and is in.
/// <see cref="SpatialState.None"/> for every input and for an output that has
/// none.
/// </param>
internal readonly record struct AudioDeviceState(
    string Id,
    string Name,
    DeviceKind Kind,
    int Percent,
    bool IsMuted,
    bool IsDefault,
    bool IsDefaultMultimedia,
    bool IsDefaultCommunications,
    SpatialState Spatial);
