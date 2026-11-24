using System.Collections.ObjectModel;
using System.ComponentModel;
using Awoo.Audio;

namespace Awoo.ViewModels;

/// <summary>
/// The Devices tab: every active endpoint, outputs above inputs, each a
/// <see cref="DeviceRowViewModel"/>.
/// </summary>
/// <remarks>
/// The service hands over a whole new list on every change, and this class
/// folds it into the two collections the UI is already bound to rather than
/// rebuilding them. That is what makes a hot-plug an inserted row instead of
/// the flyout flickering, and what lets a slider survive the stream of
/// updates its own drag produces.
/// </remarks>
public sealed class DevicesViewModel : INotifyPropertyChanged
{
    private readonly IAudioService _audio;

    private string? _defaultOutputId;
    private string? _defaultInputId;

    internal DevicesViewModel(IAudioService audio)
    {
        _audio = audio;
        Apply(audio.Devices);
        _audio.DevicesChanged += OnDevicesChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DeviceRowViewModel> Outputs { get; } = [];

    public ObservableCollection<DeviceRowViewModel> Inputs { get; } = [];

    /// <summary>Whether the outputs heading and its rows have anything to show.</summary>
    public bool HasOutputs => Outputs.Count > 0;

    public bool HasInputs => Inputs.Count > 0;

    /// <summary>True on a machine with no audio hardware at all.</summary>
    public bool IsEmpty => Outputs.Count == 0 && Inputs.Count == 0;

    private void OnDevicesChanged(object? sender, IReadOnlyList<AudioDeviceState> devices) => Apply(devices);

    private void Apply(IReadOnlyList<AudioDeviceState> devices)
    {
        var hadOutputs = HasOutputs;
        var hadInputs = HasInputs;

        Reconcile(Outputs, devices, DeviceKind.Output);
        Reconcile(Inputs, devices, DeviceKind.Input);

        // A radio button unselects its siblings the moment it is clicked,
        // before the switch has been through Windows. Once the service says
        // where the default actually is, the whole group states its case
        // again — which is also how a failed switch snaps back.
        RestateIfDefaultMoved(Outputs, DeviceKind.Output, ref _defaultOutputId, devices);
        RestateIfDefaultMoved(Inputs, DeviceKind.Input, ref _defaultInputId, devices);

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
    /// endpoint id: rows that are still there are updated where they stand,
    /// new ones inserted at their place in the order, and gone ones dropped
    /// off the end.
    /// </summary>
    private void Reconcile(
        ObservableCollection<DeviceRowViewModel> rows,
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
                rows.Insert(index, new DeviceRowViewModel(_audio, state));
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

    private static int IndexOf(ObservableCollection<DeviceRowViewModel> rows, string id, int from)
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

    private static void RestateIfDefaultMoved(
        ObservableCollection<DeviceRowViewModel> rows,
        DeviceKind kind,
        ref string? remembered,
        IReadOnlyList<AudioDeviceState> devices)
    {
        var current = devices.FirstOrDefault(device => device.Kind == kind && device.IsDefault).Id;
        if (current == remembered)
        {
            return;
        }

        remembered = current;

        foreach (var row in rows)
        {
            row.RestateDefault();
        }
    }

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
