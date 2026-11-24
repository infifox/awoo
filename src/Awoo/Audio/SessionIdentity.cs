using System.Diagnostics;
using Awoo.Interop;

namespace Awoo.Audio;

/// <summary>
/// Who a session belongs to: the application it should be grouped under, the
/// name to show for it, and where its icon comes from.
/// </summary>
/// <param name="Key">
/// What makes two sessions one row. The executable rather than the process,
/// so a browser's several audio processes are one application the way the
/// Windows mixer shows them.
/// </param>
/// <param name="Name">The name to show.</param>
/// <param name="IconFile">
/// A file to extract the icon from, optionally with a resource index after a
/// comma, or null where there is nothing to extract from.
/// </param>
/// <param name="IsSystemSounds">Whether this is Windows' system-sounds session.</param>
internal readonly record struct SessionIdentity(string Key, string Name, string? IconFile, bool IsSystemSounds)
{
    /// <summary>What a row says when the owning process cannot be identified at all.</summary>
    private const string Unknown = "Unknown application";

    /// <summary>
    /// Works out the identity of one session. Every lookup here can fail —
    /// the process may be another user's, or gone between the session being
    /// enumerated and this running — so each step has a fallback rather than
    /// throwing.
    /// </summary>
    /// <param name="processId">
    /// The session's owning process, which the caller has already read — it
    /// needs it for routing as well, and asking twice would be asking the
    /// same dying process twice.
    /// </param>
    internal static SessionIdentity For(IAudioSessionControl2 control, SessionKind kind, int processId)
    {
        if (control.IsSystemSoundsSession() == CoreAudio.S_OK)
        {
            // Plasma's "Notification Sounds"; Windows calls it this.
            return new SessionIdentity($"{kind}|@system", "System sounds", IconFileOf(control), true);
        }

        var executable = ProcessPath(processId);

        var key = executable is null
            ? $"{kind}|pid:{processId}"
            : $"{kind}|{executable.ToLowerInvariant()}";

        return new SessionIdentity(
            key,
            DisplayName(control) ?? Describe(executable) ?? Unknown,
            IconFileOf(control) ?? executable,
            false);
    }

    /// <summary>
    /// The name the session set for itself, resolved through
    /// <c>SHLoadIndirectString</c> where it is a resource reference. Null
    /// when the session named itself nothing, which is the common case:
    /// most apps leave it to Windows to name them from their executable.
    /// </summary>
    private static string? DisplayName(IAudioSessionControl2 control)
    {
        if (control.GetDisplayName(out var name) != CoreAudio.S_OK || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return name.StartsWith('@') ? Indirect(name) : name;
    }

    /// <summary>
    /// The icon the session set for itself. Windows' own sessions — system
    /// sounds especially — point at a shell resource here, and that is a
    /// better icon than anything their host process could offer.
    /// </summary>
    private static string? IconFileOf(IAudioSessionControl2 control)
    {
        if (control.GetIconPath(out var path) != CoreAudio.S_OK || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return path;
    }

    /// <summary>
    /// A process's executable path, or null where it cannot be read — a
    /// process of another user, or one that has already exited.
    /// </summary>
    private static unsafe string? ProcessPath(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        var process = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);

        if (process == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var buffer = new char[1024];
            var length = buffer.Length;

            fixed (char* start = buffer)
            {
                return NativeMethods.QueryFullProcessImageName(process, 0, start, ref length) && length > 0
                    ? new string(buffer, 0, length)
                    : null;
            }
        }
        finally
        {
            _ = NativeMethods.CloseHandle(process);
        }
    }

    /// <summary>
    /// What the executable calls itself — its file description, the same
    /// string Task Manager and the Windows mixer show — falling back to the
    /// file name without its extension.
    /// </summary>
    private static string? Describe(string? executable)
    {
        if (executable is null)
        {
            return null;
        }

        try
        {
            var description = FileVersionInfo.GetVersionInfo(executable).FileDescription;
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description.Trim();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // No version resource, or the file is not readable from here.
        }

        var name = Path.GetFileNameWithoutExtension(executable);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>Resolves an <c>@file,-id</c> reference, or returns null if Windows will not.</summary>
    private static unsafe string? Indirect(string source)
    {
        var buffer = new char[512];

        fixed (char* start = buffer)
        {
            if (NativeMethods.SHLoadIndirectString(source, start, buffer.Length, IntPtr.Zero) != CoreAudio.S_OK)
            {
                return null;
            }
        }

        var resolved = new string(buffer).TrimEnd('\0');
        return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
    }
}
