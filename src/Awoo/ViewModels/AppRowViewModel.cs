using System.ComponentModel;
using Awoo.Audio;
using Microsoft.UI.Xaml.Media;

namespace Awoo.ViewModels;

/// <summary>
/// One application as a row in the Applications tab: its icon and name, and
/// its own volume and mute under the device's.
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
/// <para>
/// Task 05 adds a device selector beside the name; the row's top line is laid
/// out with room for it.
/// </para>
/// </remarks>
public sealed class AppRowViewModel : INotifyPropertyChanged
{
    private readonly IAudioService _audio;
    private AudioAppState _state;

    internal AppRowViewModel(IAudioService audio, AudioAppState state)
    {
        _audio = audio;
        _state = state;
        FallbackGlyph = VolumeGlyph.ForSession(state.Kind);
        Icon = AppIconLoader.Load(state.IconFile);
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
    }

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
