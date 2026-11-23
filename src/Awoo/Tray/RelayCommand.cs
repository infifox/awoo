using System.Windows.Input;

namespace Awoo.Tray;

/// <summary>
/// The tray icon and its menu are driven by commands rather than events,
/// because a window-less app has no visual tree for XAML click handlers to
/// route through.
/// </summary>
internal sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
