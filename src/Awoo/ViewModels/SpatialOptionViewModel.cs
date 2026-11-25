using System.ComponentModel;

namespace Awoo.ViewModels;

/// <summary>
/// One choice in a device row's spatial sound menu: a named format, or "Off"
/// for plain stereo.
/// </summary>
/// <remarks>
/// <para>
/// Public only because a compiled binding's <c>x:DataType</c> names it from
/// XAML; nothing outside the app constructs one.
/// </para>
/// <para>
/// The same shape as <see cref="RouteOptionViewModel"/>, and for the same
/// reason: picking one of several is one radio button in a group, and the row
/// that was picked waits to be told it was rather than assuming it.
/// </para>
/// </remarks>
public sealed class SpatialOptionViewModel : INotifyPropertyChanged
{
    private readonly DeviceRowViewModel _row;

    private string _name;
    private bool _isSelected;

    internal SpatialOptionViewModel(DeviceRowViewModel row, Guid? formatId, string name, bool isSelected)
    {
        _row = row;
        _name = name;
        _isSelected = isSelected;
        FormatId = formatId;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Which radio group this choice belongs to — one per device row, so two
    /// rows' menus never unselect each other.
    /// </summary>
    public string GroupName => _row.SpatialGroupName;

    public string Name => _name;

    /// <summary>Whether this is the format the device is in now.</summary>
    public bool IsSelected => _isSelected;

    /// <summary>
    /// The format this choice stands for, or null for "Off". Also how a
    /// choice is matched to a format when the list is rebuilt.
    /// </summary>
    internal Guid? FormatId { get; }

    /// <summary>
    /// Puts the device into this format. Bound to the radio button's
    /// <c>Checked</c> rather than to a command, for the reason
    /// <see cref="DeviceRowViewModel.MakeDefault"/> gives: a command on a
    /// ToggleButton only runs on a real click, and the keyboard and UI
    /// Automation both set the state without one. The guard is what makes
    /// that safe — the binding checks this option when the service says the
    /// device landed in this format, and that echo asks for a change already
    /// made.
    /// </summary>
    public void Choose()
    {
        if (!_isSelected)
        {
            _row.ChooseSpatial(FormatId);
        }
    }

    /// <summary>
    /// Takes a fresh reading of the same choice, raising only what moved —
    /// Windows may name a format differently after an update, and the
    /// selection moves whenever the format does.
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
    /// the format has been read back — including the one that was already
    /// selected, when the change was refused.
    /// </summary>
    internal void Restate() => Raise(nameof(IsSelected));

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
