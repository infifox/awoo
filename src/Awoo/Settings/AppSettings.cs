using System.Text.Json.Serialization;

namespace Awoo.Settings;

/// <summary>
/// Everything awoo remembers between runs. A record rather than a mutable
/// object, so a change is a new value that can be compared with the old one —
/// which is what keeps the file from being rewritten for nothing.
/// </summary>
/// <remarks>
/// "Start with Windows" is deliberately not here: Windows keeps that, under
/// the user's Run key, and a copy of it in this file could only ever disagree
/// with the thing that actually decides. See <see cref="Autostart"/>.
/// </remarks>
internal sealed record AppSettings
{
    /// <summary>What the Plasma applet scrolls by, and so what awoo does.</summary>
    internal const int DefaultVolumeStep = 5;

    /// <summary>A step of nothing is not a step, and a quarter of the range is plenty.</summary>
    internal const int MinVolumeStep = 1;

    internal const int MaxVolumeStep = 25;

    /// <summary>
    /// How endpoint ids are compared, here and wherever else awoo matches
    /// one: Core Audio hands the same id back in the same form every time,
    /// but this file is the user's to edit and a hand-typed id should still
    /// match the device it names.
    /// </summary>
    internal static readonly StringComparer DeviceIdComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>Null where the file said nothing; see <see cref="HiddenDeviceIds"/>.</summary>
    private readonly IReadOnlyList<string>? _hiddenDeviceIds;

    /// <summary>
    /// How far one notch of the wheel over the tray icon moves the volume, in
    /// percentage points.
    /// </summary>
    public int VolumeStep { get; init; } = DefaultVolumeStep;

    /// <summary>
    /// Whether awoo has already had its one go at switching autostart on. On
    /// by default was settled with the human, and a default can only be
    /// applied once: anything else turns the setting back on behind somebody
    /// who turned it off.
    /// </summary>
    public bool AutostartRegistered { get; init; }

    /// <summary>
    /// Whether the Devices tab draws the default audio device and the
    /// default communications device as two markers per row rather than one.
    /// Off is one marker that moves all three of Windows' roles together.
    /// </summary>
    public bool SplitDefaultRoles { get; init; }

    /// <summary>
    /// Whether awoo holds Windows to the devices the user selected, putting
    /// back any default changed from outside awoo. Off is Windows' own
    /// behaviour, where a device appearing may take the default with it.
    /// </summary>
    public bool LockDefaultDevices { get; init; }

    /// <summary>
    /// The output the lock holds as the device everything plays through, by
    /// Core Audio endpoint id, or null where nothing is remembered for it.
    /// </summary>
    /// <remarks>
    /// Kept through the device being away, the way
    /// <see cref="HiddenDeviceIds"/> is and for the same reason: the lock
    /// leaves Windows' fallback alone while the chosen device is unplugged and
    /// retakes it the moment it is back, which it can only do if it still
    /// knows which device it was.
    /// </remarks>
    public string? LockedOutputId { get; init; }

    /// <summary>The input the lock holds, as <see cref="LockedOutputId"/>.</summary>
    public string? LockedInputId { get; init; }

    /// <summary>The output the lock holds as the device calls go through.</summary>
    public string? LockedCommunicationsOutputId { get; init; }

    /// <summary>The input the lock holds for calls.</summary>
    public string? LockedCommunicationsInputId { get; init; }

    /// <summary>The four remembered ids as the lock reads them.</summary>
    internal LockedDefaults LockedDefaults => new(
        LockedOutputId,
        LockedInputId,
        LockedCommunicationsOutputId,
        LockedCommunicationsInputId);

    /// <summary>
    /// The endpoints the user has hidden from the volume control, by Core
    /// Audio endpoint id — stable across unplugging and replugging, so a
    /// device that is away keeps its flag and comes back hidden.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read through <see cref="DeviceIdListConverter"/>, which keeps whatever
    /// of a hand-edited list is a string and drops the rest. The ids
    /// themselves are not checked against anything: a device does not have to
    /// be plugged in to be hidden, so an id awoo has never seen is a device
    /// that is simply not here.
    /// </para>
    /// <para>
    /// Behind a field with a getter that never answers null, because the
    /// generated deserializer does not run this type's initializers: a file
    /// with no <c>hiddenDeviceIds</c> in it — every file awoo wrote before
    /// this setting existed — leaves the property at its default, which for
    /// a list is nothing at all.
    /// </para>
    /// </remarks>
    [JsonConverter(typeof(DeviceIdListConverter))]
    public IReadOnlyList<string> HiddenDeviceIds
    {
        get => _hiddenDeviceIds ?? [];
        init => _hiddenDeviceIds = value;
    }

    /// <summary>The same settings with one new set of remembered ids.</summary>
    internal AppSettings With(LockedDefaults locked) => this with
    {
        LockedOutputId = locked.Output,
        LockedInputId = locked.Input,
        LockedCommunicationsOutputId = locked.CommunicationsOutput,
        LockedCommunicationsInputId = locked.CommunicationsInput,
    };

    /// <summary>
    /// The same settings with anything out of range brought back into it. The
    /// file is the user's to edit, and an edit does not have to be sensible.
    /// </summary>
    internal AppSettings Normalised()
    {
        var step = Math.Clamp(VolumeStep, MinVolumeStep, MaxVolumeStep);
        var hidden = NormalisedHiddenIds();
        var locked = NormalisedLockedDefaults();

        return step == VolumeStep && hidden is null && locked == LockedDefaults
            ? this
            : (this with { VolumeStep = step, HiddenDeviceIds = hidden ?? HiddenDeviceIds }).With(locked);
    }

    /// <summary>
    /// Equality over the values rather than over the list's identity, which
    /// is what <see cref="SettingsService"/> needs to tell a change from a
    /// write for nothing: a record's generated comparison would call two
    /// equal lists different for being two objects.
    /// </summary>
    public bool Equals(AppSettings? other) =>
        other is not null &&
        VolumeStep == other.VolumeStep &&
        AutostartRegistered == other.AutostartRegistered &&
        SplitDefaultRoles == other.SplitDefaultRoles &&
        LockDefaultDevices == other.LockDefaultDevices &&
        SameId(LockedOutputId, other.LockedOutputId) &&
        SameId(LockedInputId, other.LockedInputId) &&
        SameId(LockedCommunicationsOutputId, other.LockedCommunicationsOutputId) &&
        SameId(LockedCommunicationsInputId, other.LockedCommunicationsInputId) &&
        HiddenDeviceIds.SequenceEqual(other.HiddenDeviceIds, DeviceIdComparer);

    public override int GetHashCode() => HashCode.Combine(
        VolumeStep,
        AutostartRegistered,
        SplitDefaultRoles,
        LockDefaultDevices,
        HiddenDeviceIds.Count);

    /// <summary>
    /// Whether two remembered ids name the same device, nulls included — the
    /// same comparison <see cref="DeviceIdComparer"/> makes everywhere else,
    /// so a hand-typed id does not read as a different device.
    /// </summary>
    private static bool SameId(string? left, string? right) => DeviceIdComparer.Equals(left, right);

    /// <summary>
    /// The hidden list with the entries that name nothing taken out and the
    /// repeats collapsed, or null where it was already like that — the file
    /// is the user's to edit, and a blank or a duplicate id is not a device.
    /// </summary>
    private IReadOnlyList<string>? NormalisedHiddenIds()
    {
        var kept = new List<string>(HiddenDeviceIds.Count);
        var seen = new HashSet<string>(DeviceIdComparer);

        foreach (var id in HiddenDeviceIds)
        {
            if (!string.IsNullOrWhiteSpace(id) && seen.Add(id.Trim()))
            {
                kept.Add(id.Trim());
            }
        }

        return kept.Count == HiddenDeviceIds.Count && kept.SequenceEqual(HiddenDeviceIds)
            ? null
            : kept;
    }

    /// <summary>
    /// The remembered ids with anything that names no device read as nothing
    /// remembered. Whether the id is a device awoo has ever seen is not asked:
    /// a locked device does not have to be plugged in.
    /// </summary>
    private LockedDefaults NormalisedLockedDefaults() => new(
        NormalisedId(LockedOutputId),
        NormalisedId(LockedInputId),
        NormalisedId(LockedCommunicationsOutputId),
        NormalisedId(LockedCommunicationsInputId));

    private static string? NormalisedId(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : id.Trim();
}
