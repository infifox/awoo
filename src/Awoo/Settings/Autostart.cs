using Microsoft.Win32;

namespace Awoo.Settings;

/// <summary>
/// "Start with Windows" for an application with no package identity: a value
/// under the current user's Run key, which is what Windows reads at sign-in
/// and what Task Manager's Startup tab lists.
/// </summary>
/// <remarks>
/// The registry is the setting rather than a copy of it. Nothing here caches
/// anything, so the toggle in the settings window says what is actually
/// there — including after somebody has disabled awoo from Task Manager or
/// taken the value out by hand.
/// </remarks>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>The value's name, which is also what Task Manager calls the entry.</summary>
    private const string ValueName = "awoo";

    /// <summary>Whether Windows will start awoo at sign-in.</summary>
    internal static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is not null;
            }
            catch (Exception exception) when (exception is System.Security.SecurityException or IOException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Switches autostart on or off. False where Windows would not take the
    /// change, so a caller can show the setting as it is rather than as it
    /// was asked for.
    /// </summary>
    internal static bool TrySet(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null)
            {
                return false;
            }

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            if (Command() is not { } command)
            {
                return false;
            }

            key.SetValue(ValueName, command, RegistryValueKind.String);
            return true;
        }
        catch (Exception exception) when (exception
            is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Points an existing entry at this copy of awoo. A portable exe moves,
    /// and an installer puts one somewhere else again, either of which leaves
    /// the Run key naming a path that starts the wrong awoo or none at all.
    /// Does nothing where there is no entry: whether awoo starts with Windows
    /// is the user's setting, and this is only about where it starts from.
    /// </summary>
    internal static void Refresh()
    {
        if (Command() is not { } command)
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is not string existing || existing == command)
            {
                return;
            }

            key.SetValue(ValueName, command, RegistryValueKind.String);
        }
        catch (Exception exception) when (exception
            is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // The entry stays as it was, which is the next best thing.
        }
    }

    /// <summary>
    /// What Windows should run. Quoted, because the value is a command line
    /// rather than a path, and an unquoted path with a space in it is a
    /// different command.
    /// </summary>
    private static string? Command() =>
        Environment.ProcessPath is { Length: > 0 } path ? $"\"{path}\"" : null;
}
