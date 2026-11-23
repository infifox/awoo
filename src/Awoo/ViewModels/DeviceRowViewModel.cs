using System.ComponentModel;
using Awoo.Audio;

namespace Awoo.ViewModels;

/// <summary>
/// One audio endpoint as a row in the Devices tab: its name and kind, its
/// volume and mute, and whether it is the default for its kind.
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
    private readonly IAudioService _audio;
    private AudioDeviceState _state;

    internal DeviceRowViewModel(IAudioService audio, AudioDeviceState state)
    {
        _audio = audio;
        _state = state;

        // The radio group is per kind: an output and an input are each the
        // default of their own list, so they must not unselect each other.
        GroupName = $"awoo.default.{state.Kind}";
        DeviceGlyph = VolumeGlyph.ForKind(state.Kind);
        CanSetDefault = audio.CanSetDefaultDevice;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Core Audio's endpoint id — how a row is matched to a device.</summary>
    public string Id => _state.Id;

    public string Name => _state.Name;

    /// <summary>Which radio group this row's default marker belongs to.</summary>
    public string GroupName { get; }

    /// <summary>A speaker or a microphone: what kind of device this row is.</summary>
    public string DeviceGlyph { get; }

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

    /// <summary>Whether Windows' default for this kind of device is this one.</summary>
    public bool IsDefault => _state.IsDefault;

    public string PercentText => $"{_state.Percent}%";

    /// <summary>The mute button's glyph: an output's level, or a microphone.</summary>
    public string MuteGlyph => VolumeGlyph.ForMute(_state.Kind, Level);

    public string MuteToolTip => _state.IsMuted ? "Unmute" : "Mute";

    public string DefaultToolTip => _state.IsDefault
        ? "Default device"
        : CanSetDefault ? "Set as default device" : "Default device";

    private VolumeLevel Level => VolumeState.LevelOf(_state.Percent, _state.IsMuted, true);

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
        }

        if (VolumeState.LevelOf(state.Percent, state.IsMuted, true) !=
            VolumeState.LevelOf(previous.Percent, previous.IsMuted, true))
        {
            Raise(nameof(MuteGlyph));
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
        Raise(nameof(DefaultToolTip));
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
            _audio.SetDefaultDevice(_state.Id);
        }
    }

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
