using System.ComponentModel;

namespace Awoo.ViewModels;

/// <summary>
/// One choice in an application row's output-device menu: a named device, or
/// "Default" for following whichever device Windows is using.
/// </summary>
/// <remarks>
/// <para>
/// Public only because a compiled binding's <c>x:DataType</c> names it from
/// XAML; nothing outside the app constructs one.
/// </para>
/// <para>
/// The same shape as the Devices tab's default marker, and for the same
/// reason: picking a device is one radio button in a group, and the row that
/// was picked waits to be told it was rather than assuming it.
/// </para>
/// </remarks>
public sealed class RouteOptionViewModel : INotifyPropertyChanged
{
    private readonly AppRowViewModel _row;

    private string _name;
    private bool _isSelected;

    internal RouteOptionViewModel(AppRowViewModel row, string? deviceId, string name, bool isSelected)
    {
        _row = row;
        _name = name;
        _isSelected = isSelected;
        DeviceId = deviceId;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Which radio group this choice belongs to — one per application row, so
    /// two rows' menus never unselect each other.
    /// </summary>
    public string GroupName => _row.RouteGroupName;

    public string Name => _name;

    /// <summary>Whether this is where the application's sound is going now.</summary>
    public bool IsSelected => _isSelected;

    /// <summary>
    /// The endpoint this choice stands for, or null for "Default". Also how a
    /// choice is matched to a device when the list is rebuilt.
    /// </summary>
    internal string? DeviceId { get; }

    /// <summary>
    /// Sends the application's sound here. Bound to the radio button's
    /// <c>Checked</c> rather than to a command, for the reason
    /// <see cref="DeviceRowViewModel.MakeDefault"/> gives: a command on a
    /// ToggleButton only runs on a real click, and the keyboard and UI
    /// Automation both set the state without one. The guard is what makes
    /// that safe — the binding checks this option when the service says the
    /// route landed here, and that echo asks for a move already made.
    /// </summary>
    public void Choose()
    {
        if (!_isSelected)
        {
            _row.ChooseRoute(DeviceId);
        }
    }

    /// <summary>
    /// Takes a fresh reading of the same choice, raising only what moved —
    /// the device may have been renamed, and the selection moves whenever the
    /// route does.
    /// </summary>
    internal void Update(string name, bool isSelected)
    {
        if (name != _name)
        {
            _name = name;
            Raise(nameof(Name));
        }

        if (isSelected != _isSelected)
        {
            _isSelected = isSelected;
            Raise(nameof(IsSelected));
        }
    }

    /// <summary>
    /// Re-asserts <see cref="IsSelected"/> whatever it is. Clicking a radio
    /// button unselects its siblings locally before Windows has said
    /// anything, so every choice in the group states its own case again once
    /// the route has been read back — including the one that was already
    /// selected, when the move failed.
    /// </summary>
    internal void Restate() => Raise(nameof(IsSelected));

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
