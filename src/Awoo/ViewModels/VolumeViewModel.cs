using System.ComponentModel;
using Awoo.Audio;

namespace Awoo.ViewModels;

/// <summary>
/// The default output device as the flyout and the tray icon see it: one
/// object over <see cref="IAudioService"/>, so both show the same thing and
/// neither touches Core Audio.
/// </summary>
/// <remarks>
/// The service raises its changes on the UI thread, so nothing here marshals.
/// Both settable properties are write-through — they do not cache the value
/// the user chose, they ask the service for it and wait to be told what the
/// endpoint took. That is also what stops a two-way binding looping: the echo
/// of a change sets the same value, which the setter drops.
/// </remarks>
internal sealed class VolumeViewModel : INotifyPropertyChanged
{
    /// <summary>Shell_NotifyIcon's tooltip is 128 characters including its terminator.</summary>
    private const int ToolTipLimit = 127;

    private readonly IAudioService _audio;
    private VolumeState _state;

    internal VolumeViewModel(IAudioService audio)
    {
        _audio = audio;
        _state = audio.State;
        _audio.StateChanged += OnStateChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The endpoint's name, as Windows shows it.</summary>
    public string DeviceName => _state.DeviceName;

    /// <summary>Volume 0–100. A double because that is what a Slider binds to.</summary>
    public double Percent
    {
        get => _state.Percent;
        set
        {
            var percent = (int)Math.Round(value);
            if (percent == _state.Percent)
            {
                return;
            }

            _audio.SetPercent(percent);
        }
    }

    public bool IsMuted
    {
        get => _state.IsMuted;
        set
        {
            if (value == _state.IsMuted)
            {
                return;
            }

            _audio.SetMute(value);
        }
    }

    /// <summary>False when the machine has no output device; the controls go insensitive.</summary>
    public bool HasDevice => _state.HasDevice;

    public string PercentText => $"{_state.Percent}%";

    /// <summary>The speaker glyph for the current level, muted included.</summary>
    public string Glyph => VolumeGlyph.For(_state.Level);

    public string MuteToolTip => _state.IsMuted ? "Unmute" : "Mute";

    /// <summary>What hovering the tray icon says: the device, then where it is.</summary>
    public string TrayToolTip
    {
        get
        {
            if (!_state.HasDevice)
            {
                return Fit(_state.DeviceName, ToolTipLimit);
            }

            var reading = _state.IsMuted ? $"Muted — {_state.Percent}%" : $"{_state.Percent}%";
            return $"{Fit(_state.DeviceName, ToolTipLimit - reading.Length - 1)}\n{reading}";
        }
    }

    /// <summary>The level the tray icon draws.</summary>
    internal VolumeLevel Level => _state.Level;

    /// <summary>
    /// Moves the volume by <paramref name="delta"/> percentage points, which
    /// is what one or more notches of the wheel over the tray icon come to.
    /// Clamped at both ends: the top of the range is 100%, because
    /// over-amplification is out of scope.
    /// </summary>
    /// <remarks>
    /// Relative to the volume as it stands rather than to a figure of its
    /// own, so a step lands where it should however the volume last moved —
    /// including by a hardware key or another application a moment earlier.
    /// </remarks>
    internal void Step(int delta)
    {
        if (!_state.HasDevice || delta == 0)
        {
            return;
        }

        var target = Math.Clamp(_state.Percent + delta, VolumeState.MinPercent, VolumeState.MaxPercent);
        if (target == _state.Percent)
        {
            return;
        }

        _audio.SetPercent(target);
    }

    /// <summary>Mutes if it is not muted and unmutes if it is: the middle-click gesture.</summary>
    internal void ToggleMute()
    {
        if (!_state.HasDevice)
        {
            return;
        }

        _audio.SetMute(!_state.IsMuted);
    }

    private void OnStateChanged(object? sender, VolumeState state)
    {
        var previous = _state;
        _state = state;

        if (state.DeviceName != previous.DeviceName)
        {
            Raise(nameof(DeviceName));
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

        if (state.HasDevice != previous.HasDevice)
        {
            Raise(nameof(HasDevice));
        }

        if (state.Level != previous.Level)
        {
            Raise(nameof(Glyph));
        }

        Raise(nameof(TrayToolTip));
    }

    /// <summary>
    /// Trims to what the tooltip has room for. The device name is what gives
    /// way — a name long enough to fill the tooltip on its own would
    /// otherwise push the volume out of it.
    /// </summary>
    private static string Fit(string text, int limit) =>
        text.Length <= limit
            ? text
            : string.Concat(text.AsSpan(0, Math.Max(limit - 1, 0)), "…");

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
