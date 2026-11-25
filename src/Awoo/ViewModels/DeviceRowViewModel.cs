using System.Collections.ObjectModel;
using System.ComponentModel;
using Awoo.Audio;
using Awoo.Settings;

namespace Awoo.ViewModels;

/// <summary>
/// One audio endpoint as a row in the Devices tab: its name, its volume and
/// mute, and which of Windows' defaults for its kind it holds — one marker
/// for all three roles, or a marker each for audio and for calls, as the
/// setting has it.
/// </summary>
/// <remarks>
/// <para>
/// Public only because a compiled binding's <c>x:DataType</c> names it from
/// XAML; nothing outside the app constructs one.
/// </para>
/// <para>
/// Like <see cref="VolumeViewModel"/>, the settable properties are
/// write-through: they ask the service and wait to be told what the endpoint
/// took, which is what stops a two-way binding looping on its own echo.
/// </para>
/// </remarks>
public sealed class DeviceRowViewModel : INotifyPropertyChanged
{
    /// <summary>What the spatial menu calls plain stereo.</summary>
    private const string SpatialOffName = "Off";

    private readonly IAudioService _audio;
    private readonly SettingsService _settings;
    private AudioDeviceState _state;

    internal DeviceRowViewModel(IAudioService audio, SettingsService settings, AudioDeviceState state)
    {
        _audio = audio;
        _settings = settings;
        _state = state;

        // The radio group is per kind: an output and an input are each the
        // default of their own list, so they must not unselect each other.
        // The split view's two columns are two groups again, for the same
        // reason — and groups of their own rather than the unsplit one's, so
        // the markers that are not on screen cannot unselect the ones that
        // are.
        GroupName = $"awoo.default.{state.Kind}";
        AudioGroupName = $"awoo.default.audio.{state.Kind}";
        CommunicationsGroupName = $"awoo.default.communications.{state.Kind}";
        CanSetDefault = audio.CanSetDefaultDevice;

        // This one is per device: spatial sound is a property of the
        // endpoint, so every row's menu is a group of its own.
        SpatialGroupName = $"awoo.spatial.{state.Id}";
        SpatialGlyph = VolumeGlyph.Spatial;
        RebuildSpatial();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Core Audio's endpoint id — how a row is matched to a device.</summary>
    public string Id => _state.Id;

    public string Name => _state.Name;

    /// <summary>Which radio group this row's single default marker belongs to.</summary>
    public string GroupName { get; }

    /// <summary>Which group the split view's default audio marker belongs to.</summary>
    public string AudioGroupName { get; }

    /// <summary>Which group the split view's communications marker belongs to.</summary>
    public string CommunicationsGroupName { get; }

    /// <summary>
    /// Whether this row draws the two markers rather than one: the setting,
    /// read where it lives rather than copied, so the row and the settings
    /// window can never disagree about it.
    /// </summary>
    public bool IsSplit => _settings.SplitDefaultRoles;

    /// <summary>
    /// The other half of <see cref="IsSplit"/>, because a template cannot
    /// negate a binding: whichever of the two layouts is not shown is
    /// collapsed, and one of them always is.
    /// </summary>
    public bool IsNotSplit => !IsSplit;

    /// <summary>
    /// False where this build of Windows would not give up the policy-config
    /// interface, which turns the default marker into a read-only indicator
    /// rather than a button that silently does nothing.
    /// </summary>
    public bool CanSetDefault { get; }

    /// <summary>Volume 0–100. A double because that is what a Slider binds to.</summary>
    public double Percent
    {
        get => _state.Percent;
        set
        {
            var percent = (int)Math.Round(value);
            if (percent != _state.Percent)
            {
                _audio.SetDevicePercent(_state.Id, percent);
            }
        }
    }

    public bool IsMuted
    {
        get => _state.IsMuted;
        set
        {
            if (value != _state.IsMuted)
            {
                _audio.SetDeviceMute(_state.Id, value);
            }
        }
    }

    /// <summary>
    /// Whether Windows' default for this kind of device is this one — what
    /// the single marker shows, and what the split view's first marker shows.
    /// </summary>
    public bool IsDefault => _state.IsDefault;

    /// <summary>Whether this is the device Windows puts calls through.</summary>
    public bool IsDefaultCommunications => _state.IsDefaultCommunications;

    public string PercentText => $"{_state.Percent}%";

    /// <summary>The mute button's glyph: an output's level, or a microphone.</summary>
    public string MuteGlyph => VolumeGlyph.ForMute(_state.Kind, Level);

    public string MuteToolTip => _state.IsMuted ? "Unmute" : "Mute";

    /// <summary>
    /// The single marker's tooltip. Unqualified, because its one click sets
    /// every role Windows has.
    /// </summary>
    public string DefaultToolTip => ToolTipFor(_state.IsDefault, "Default device", "Set as default device");

    /// <summary>
    /// The split view's first marker: the two columns are told apart by
    /// their tooltips alone, so this one says which of the two it is.
    /// </summary>
    public string DefaultAudioToolTip =>
        ToolTipFor(_state.IsDefault, "Default audio device", "Set as default audio device");

    public string DefaultCommunicationsToolTip => ToolTipFor(
        _state.IsDefaultCommunications,
        "Default communications device",
        "Set as default communications device");

    /// <summary>
    /// What a screen reader reads for the split view's first marker. The
    /// device's name alone would have the two markers of a row announce
    /// themselves identically, so each carries the role it sets.
    /// </summary>
    public string DefaultAudioAutomationName => $"{_state.Name}, default audio device";

    public string DefaultCommunicationsAutomationName =>
        $"{_state.Name}, default communications device";

    /// <summary>
    /// Whether to offer this row's spatial sound control at all. False for
    /// every input, for an output Windows says has no spatial sound, and for
    /// one where no format is usable — the control is absent rather than
    /// disabled, the same manners as the route selector on an application
    /// that cannot be routed.
    /// </summary>
    public bool CanSetSpatial => _state.Spatial.CanSet;

    /// <summary>Sound going out in every direction: what the control is about.</summary>
    public string SpatialGlyph { get; }

    /// <summary>Which radio group this row's spatial menu is, so two rows never collide.</summary>
    public string SpatialGroupName { get; }

    /// <summary>Every format this device can be put into, "Off" first.</summary>
    public ObservableCollection<SpatialOptionViewModel> SpatialOptions { get; } = [];

    /// <summary>
    /// Whether this device is in a spatial format rather than plain stereo.
    /// What makes the button name the format: off is the quiet common case
    /// and shows the glyph alone, the way an application following the
    /// default device does.
    /// </summary>
    public bool HasSpatial => _state.Spatial.Active is not null;

    /// <summary>The format this device is in, as the button names it.</summary>
    public string SpatialName => _state.Spatial.Active?.Name ?? SpatialOffName;

    public string SpatialToolTip => _state.Spatial.Active is { } active
        ? $"Spatial sound: {active.Name}"
        : "Spatial sound is off";

    private VolumeLevel Level => VolumeState.LevelOf(_state.Percent, _state.IsMuted, true);

    /// <summary>
    /// What a default marker says on hover: what it is where it is already
    /// selected, and what clicking it would do where it is not — unless this
    /// build of Windows would not give up the switch, in which case the
    /// marker is an indicator and promises nothing.
    /// </summary>
    private string ToolTipFor(bool holds, string held, string set) =>
        holds || !CanSetDefault ? held : set;

    /// <summary>
    /// Takes a fresh snapshot of the same endpoint, raising only what moved.
    /// Updating in place rather than replacing the row is what lets a slider
    /// be dragged while the service publishes every step of it.
    /// </summary>
    internal void Update(AudioDeviceState state)
    {
        var previous = _state;
        _state = state;

        if (state.Name != previous.Name)
        {
            Raise(nameof(Name));
            Raise(nameof(DefaultAudioAutomationName));
            Raise(nameof(DefaultCommunicationsAutomationName));
        }

        if (state.Percent != previous.Percent)
        {
            Raise(nameof(Percent));
            Raise(nameof(PercentText));
        }

        if (state.IsMuted != previous.IsMuted)
        {
            Raise(nameof(IsMuted));
            Raise(nameof(MuteToolTip));
        }

        if (state.IsDefault != previous.IsDefault)
        {
            Raise(nameof(IsDefault));
            Raise(nameof(DefaultToolTip));
            Raise(nameof(DefaultAudioToolTip));
        }

        if (state.IsDefaultCommunications != previous.IsDefaultCommunications)
        {
            Raise(nameof(IsDefaultCommunications));
            Raise(nameof(DefaultCommunicationsToolTip));
        }

        if (VolumeState.LevelOf(state.Percent, state.IsMuted, true) !=
            VolumeState.LevelOf(previous.Percent, previous.IsMuted, true))
        {
            Raise(nameof(MuteGlyph));
        }

        if (!state.Spatial.Equals(previous.Spatial))
        {
            RebuildSpatial();
        }
    }

    /// <summary>
    /// Re-asserts <see cref="IsDefault"/> whatever it is. Clicking a radio
    /// button unselects its siblings locally before the service has said
    /// anything, so every row in the group is asked to state its own case
    /// again once the switch has gone through — including the row that was
    /// already the default, when the switch failed.
    /// </summary>
    internal void RestateDefault()
    {
        Raise(nameof(IsDefault));
        Raise(nameof(IsDefaultCommunications));
        Raise(nameof(DefaultToolTip));
        Raise(nameof(DefaultAudioToolTip));
        Raise(nameof(DefaultCommunicationsToolTip));
    }

    /// <summary>
    /// Says the split setting has moved, which is what redraws an open
    /// flyout: the row swaps one marker for two, or two for one, and then
    /// restates which of them are selected — the markers coming on screen
    /// have never been asked.
    /// </summary>
    internal void RestateSplit()
    {
        Raise(nameof(IsSplit));
        Raise(nameof(IsNotSplit));
        RestateDefault();
    }

    /// <summary>
    /// Makes this device the default for its kind. Bound to the radio
    /// button's <c>Checked</c> rather than to a command, because a command on
    /// a ToggleButton only runs on a real click: selecting the row with the
    /// keyboard, or through UI Automation, sets the state without one. The
    /// guard is what makes that safe — the binding checks this row when the
    /// service says it became the default, and that echo asks for a switch
    /// that has already happened.
    /// </summary>
    public void MakeDefault()
    {
        if (CanSetDefault && !_state.IsDefault)
        {
            _audio.SetDefaultDevice(_state.Id, DefaultRole.All);
        }
    }

    /// <summary>
    /// Makes this device the default audio device for its kind — console and
    /// multimedia, what Windows' own Sound settings moves — leaving calls
    /// where they are. The split view's first marker; guarded like
    /// <see cref="MakeDefault"/>, and for the same reason.
    /// </summary>
    public void MakeDefaultAudio()
    {
        if (CanSetDefault && !_state.IsDefault)
        {
            _audio.SetDefaultDevice(_state.Id, DefaultRole.Audio);
        }
    }

    /// <summary>
    /// Makes this device the one Windows puts calls through, leaving
    /// everything else where it is. The split view's second marker.
    /// </summary>
    public void MakeDefaultCommunications()
    {
        if (CanSetDefault && !_state.IsDefaultCommunications)
        {
            _audio.SetDefaultDevice(_state.Id, DefaultRole.Communications);
        }
    }

    /// <summary>
    /// Puts this device into one spatial format, or back to plain stereo
    /// when <paramref name="formatId"/> is null.
    /// </summary>
    /// <remarks>
    /// The one write in the flyout that cannot be made and answered in a
    /// breath: Windows takes a spatial format asynchronously, so the service
    /// is awaited rather than trusted. By the time it returns, the format
    /// has been read back off the device and published, so every choice
    /// states its own case again — which is both how the new format gets
    /// ticked and how one Windows declined snaps back to where the device
    /// actually is.
    /// </remarks>
    internal async void ChooseSpatial(Guid? formatId)
    {
        if (!_state.Spatial.CanSet)
        {
            return;
        }

        await _audio.SetDeviceSpatialFormat(_state.Id, formatId);

        foreach (var option in SpatialOptions)
        {
            option.Restate();
        }
    }

    /// <summary>
    /// Brings the menu in line with the formats this device offers and the
    /// one it is in: "Off" first and then every format, matched by subtype so
    /// a format renamed by an update keeps its place and one that appears —
    /// a provider installed while awoo is running — is an inserted item
    /// rather than a rebuilt menu.
    /// </summary>
    private void RebuildSpatial()
    {
        var spatial = _state.Spatial;
        var wanted = new List<(Guid? Id, string Name)>(spatial.Offered.Count + 1) { (null, SpatialOffName) };

        foreach (var format in spatial.Offered)
        {
            wanted.Add((format.Id, format.Name));
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var (id, name) = wanted[index];

            // Off is selected exactly when no format is: both are null here.
            var selected = id == spatial.Active?.Id;
            var found = IndexOfSpatial(id, index);

            if (found < 0)
            {
                SpatialOptions.Insert(index, new SpatialOptionViewModel(this, id, name, selected));
                continue;
            }

            if (found != index)
            {
                SpatialOptions.Move(found, index);
            }

            SpatialOptions[index].Update(name, selected);
        }

        while (SpatialOptions.Count > wanted.Count)
        {
            SpatialOptions.RemoveAt(SpatialOptions.Count - 1);
        }

        Raise(nameof(CanSetSpatial));
        Raise(nameof(HasSpatial));
        Raise(nameof(SpatialName));
        Raise(nameof(SpatialToolTip));
    }

    private int IndexOfSpatial(Guid? formatId, int from)
    {
        for (var index = from; index < SpatialOptions.Count; index++)
        {
            if (SpatialOptions[index].FormatId == formatId)
            {
                return index;
            }
        }

        return -1;
    }

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
