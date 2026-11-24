using System.Collections.ObjectModel;
using System.ComponentModel;
using Awoo.Audio;
using Awoo.Settings;

namespace Awoo.ViewModels;

/// <summary>
/// The Applications tab: everything playing above everything recording, each
/// an <see cref="AppRowViewModel"/>, and a placeholder when nothing is doing
/// either.
/// </summary>
/// <remarks>
/// The service hands over a whole new list on every change, and this class
/// folds it into the two collections the UI is already bound to rather than
/// rebuilding them — the same reconciliation
/// <see cref="DevicesViewModel"/> does, and for the same reason: an app
/// starting is an inserted row instead of the flyout flickering, and a slider
/// survives the stream of updates its own drag produces.
/// </remarks>
public sealed class AppsViewModel : INotifyPropertyChanged
{
    private readonly IAudioService _audio;
    private readonly SettingsService _settings;

    /// <summary>
    /// The devices a row could be sent to. Held here and pushed down into the
    /// rows rather than subscribed to by each of them: rows come and go as
    /// applications start and stop, and a per-row subscription would leave a
    /// handler behind on every one.
    /// </summary>
    private IReadOnlyList<AudioDeviceState> _outputs;

    internal AppsViewModel(IAudioService audio, SettingsService settings)
    {
        _audio = audio;
        _settings = settings;
        _outputs = OutputsOf(audio.Devices);
        Apply(audio.Apps);
        _audio.AppsChanged += OnAppsChanged;
        _audio.DevicesChanged += OnDevicesChanged;
        _settings.HiddenDevicesChanged += OnHiddenDevicesChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AppRowViewModel> Playback { get; } = [];

    public ObservableCollection<AppRowViewModel> Recording { get; } = [];

    /// <summary>Whether the playback heading and its rows have anything to show.</summary>
    public bool HasPlayback => Playback.Count > 0;

    public bool HasRecording => Recording.Count > 0;

    /// <summary>True when nothing on the machine is playing or recording.</summary>
    public bool IsEmpty => Playback.Count == 0 && Recording.Count == 0;

    /// <summary>
    /// Re-reads where every application's sound is going. Nothing announces a
    /// route changing — not awoo's own write and not one made in Windows'
    /// Settings — so the flyout calls this as it opens, which is when a
    /// stale selector would be seen.
    /// </summary>
    internal void Refresh() => _audio.RefreshAppRoutes();

    private void OnAppsChanged(object? sender, IReadOnlyList<AudioAppState> apps) => Apply(apps);

    /// <summary>
    /// A device plugged in, pulled out or renamed changes what every row's
    /// selector can offer, and what the device a row is routed to is called.
    /// </summary>
    private void OnDevicesChanged(object? sender, IReadOnlyList<AudioDeviceState> devices)
    {
        _outputs = OutputsOf(devices);

        foreach (var row in Playback.Concat(Recording))
        {
            row.UpdateDevices(_outputs);
        }
    }

    /// <summary>
    /// A device hidden or shown again in the settings window. The outputs are
    /// the same as before; which of them a row will offer is not, so every
    /// menu is built again over the same list.
    /// </summary>
    private void OnHiddenDevicesChanged(object? sender, EventArgs args)
    {
        foreach (var row in Playback.Concat(Recording))
        {
            row.UpdateDevices(_outputs);
        }
    }

    private static IReadOnlyList<AudioDeviceState> OutputsOf(IReadOnlyList<AudioDeviceState> devices) =>
        devices.Where(device => device.Kind == DeviceKind.Output).ToArray();

    private void Apply(IReadOnlyList<AudioAppState> apps)
    {
        var hadPlayback = HasPlayback;
        var hadRecording = HasRecording;

        Reconcile(Playback, apps, SessionKind.Playback);
        Reconcile(Recording, apps, SessionKind.Recording);

        if (HasPlayback != hadPlayback)
        {
            Raise(nameof(HasPlayback));
        }

        if (HasRecording != hadRecording)
        {
            Raise(nameof(HasRecording));
        }

        if (HasPlayback != hadPlayback || HasRecording != hadRecording)
        {
            Raise(nameof(IsEmpty));
        }
    }

    /// <summary>
    /// Brings one collection in line with the states of one kind, matching by
    /// application id: rows that are still there are updated where they
    /// stand, new ones inserted at their place in the order, and gone ones
    /// dropped off the end.
    /// </summary>
    private void Reconcile(
        ObservableCollection<AppRowViewModel> rows,
        IReadOnlyList<AudioAppState> apps,
        SessionKind kind)
    {
        var wanted = apps.Where(app => app.Kind == kind).ToArray();

        for (var index = 0; index < wanted.Length; index++)
        {
            var state = wanted[index];
            var found = IndexOf(rows, state.Id, index);

            if (found < 0)
            {
                rows.Insert(index, new AppRowViewModel(_audio, _settings, state, _outputs));
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

    private static int IndexOf(ObservableCollection<AppRowViewModel> rows, string id, int from)
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
