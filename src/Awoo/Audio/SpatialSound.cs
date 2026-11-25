using System.Runtime.InteropServices;
using System.Text;
using Windows.Media.Audio;

namespace Awoo.Audio;

/// <summary>
/// Spatial sound on one output endpoint: which formats Windows will take
/// there, which of them is in force, and putting it into another one. This is
/// the setting Windows' own volume flyout offers and Core Audio does not.
/// </summary>
/// <remarks>
/// <para>
/// Half of this is the documented Windows Runtime API —
/// <see cref="SpatialAudioDeviceConfiguration"/> reads the format in force
/// and sets a new one, and it does both from awoo's ordinary unelevated
/// context. The other half is working out what to offer, which that API
/// cannot answer: <c>IsSpatialAudioFormatSupported</c> says yes to formats
/// the setter then refuses, because it asks whether the endpoint could carry
/// the format rather than whether the machine has it to give. So the list
/// comes from the endpoint's own property store instead, where Windows
/// publishes a record per format with a flag saying whether it is usable
/// here and now — documented interface, undocumented key.
/// </para>
/// <para>
/// Like <see cref="AppRouting"/>, nothing here throws: a Windows that moved
/// any of this leaves <see cref="IsAvailable"/> false or the offered list
/// empty, and the UI drops the control rather than showing one that cannot
/// work.
/// </para>
/// <para>
/// Instances are not thread-safe and are not meant to be: like everything
/// else in the audio layer, this is touched only on the thread
/// <see cref="AudioService"/> runs on. The one exception is the wait inside
/// <see cref="SetAsync"/>, which is explicitly taken off that thread.
/// </para>
/// </remarks>
internal sealed class SpatialSound
{
    /// <summary>
    /// What a Core Audio endpoint id has to be wrapped in before the spatial
    /// API will take it: the same MMDEVAPI device interface path
    /// <see cref="AppRouting"/> wraps one in.
    /// </summary>
    private const string EndpointPrefix = @"\\?\SWD#MMDEVAPI#";

    private const string RenderSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

    /// <summary>VT_BLOB — what every record in the format list is stored as.</summary>
    private const ushort VtBlob = 65;

    /// <summary>
    /// Where the format list starts. Slot 0 and 1 hold something else, and
    /// the formats run upwards from here; the ceiling is a guard rather than
    /// a count, since a miss costs one failed read.
    /// </summary>
    private const int FirstSlot = 2;

    private const int LastSlot = 17;

    /// <summary>How much room the record gives Windows' own name for the format.</summary>
    private const int NameLength = 512;

    /// <summary>Where the format's subtype GUID sits in the record.</summary>
    private const int SubtypeOffset = 768;

    /// <summary>
    /// Where the record says whether this format can be used on this endpoint
    /// right now: 1 where it can, 0 where the machine has not got it or its
    /// licence does not cover this endpoint. This is the flag the whole list
    /// is read for — it is what the setter agrees with, and
    /// <c>IsSpatialAudioFormatSupported</c> is not.
    /// </summary>
    private const int UsableOffset = 784;

    /// <summary>A record shorter than this is not the shape read here.</summary>
    private const int MinimumRecordLength = UsableOffset + sizeof(int);

    /// <summary>
    /// The property key family Windows lists an endpoint's spatial formats
    /// under, one slot per format. Undocumented, and read through the
    /// documented <see cref="IPropertyStore"/> like any other endpoint
    /// property.
    /// </summary>
    private static readonly Guid AvailableFormats = new("a45429a4-aa63-4480-b7f8-3f2552daee93");

    /// <summary>
    /// Where the endpoint keeps the format it is in. Never read — the
    /// documented API answers that — but watched, because it is the key
    /// Windows announces a change of spatial sound on.
    /// </summary>
    private static readonly Guid SelectedFormat = new("8a845654-d6c3-4cd7-b4eb-243d4bd99032");

    /// <summary>
    /// Windows' own name for each format it ships knowing about, keyed by the
    /// subtype GUIDs <see cref="SpatialAudioFormatSubtype"/> names. Only a
    /// fallback: the format list carries the same strings, in the language
    /// Windows is running in, and those are preferred where they read.
    /// </summary>
    private static readonly Dictionary<Guid, string> Names = new()
    {
        [new Guid("{B53D940C-B846-4831-9F76-D102B9B725A0}")] = "Windows Sonic for Headphones",
        [new Guid("{1459AC38-3875-49BF-BB59-0FE80F4D395D}")] = "Dolby Atmos for Headphones",
        [new Guid("{4C81E564-C8EF-4AD9-9F2C-9EF995533790}")] = "Dolby Atmos for Speakers",
        [new Guid("{A289735D-FA3E-4E35-9D7D-B6F896ACB2E7}")] = "Dolby Atmos for home theater",
        [new Guid("{4444ACB0-8DC0-4C2C-A0D8-2C76DB470F86}")] = "DTS Headphone:X",
        [new Guid("{ADAFD3C6-AC4C-404A-836A-9615E9060564}")] = "DTS Virtual:X for speakers",
        [new Guid("{10201B4A-3322-4967-BF40-2CAA9BAFCA44}")] = "DTS:X for home theater",
    };

    /// <summary>What a format nobody here has a name for is called.</summary>
    private const string UnknownName = "Spatial format";

    private readonly bool _available;

    private SpatialSound(bool available) => _available = available;

    /// <summary>
    /// Whether this build of Windows has the spatial API at all. False turns
    /// every other method here into "no spatial sound on any device".
    /// </summary>
    internal bool IsAvailable => _available;

    /// <summary>
    /// Asks Windows for the spatial configuration class once, or returns an
    /// instance that can do nothing where it has not got it. Call it on the
    /// thread that will use it, like the rest of the audio layer.
    /// </summary>
    /// <remarks>
    /// The probe names no real device on purpose: the class answers for any
    /// well-formed endpoint path, so what is being tested is whether it
    /// activates. A Windows without it fails here, once, rather than on every
    /// row of every refresh.
    /// </remarks>
    internal static SpatialSound Create()
    {
        try
        {
            _ = SpatialAudioDeviceConfiguration.GetForDeviceId(
                PathOf("{0.0.0.00000000}.{00000000-0000-0000-0000-000000000000}"));

            return new SpatialSound(true);
        }
        catch (Exception e) when (e is COMException or TypeLoadException or NotSupportedException
                                      or ArgumentException or DllNotFoundException
                                      or EntryPointNotFoundException)
        {
            // A Windows that moved, renamed or withdrew the class. Nothing to
            // do but go without spatial sound.
            return new SpatialSound(false);
        }
    }

    /// <summary>
    /// Whether a property Windows says has changed is one spatial sound is
    /// read out of — the format in force, or the list of formats on offer.
    /// Both land on <see cref="IMMNotificationClient.OnPropertyValueChanged"/>
    /// when anything changes spatial sound, awoo included, which is what
    /// saves this from being polled for.
    /// </summary>
    internal static bool Announces(PropertyKey key) =>
        key.FormatId == AvailableFormats || key.FormatId == SelectedFormat;

    /// <summary>
    /// What spatial sound one endpoint offers and is in.
    /// <see cref="SpatialState.None"/> for an input, for an endpoint Windows
    /// says has no spatial sound, and wherever nothing could be read — all of
    /// which the UI treats the same way, by showing nothing.
    /// </summary>
    internal SpatialState Read(IMMDevice device, string deviceId, DeviceKind kind)
    {
        // Windows has no spatial sound for capture endpoints, and asking
        // about one answers nothing useful.
        if (!_available || kind != DeviceKind.Output)
        {
            return SpatialState.None;
        }

        SpatialAudioDeviceConfiguration configuration;

        try
        {
            configuration = SpatialAudioDeviceConfiguration.GetForDeviceId(PathOf(deviceId));

            if (!configuration.IsSpatialAudioSupported)
            {
                return SpatialState.None;
            }
        }
        catch (Exception e) when (e is COMException or ArgumentException)
        {
            return SpatialState.None;
        }

        var offered = Offered(device);
        var active = ActiveOn(configuration);

        if (active is { } id)
        {
            // Named from the list where the list has it, so the button face
            // and the ticked option never call the same format two things.
            var listed = offered.FindIndex(format => format.Id == id);

            if (listed < 0)
            {
                // In force but not on offer: something else set it, or the
                // licence lapsed under it. It is still named and still
                // listed, because otherwise there is no way back off it.
                offered.Insert(0, new SpatialFormat(id, NameOf(id)));
                listed = 0;
            }

            return new SpatialState(offered[listed], offered);
        }

        return offered.Count == 0 ? SpatialState.None : new SpatialState(null, offered);
    }

    /// <summary>
    /// Puts one endpoint into one spatial format, or turns spatial sound off
    /// there when <paramref name="formatId"/> is null. False where Windows
    /// declined it — a format the machine has not got, or whose licence does
    /// not cover this endpoint — and where the API is missing.
    /// </summary>
    /// <remarks>
    /// The only asynchronous call in the audio layer, because Windows makes
    /// it one. The wait is taken off the caller's thread deliberately: that
    /// thread is the UI thread, and blocking it on a Windows Runtime
    /// operation is how an app stops answering rather than how a call
    /// finishes.
    /// </remarks>
    internal async Task<bool> SetAsync(string deviceId, Guid? formatId)
    {
        if (!_available)
        {
            return false;
        }

        try
        {
            var configuration = SpatialAudioDeviceConfiguration.GetForDeviceId(PathOf(deviceId));

            // The all-zero subtype is how the API is told to turn spatial
            // sound off, rather than there being a method for it.
            var subtype = (formatId ?? Guid.Empty).ToString("B").ToUpperInvariant();

            var result = await configuration.SetDefaultSpatialAudioFormatAsync(subtype)
                .AsTask()
                .ConfigureAwait(false);

            return result.Status == SetDefaultSpatialAudioFormatStatus.Succeeded;
        }
        catch (Exception e) when (e is COMException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Wraps a Core Audio endpoint id as the device interface path the API takes.</summary>
    private static string PathOf(string deviceId) => EndpointPrefix + deviceId + RenderSuffix;

    /// <summary>
    /// The format in force on an endpoint, or null where spatial sound is
    /// off there. The <em>default</em> format rather than the active one:
    /// that is the setting being mirrored, where the active one is what is
    /// playing through right now and goes blank with nothing to play.
    /// </summary>
    private static Guid? ActiveOn(SpatialAudioDeviceConfiguration configuration)
    {
        try
        {
            return Guid.TryParse(configuration.DefaultSpatialAudioFormat, out var id) && id != Guid.Empty
                ? id
                : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>
    /// Every format this endpoint can be put into right now, in the order
    /// Windows lists them. Empty where the list could not be read, which the
    /// caller treats as having no spatial sound to offer.
    /// </summary>
    private static List<SpatialFormat> Offered(IMMDevice device)
    {
        var formats = new List<SpatialFormat>();

        if (device.OpenPropertyStore(CoreAudio.StgmRead, out var store) != CoreAudio.S_OK || store is null)
        {
            return formats;
        }

        for (var slot = FirstSlot; slot <= LastSlot; slot++)
        {
            var key = new PropertyKey(AvailableFormats, slot);

            if (store.GetValue(ref key, out var value) != CoreAudio.S_OK)
            {
                continue;
            }

            try
            {
                if (Parse(value) is { } format && formats.TrueForAll(other => other.Id != format.Id))
                {
                    formats.Add(format);
                }
            }
            finally
            {
                _ = CoreAudio.PropVariantClear(ref value);
            }
        }

        return formats;
    }

    /// <summary>
    /// Pulls one format out of its record, or null where the record is not
    /// the shape this reads or says the format cannot be used here. Every
    /// field is checked rather than trusted: the record is undocumented, so
    /// a Windows that changes it has to come out as "nothing on offer"
    /// rather than as nonsense in a menu.
    /// </summary>
    private static SpatialFormat? Parse(PropVariant value)
    {
        if (value.Type != VtBlob || value.Value2 == IntPtr.Zero)
        {
            return null;
        }

        // A VT_BLOB is a length and a pointer, in that order, inside the
        // union both of these fields overlay.
        var length = (int)value.Value;

        if (length < MinimumRecordLength)
        {
            return null;
        }

        var record = new byte[length];
        Marshal.Copy(value.Value2, record, 0, length);

        var id = new Guid(record.AsSpan(SubtypeOffset, 16));

        if (id == Guid.Empty || BitConverter.ToInt32(record, UsableOffset) != 1)
        {
            return null;
        }

        return new SpatialFormat(id, NameIn(record) ?? NameOf(id));
    }

    /// <summary>
    /// Windows' own name for the format, out of the front of its record: a
    /// NUL-terminated string in a fixed-width field, in whatever language
    /// Windows is running in. Null where the field holds nothing readable.
    /// </summary>
    private static string? NameIn(byte[] record)
    {
        var end = 0;

        while (end + 1 < NameLength && (record[end] != 0 || record[end + 1] != 0))
        {
            end += 2;
        }

        var name = Encoding.Unicode.GetString(record, 0, end).Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>
    /// What to call a format by its subtype alone. A format nobody here has
    /// heard of still has to be named, since it may be the one in force.
    /// </summary>
    private static string NameOf(Guid id) => Names.TryGetValue(id, out var name) ? name : UnknownName;
}
