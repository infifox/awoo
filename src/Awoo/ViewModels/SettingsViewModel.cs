using System.ComponentModel;
using Awoo.Settings;

namespace Awoo.ViewModels;

/// <summary>
/// The settings window's two controls over <see cref="SettingsService"/>.
/// </summary>
/// <remarks>
/// Write-through, the way <see cref="VolumeViewModel"/> is: what is shown is
/// read back from where the setting lives rather than remembered here, so a
/// registry write Windows refuses shows as the switch going back instead of
/// as a lie on the screen.
/// </remarks>
internal sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly SettingsService _settings;

    internal SettingsViewModel(SettingsService settings) => _settings = settings;

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

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
