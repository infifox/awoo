using System.Collections.ObjectModel;
using System.ComponentModel;
using Awoo.Audio;
using Awoo.Settings;

namespace Awoo.ViewModels;

/// <summary>
/// The settings window: the switches over <see cref="SettingsService"/>, and
/// a row per connected device for hiding it from the volume control.
/// </summary>
/// <remarks>
/// Write-through, the way <see cref="VolumeViewModel"/> is: what is shown is
/// read back from where the setting lives rather than remembered here, so a
/// registry write Windows refuses shows as the switch going back instead of
/// as a lie on the screen.
/// </remarks>
internal sealed class SettingsViewModel : IDisposable, INotifyPropertyChanged
{
    private readonly SettingsService _settings;
    private readonly IAudioService _audio;

    internal SettingsViewModel(SettingsService settings, IAudioService audio)
    {
        _settings = settings;
        _audio = audio;
        Apply(audio.Devices);
        _audio.DevicesChanged += OnDevicesChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>A double because that is what a NumberBox binds to.</summary>
    public double VolumeStep
    {
        get => _settings.VolumeStep;
        set
        {
            // An emptied NumberBox reports NaN, and there is no step in that.
            if (!double.IsNaN(value))
            {
                _settings.VolumeStep = (int)Math.Round(value);
            }

            // Either way, say what was stored rather than what was asked for:
            // the step has a range, and a value outside it comes back clamped.
            Raise(nameof(VolumeStep));
        }
    }

    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set
        {
            _settings.StartWithWindows = value;
            Raise(nameof(StartWithWindows));
        }
    }

    /// <summary>
    /// Whether the volume control sets the default audio device and the
    /// default communications device apart. Turning it off moves no
    /// defaults: the two markers become one again, and what calls were going
    /// through they go on going through until something sets a default.
    /// </summary>
    public bool SplitDefaultRoles
    {
        get => _settings.SplitDefaultRoles;
        set
        {
            _settings.SplitDefaultRoles = value;
            Raise(nameof(SplitDefaultRoles));
        }
    }

    /// <summary>
    /// The connected outputs, each with its visibility switch. Only what is
    /// plugged in: a device that is away cannot be pointed at, and its flag
    /// is kept rather than forgotten — plug it back in and it is here again,
    /// still hidden.
    /// </summary>
    public ObservableCollection<DeviceVisibilityViewModel> Outputs { get; } = [];

    public ObservableCollection<DeviceVisibilityViewModel> Inputs { get; } = [];

    /// <summary>Whether the outputs heading and its rows have anything to show.</summary>
    public bool HasOutputs => Outputs.Count > 0;

    public bool HasInputs => Inputs.Count > 0;

    /// <summary>True on a machine with no audio hardware at all.</summary>
    public bool IsEmpty => Outputs.Count == 0 && Inputs.Count == 0;

    /// <summary>
    /// Lets go of the device list. The window is closed rather than hidden,
    /// and a view model still listening to the service would be a window's
    /// worth of rows kept alive by every settings window ever opened.
    /// </summary>
    public void Dispose() => _audio.DevicesChanged -= OnDevicesChanged;

    private void OnDevicesChanged(object? sender, IReadOnlyList<AudioDeviceState> devices) => Apply(devices);

    private void Apply(IReadOnlyList<AudioDeviceState> devices)
    {
        var hadOutputs = HasOutputs;
        var hadInputs = HasInputs;

        Reconcile(Outputs, devices, DeviceKind.Output);
        Reconcile(Inputs, devices, DeviceKind.Input);

        if (HasOutputs != hadOutputs)
        {
            Raise(nameof(HasOutputs));
        }

        if (HasInputs != hadInputs)
        {
            Raise(nameof(HasInputs));
        }

        if (HasOutputs != hadOutputs || HasInputs != hadInputs)
        {
            Raise(nameof(IsEmpty));
        }
    }

    /// <summary>
    /// Brings one collection in line with the states of one kind, matching by
    /// endpoint id — the same fold <see cref="DevicesViewModel"/> does, and
    /// for the same reason: a device plugged in while the window is open is
    /// an inserted row rather than a list that jumps.
    /// </summary>
    private void Reconcile(
        ObservableCollection<DeviceVisibilityViewModel> rows,
        IReadOnlyList<AudioDeviceState> devices,
        DeviceKind kind)
    {
        var wanted = devices.Where(device => device.Kind == kind).ToArray();

        for (var index = 0; index < wanted.Length; index++)
        {
            var state = wanted[index];
            var found = IndexOf(rows, state.Id, index);

            if (found < 0)
            {
                rows.Insert(index, new DeviceVisibilityViewModel(_settings, state));
                continue;
            }

            if (found != index)
            {
                rows.Move(found, index);
            }

            rows[index].Update(state);
        }

        while (rows.Count > wanted.Length)
        {
            rows.RemoveAt(rows.Count - 1);
        }
    }

    private static int IndexOf(ObservableCollection<DeviceVisibilityViewModel> rows, string id, int from)
    {
        for (var index = from; index < rows.Count; index++)
        {
            if (rows[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
