using System.ComponentModel;
using Awoo.Audio;
using Awoo.Settings;

namespace Awoo.ViewModels;

/// <summary>
/// One audio endpoint as a row in the settings window: its name, and whether
/// the volume control shows it.
/// </summary>
/// <remarks>
/// <para>
/// Public only because a compiled binding's <c>x:DataType</c> names it from
/// XAML; nothing outside the app constructs one.
/// </para>
/// <para>
/// Write-through like every other row in awoo: the switch asks the settings
/// service and then says what the service holds, rather than remembering an
/// answer of its own.
/// </para>
/// </remarks>
public sealed class DeviceVisibilityViewModel : INotifyPropertyChanged
{
    private readonly SettingsService _settings;
    private AudioDeviceState _state;

    internal DeviceVisibilityViewModel(SettingsService settings, AudioDeviceState state)
    {
        _settings = settings;
        _state = state;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Core Audio's endpoint id — how a row is matched to a device.</summary>
    public string Id => _state.Id;

    public string Name => _state.Name;

    /// <summary>
    /// Whether the flyout lists this device. Hiding one takes nothing away
    /// from the device itself: Windows goes on using it, and awoo only stops
    /// offering it.
    /// </summary>
    public bool IsVisible
    {
        get => !_settings.IsDeviceHidden(_state.Id);
        set
        {
            _settings.SetDeviceHidden(_state.Id, !value);

            // Said back rather than assumed, the way every other setting in
            // awoo is: what is shown is what the service holds.
            Raise(nameof(IsVisible));
            Raise(nameof(VisibilityToolTip));
        }
    }

    public string VisibilityToolTip => IsVisible
        ? "Shown in the volume control"
        : "Hidden from the volume control";

    /// <summary>
    /// Takes a fresh snapshot of the same endpoint. Only the name can show
    /// here, so only the name is worth raising — this row says nothing about
    /// volume, mute or which device is the default.
    /// </summary>
    internal void Update(AudioDeviceState state)
    {
        var previous = _state;
        _state = state;

        if (state.Name != previous.Name)
        {
            Raise(nameof(Name));
        }
    }

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
