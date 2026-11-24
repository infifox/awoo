using System.Collections.ObjectModel;
using System.ComponentModel;
using Awoo.Audio;
using Microsoft.UI.Xaml.Media;

namespace Awoo.ViewModels;

/// <summary>
/// One application as a row in the Applications tab: its icon and name, the
/// output its sound goes to, and its own volume and mute under the device's.
/// </summary>
/// <remarks>
/// <para>
/// Public only because a compiled binding's <c>x:DataType</c> names it from
/// XAML; nothing outside the app constructs one.
/// </para>
/// <para>
/// Like <see cref="DeviceRowViewModel"/>, the settable properties are
/// write-through: they ask the service and wait to be told what the session
/// took, which is what stops a two-way binding looping on its own echo.
/// </para>
/// </remarks>
public sealed class AppRowViewModel : INotifyPropertyChanged
{
    /// <summary>What the menu calls following whichever device Windows is using.</summary>
    private const string DefaultRouteName = "Default";

    /// <summary>
    /// What a row says when its sound has been sent to a device that is not
    /// there any more — unplugged since, or chosen in Windows' Settings while
    /// it was. The route still stands, so it is named rather than hidden.
    /// </summary>
    private const string MissingRouteName = "Unavailable device";

    private readonly IAudioService _audio;

    private AudioAppState _state;
    private IReadOnlyList<AudioDeviceState> _outputs;

    internal AppRowViewModel(IAudioService audio, AudioAppState state, IReadOnlyList<AudioDeviceState> outputs)
    {
        _audio = audio;
        _state = state;
        _outputs = outputs;
        FallbackGlyph = VolumeGlyph.ForSession(state.Kind);
        RouteGlyph = VolumeGlyph.ForKind(DeviceKind.Output);
        RouteGroupName = $"awoo.route.{state.Id}";
        Icon = AppIconLoader.Load(state.IconFile);
        RebuildRoutes();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The service's key for this application — how a row is matched to one.</summary>
    public string Id => _state.Id;

    public string Name => _state.Name;

    /// <summary>The application's icon, or null where none could be extracted.</summary>
    public ImageSource? Icon { get; private set; }

    public bool HasIcon => Icon is not null;

    /// <summary>
    /// The companion of <see cref="HasIcon"/>: a row shows one or the other,
    /// and x:Bind has no negation to work it out from a single property.
    /// </summary>
    public bool HasNoIcon => Icon is null;

    /// <summary>What to draw in the icon's place when there is no icon.</summary>
    public string FallbackGlyph { get; }

    /// <summary>Volume 0–100. A double because that is what a Slider binds to.</summary>
    public double Percent
    {
        get => _state.Percent;
        set
        {
            var percent = (int)Math.Round(value);
            if (percent != _state.Percent)
            {
                _audio.SetAppPercent(_state.Id, percent);
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
                _audio.SetAppMute(_state.Id, value);
            }
        }
    }

    public string PercentText => $"{_state.Percent}%";

    /// <summary>The mute button's glyph: a playing app's level, or a microphone.</summary>
    public string MuteGlyph => VolumeGlyph.ForMute(_state.Kind, Level);

    public string MuteToolTip => _state.IsMuted ? "Unmute" : "Mute";

    /// <summary>
    /// Whether to offer this row's output-device selector at all. False where
    /// this build of Windows would not give up the routing interface, where
    /// the row is recording rather than playing, and for Windows' own
    /// system sounds, which belong to no application to route.
    /// </summary>
    public bool CanRoute => _state.CanRoute;

    /// <summary>The speaker on the selector: this is an output being chosen.</summary>
    public string RouteGlyph { get; }

    /// <summary>Which radio group this row's device menu is, so two rows never collide.</summary>
    public string RouteGroupName { get; }

    /// <summary>Every device this row's sound could go to, "Default" first.</summary>
    public ObservableCollection<RouteOptionViewModel> RouteOptions { get; } = [];

    /// <summary>
    /// Whether this application has been sent to a device of its own rather
    /// than following the default. What makes the selector name the device:
    /// the common case is to show the speaker alone and stay out of the way.
    /// </summary>
    public bool HasRoute => _state.RoutedDeviceId is not null;

    /// <summary>The device this row's sound goes to, as the selector names it.</summary>
    public string RouteName => _state.RoutedDeviceId is not { } routed
        ? DefaultRouteName
        : NameOf(routed) ?? MissingRouteName;

    public string RouteToolTip => _state.RoutedDeviceId is null
        ? "Playing on the default output device"
        : $"Playing on {RouteName}";

    private VolumeLevel Level => VolumeState.LevelOf(_state.Percent, _state.IsMuted, true);

    /// <summary>
    /// Takes a fresh snapshot of the same application, raising only what
    /// moved. Updating in place rather than replacing the row is what lets a
    /// slider be dragged while the service publishes every step of it.
    /// </summary>
    internal void Update(AudioAppState state)
    {
        var previous = _state;
        _state = state;

        if (state.Name != previous.Name)
        {
            Raise(nameof(Name));
        }

        if (state.IconFile != previous.IconFile)
        {
            Icon = AppIconLoader.Load(state.IconFile);
            Raise(nameof(Icon));
            Raise(nameof(HasIcon));
            Raise(nameof(HasNoIcon));
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

        if (VolumeState.LevelOf(state.Percent, state.IsMuted, true) !=
            VolumeState.LevelOf(previous.Percent, previous.IsMuted, true))
        {
            Raise(nameof(MuteGlyph));
        }

        if (state.CanRoute != previous.CanRoute)
        {
            Raise(nameof(CanRoute));
        }

        if (state.RoutedDeviceId != previous.RoutedDeviceId)
        {
            RebuildRoutes();
        }
    }

    /// <summary>
    /// Takes a fresh list of the outputs a row could be sent to. Pushed in by
    /// <see cref="AppsViewModel"/> rather than subscribed to here: rows come
    /// and go as applications start and stop, and a per-row subscription
    /// would be a handler left behind on every one of them.
    /// </summary>
    internal void UpdateDevices(IReadOnlyList<AudioDeviceState> outputs)
    {
        _outputs = outputs;
        RebuildRoutes();
    }

    /// <summary>
    /// Sends this application's sound to one device, or back to following the
    /// default when <paramref name="deviceId"/> is null.
    /// </summary>
    internal void ChooseRoute(string? deviceId)
    {
        if (!_state.CanRoute)
        {
            return;
        }

        _audio.SetAppDevice(_state.Id, deviceId);

        // The service reads the route back and publishes before returning, so
        // the state is already the truth by here. Every choice states its own
        // case again, because a radio button unselects its siblings the
        // moment it is clicked — which is also how a move Windows declined
        // snaps back to where the sound actually goes.
        foreach (var option in RouteOptions)
        {
            option.Restate();
        }
    }

    /// <summary>
    /// Brings the menu in line with the outputs there are and the route the
    /// application has: "Default" first and then every output, matched by
    /// device id so a renamed device keeps its place and a hot-plug is an
    /// inserted item rather than a rebuilt menu.
    /// </summary>
    private void RebuildRoutes()
    {
        var wanted = new List<(string? Id, string Name)>(_outputs.Count + 1) { (null, DefaultRouteName) };

        foreach (var output in _outputs)
        {
            wanted.Add((output.Id, output.Name));
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var (id, name) = wanted[index];
            var selected = id == _state.RoutedDeviceId;
            var found = IndexOf(id, index);

            if (found < 0)
            {
                RouteOptions.Insert(index, new RouteOptionViewModel(this, id, name, selected));
                continue;
            }

            if (found != index)
            {
                RouteOptions.Move(found, index);
            }

            RouteOptions[index].Update(name, selected);
        }

        while (RouteOptions.Count > wanted.Count)
        {
            RouteOptions.RemoveAt(RouteOptions.Count - 1);
        }

        Raise(nameof(HasRoute));
        Raise(nameof(RouteName));
        Raise(nameof(RouteToolTip));
    }

    private int IndexOf(string? deviceId, int from)
    {
        for (var index = from; index < RouteOptions.Count; index++)
        {
            if (RouteOptions[index].DeviceId == deviceId)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// What an endpoint id is called, or null where no active output has that
    /// id — a device routed to and then unplugged.
    /// </summary>
    private string? NameOf(string deviceId)
    {
        foreach (var output in _outputs)
        {
            if (output.Id == deviceId)
            {
                return output.Name;
            }
        }

        return null;
    }

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
